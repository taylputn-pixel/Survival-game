using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using GrandmasHouse.Animals;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Garden;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using GrandmasHouse.Trade;
using UnityEngine;

namespace GrandmasHouse.Tests.Sim
{
    // One scripted player living one year in the real Homestead. The garden,
    // the hens, the weather and the traders are the game's own: the run does
    // the chores through their methods and sleeps the real clock, so plants
    // grow, wilt, get hornworms and die, hens lay, and the store's truck comes
    // or doesn't exactly as in play. The kitchen is kept as a ledger instead,
    // using the game's numbers: what each food fills, how fast it spoils (and
    // how much slower in the cellar or fridge), what soup is worth
    // (MealSettings), what a jar of each recipe is worth, the chance a lid
    // fails to seal, and how long a piece of firewood burns. Hunger and cold
    // run through the real Needs rules hour by hour.
    //
    // Not modelled: walking about and carrying (time is free), rest and
    // splitting wood costing rest, salt, vinegar and lids running out, the
    // dehydrator, smokehouse and meat, and more than one player.
    public class Run
    {
        // Pieces each thing cuts into for the pot (players choose the
        // thickness, so these are typical), and the MealSettings kind.
        static readonly Dictionary<string, (string kind, int pieces)> Cut = new Dictionary<string, (string, int)>
        {
            { "Potato", ("Potato", 8) }, { "Carrot", ("Carrot", 8) }, { "WinterSquash", ("Winter", 12) },
            { "Onion", ("Onion", 6) }, { "Tomato", ("Tomato", 4) }, { "Cucumber", ("Cucumber", 14) },
            { "Jalapeno", ("Jalapeño", 6) }, { "GarlicBulb", ("Clove", 10) }, { "Egg", ("Egg", 1) }, { "Cabbage", ("Cabbage", 10) },
        };
        // Go to the root cellar when kept cold; the rest go in the fridge.
        static readonly HashSet<string> Keepers = new HashSet<string> { "Potato", "Carrot", "WinterSquash", "Onion", "Cabbage", "GarlicBulb" };
        const float CellarRate = 0.2f, FridgeRate = 0.3f;
        const int PotPieces = 44, CucumberSlices = 14, CabbageShreds = 10, CrockShreds = 48, JarsADay = 14;
        // How much of the garden each crop gets, given seeds.
        static readonly Dictionary<string, float> Mix = new Dictionary<string, float>
        {
            { "Crop_Potato", 3f }, { "Crop_Tomato", 3f }, { "Crop_Squash", 2f }, { "Crop_Carrot", 2f }, { "Crop_Cabbage", 2f },
            { "Crop_Cucumber", 2f }, { "Crop_Onion", 1f }, { "Crop_Jalapeno", 1f }, { "Crop_Garlic", 1f }, { "Crop_Dill", 1f }, { "Crop_Marigold", 0.5f },
        };

        class Food
        {
            public string Key;
            public string Name;
            public float Value;
            public float LifeDays;
            public float Fresh = 1f;
            public float Rate = 1f;
            // Sealed, canned or fermented: it keeps.
            public bool Kept;
            public bool Bowl;
            public float DaysLeft => Kept ? 9999f : Fresh * LifeDays / Mathf.Max(0.01f, Rate);
        }

        public readonly Policy Policy;
        public readonly int Seed;
        public readonly List<string> Days = new List<string>();

        // ---- The game ----
        DayClock clock;
        Weather weather;
        CalendarSettings calendar;
        SurvivalSettings survival;
        WeatherSettings weatherSettings;
        GardenSettings garden;
        MealSettings meals;
        TradeSettings trade;
        Flock flock;
        GardenBed[] beds;
        Trader[] traders;
        Trader store, ada, walt;
        RecipeData pickles, tomatoes, salsa, garlic;
        float sealFail, badSealFail, hoursPerPiece;
        int logPieces;

        // ---- The household ----
        Needs needs;
        readonly List<Food> larder = new List<Food>();
        readonly Dictionary<CropData, int> seeds = new Dictionary<CropData, int>();
        readonly Dictionary<CropData, int> planted = new Dictionary<CropData, int>();
        int emptyJars, feedScoops, firewood, startingLogs;
        float fuelLeft, heat, credit;
        int crockShreds, crockReadyDay = -1;

        // ---- What happened ----
        float sold, eaten, ateToday, harvested, harvestedToday, spoiled, hungryHours, starvingHours, coldHours, freezingHours;
        int collapses, firstCollapse = -1, sown, diedDry, diedPest, frostKilled, jarsSealed, jarsFailed, krautBowls, eggs;
        int beansBought, seedPacketsBought, jarsBought, chicksBought, foxTook, hensStarved, burned, logsBought, deliveries, outOfStock, eggsSold;
        int lastDelivery = -1, firstHarvest = -1;
        readonly int[] hungryByMonth = new int[8], emptyByMonth = new int[8];
        readonly Dictionary<string, int> picked = new Dictionary<string, int>();

        public Run(Policy policy, int seed)
        {
            Policy = policy;
            Seed = seed;
        }

        static float Roll => Random.value;

        public IEnumerator Play()
        {
            Setup();
            int year = calendar.DaysInYear;
            for (int day = 1; day <= year; day++)
            {
                yield return Morning(day);
                for (int h = 7; h < 22; h++) Hour(day, h, false);
                Evening(day);
                for (int h = 22; h < 31; h++) Hour(day, h, true);
                yield return Night(day);
            }
        }

        void Setup()
        {
            clock = Homestead.Find<DayClock>();
            weather = Homestead.Find<Weather>();
            flock = Homestead.Find<Flock>();
            beds = Object.FindObjectsByType<GardenBed>(FindObjectsSortMode.InstanceID);
            traders = Object.FindObjectsByType<Trader>(FindObjectsSortMode.InstanceID);
            foreach (var t in traders)
            {
                if (t.IsStore) store = t;
                else if (t.Name == "Ada") ada = t;
                else if (t.Name == "Walt") walt = t;
            }
            calendar = weather.Calendar;
            weatherSettings = weather.Settings;
            garden = beds[0].Settings;
            var playerNeeds = Homestead.Find<PlayerNeeds>();
            survival = playerNeeds.Settings;
            meals = Resources.FindObjectsOfTypeAll<MealSettings>()[0];
            trade = Resources.FindObjectsOfTypeAll<TradeSettings>()[0];
            foreach (var r in Resources.FindObjectsOfTypeAll<RecipeData>())
            {
                if (r.name == "Recipe_DillPickleChips") pickles = r;
                else if (r.name == "Recipe_CannedTomatoes") tomatoes = r;
                else if (r.name == "Recipe_Salsa") salsa = r;
                else if (r.name == "Recipe_PickledGarlic") garlic = r;
            }
            var jarPrefab = SaveManager.Catalog.Item("PintJar").GetComponent<JarContents>();
            sealFail = Field<float>(jarPrefab, "sealFailChance");
            badSealFail = Field<float>(jarPrefab, "badHeadspaceSealFail");
            logPieces = Field<int>(SaveManager.Catalog.Item("Log").GetComponent<Log>(), "pieces");
            hoursPerPiece = Field<float>(Homestead.Find<WoodStove>(), "hoursPerLog");

            // The sim keeps the time, the weather's frosts and the hens' nights itself.
            clock.enabled = false;
            weather.enabled = false;
            flock.enabled = false;
            playerNeeds.enabled = false;
            Random.InitState(Seed);
            weather.Model.Seed = Seed;

            needs = new Needs(survival);
            foreach (var packet in Object.FindObjectsByType<SeedPacket>(FindObjectsSortMode.InstanceID))
            {
                seeds.TryGetValue(packet.Crop, out int n);
                seeds[packet.Crop] = n + packet.Seeds;
            }
            foreach (var sack in Object.FindObjectsByType<FeedSack>(FindObjectsSortMode.None)) feedScoops += sack.Scoops;
            firewood = Object.FindObjectsByType<Firewood>(FindObjectsSortMode.None).Length;
            startingLogs = Object.FindObjectsByType<Log>(FindObjectsSortMode.None).Length;
            foreach (var jar in Object.FindObjectsByType<JarContents>(FindObjectsSortMode.None))
            {
                if (jar.TryGetComponent(out JarFood food) && food.CanEat)
                {
                    larder.Add(new Food { Key = "Jar", Name = jar.Contents, Value = food.Nutrition, Kept = true });
                }
                else if (!jar.HasContents) emptyJars++;
            }
            foreach (var fresh in Object.FindObjectsByType<FreshFood>(FindObjectsSortMode.None))
            {
                var perishable = fresh.GetComponent<Perishable>();
                larder.Add(new Food { Key = Key(fresh), Name = fresh.name, Value = fresh.Nutrition, LifeDays = perishable != null ? perishable.ShelfLifeDays : 9999f, Kept = true });
            }
            if (Policy == Policy.Diligent && Seed == 1000)
            {
                var start = new StringBuilder("[YearSim] start: ");
                start.Append(beds.Length + " beds, " + beds[0].Spots.Count + " spots each; ");
                start.Append(emptyJars + " empty jars, " + feedScoops + " feed scoops, " + firewood + " firewood, " + startingLogs + " logs about; seeds ");
                foreach (var pair in seeds) start.Append(pair.Key.name + " " + pair.Value + ", ");
                foreach (var t in traders)
                {
                    start.Append("\n  " + t.Name + (t.IsStore ? " (store)" : "") + " wants [" + string.Join(", ", Wants(t)) + "] sells ");
                    foreach (var w in t.Wares) if (w.recipe == null) start.Append(w.key + " x" + w.stock + " @" + Price(t, w).ToString("0.0") + ", ");
                }
                Debug.Log(start.ToString());
            }
        }

        static T Field<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert(field != null, target.GetType().Name + "." + name);
            return (T)field.GetValue(target);
        }

        static void Assert(bool ok, string what)
        {
            if (!ok) throw new System.Exception("YearSim can't find " + what);
        }

        static string Key(Component item) => item.TryGetComponent(out SavedItem saved) ? saved.Key : item.name;

        int Month(int day) => calendar.DateOf(day).month;

        // ---- Morning: the garden, the hens, the trip to town ----

        IEnumerator Morning(int day)
        {
            harvestedToday = 0f;
            ateToday = 0f;
            int month = Month(day);
            bool frozen = weather.Model.DayOf(day).high < weatherSettings.groundFrozenBelow;
            var skipBed = new Dictionary<GardenBed, bool>();
            foreach (var bed in beds)
            {
                if (bed.Covered && Roll >= Policy.ForgetsUncover) bed.Cover(false);
                skipBed[bed] = bed.Covered;
            }

            // Pick what's ripe.
            foreach (var bed in beds)
            {
                if (skipBed[bed]) continue;
                foreach (var spot in bed.Spots)
                {
                    foreach (var item in new List<Pickup>(spot.Ripe))
                    {
                        if (item == null) continue;
                        Harvest(item);
                        Net.ItemNet.Remove(item.gameObject);
                    }
                }
            }
            yield return null;
            yield return null;

            // Weeds, hornworms, suckers.
            foreach (var bed in beds)
            {
                if (skipBed[bed] || Roll < Policy.SkipTending) continue;
                foreach (var weed in new List<Weed>(bed.Weeds)) bed.PullOut(weed);
                foreach (var spot in bed.Spots)
                {
                    foreach (var thing in new List<Pinchable>(spot.Pinchables)) bed.TakeOff(spot, thing);
                }
            }

            // Plant the open spots, in spring and summer.
            if (!frozen && month <= 1)
            {
                foreach (var bed in beds)
                {
                    if (skipBed[bed]) continue;
                    foreach (var spot in bed.Spots)
                    {
                        if (!spot.IsOpen) continue;
                        CropData crop = NextCrop();
                        if (crop == null) break;
                        bed.Plant(spot, crop, 0f);
                        seeds[crop]--;
                        planted.TryGetValue(crop, out int n);
                        planted[crop] = n + 1;
                        sown++;
                    }
                }
                yield return null;
            }

            foreach (var bed in beds)
            {
                if (skipBed[bed] || Roll < Policy.SkipWatering) continue;
                foreach (var spot in bed.Spots) if (!spot.IsEmpty && !spot.IsDead) bed.Water(spot, 1f);
            }

            Hens();
            if (day == 1 || day % Policy.TripEvery == 0) Trip(day);
        }

        void Harvest(Pickup item)
        {
            string key = Key(item);
            var prefab = SaveManager.Catalog.Item(key);
            var source = prefab != null ? prefab.gameObject : item.gameObject;
            var fresh = source.GetComponent<FreshFood>();
            var perishable = source.GetComponent<Perishable>();
            if (fresh == null) return;
            Store(new Food { Key = key, Name = key, Value = fresh.Nutrition, LifeDays = perishable != null ? perishable.ShelfLifeDays : 9999f });
            harvested += fresh.Nutrition;
            harvestedToday += fresh.Nutrition;
            if (firstHarvest < 0) firstHarvest = clock.Day;
            picked.TryGetValue(key, out int n);
            picked[key] = n + 1;
        }

        void Store(Food food)
        {
            if (!food.Kept)
            {
                food.Rate = !Policy.KeepsCold ? 1f : Keepers.Contains(food.Key) ? CellarRate : FridgeRate;
                if (food.Bowl && Policy.Cooks) food.Rate = FridgeRate;
            }
            larder.Add(food);
        }

        // The crop furthest below its share of the garden, with seed left.
        CropData NextCrop()
        {
            CropData best = null;
            float bestShare = float.MaxValue;
            foreach (var pair in seeds)
            {
                if (pair.Value <= 0 || !Mix.TryGetValue(pair.Key.name, out float weight) || weight <= 0f) continue;
                planted.TryGetValue(pair.Key, out int n);
                float share = n / weight;
                if (share < bestShare) { bestShare = share; best = pair.Key; }
            }
            return best;
        }

        void Hens()
        {
            // Eggs from the nest boxes.
            foreach (var item in Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None))
            {
                if (item == null || Key(item) != "Egg") continue;
                var prefab = SaveManager.Catalog.Item("Egg");
                Store(new Food { Key = "Egg", Name = "Egg", Value = prefab.GetComponent<FreshFood>().Nutrition, LifeDays = prefab.GetComponent<Perishable>().ShelfLifeDays });
                Net.ItemNet.Remove(item.gameObject);
            }
            if (flock.Alive == 0 || Roll < Policy.SkipHens) return;
            var settings = flock.Settings;
            while (flock.Feed < 1f - settings.scoop + 0.001f && feedScoops > 0)
            {
                flock.AddFeed(settings.scoop);
                feedScoops--;
            }
            flock.AddWater(1f);
        }

        // ---- Trading ----

        static List<string> Wants(Trader t)
        {
            var list = Field<List<string>>(t, "wants");
            return t.IsStore ? new List<string> { "anything" } : list;
        }

        float Price(Trader t, Trader.Ware w)
        {
            float worth = w.worth > 0f ? w.worth : trade.BaseWorth(w.key, t.Month);
            return t.IsStore ? worth : worth * trade.neighborAsks;
        }

        Trader.Ware Ware(Trader t, string key)
        {
            if (t == null) return null;
            foreach (var w in t.Wares) if (w.key == key && w.recipe == null) return w;
            return null;
        }

        bool Buy(Trader t, string key)
        {
            var w = Ware(t, key);
            if (w == null) return false;
            if (w.stock <= 0) { outOfStock++; return false; }
            float price = Price(t, w);
            if (credit < price) return false;
            credit -= price;
            w.stock--;
            return true;
        }

        void Trip(int day)
        {
            int month = Month(day);
            if (!ada.Welcomed)
            {
                ada.Welcome();
                foreach (var box in Object.FindObjectsByType<ChickBox>(FindObjectsSortMode.None))
                {
                    flock.AdoptHere(flock.SpotInRun());
                    Net.ItemNet.Remove(box.gameObject);
                }
            }

            // What's needed most first: feed, wood once fall comes, food if
            // the larder's bare, chicks to fill the coop, seeds, jars. Goods
            // are sold to pay for each as it comes.
            int scoopsPerSack = SaveManager.Catalog.Item("ChickenFeed").GetComponent<FeedSack>().Scoops;
            if (flock.Alive > 0 && feedScoops < 8 && Afford(store, "ChickenFeed", month, true)) feedScoops += scoopsPerSack;

            int wanted = month < 2 ? 0 : Policy.LaysInWood ? WinterWood(day) : Mathf.Min(WinterWood(day), 8);
            while (firewood < wanted && Afford(walt, "Log", month, true))
            {
                firewood += logPieces;
                logsBought++;
                YearTally.Add(YearTally.Count.Firewood, logPieces);
            }

            while (FoodDays() < 3f && Afford(store, "CannedBeans", month, false, keptToo: false))
            {
                var beans = SaveManager.Catalog.Item("CannedBeans").GetComponent<FreshFood>();
                Store(new Food { Key = "CannedBeans", Name = "Canned beans", Value = beans.Nutrition, Kept = true });
                beansBought++;
            }

            while (flock.HasRoom && month <= 2 && Afford(ada, "ChickBox", month, false))
            {
                flock.AdoptHere(flock.SpotInRun());
                chicksBought++;
            }

            // Seeds for anything running out, while there's planting to do.
            if (month <= 1)
            {
                foreach (var w in store.Wares)
                {
                    if (w.recipe != null) continue;
                    var prefab = SaveManager.Catalog.Item(w.key);
                    if (prefab == null || !prefab.TryGetComponent(out SeedPacket packet) || packet.Crop == null) continue;
                    seeds.TryGetValue(packet.Crop, out int have);
                    if (have < 3 && Mix.TryGetValue(packet.Crop.name, out float weight) && weight >= 1f && Afford(store, w.key, month, false))
                    {
                        seeds[packet.Crop] = have + packet.Seeds;
                        seedPacketsBought++;
                    }
                }
            }

            // Jars for putting up, through summer and fall.
            if (Policy.CansOnADay > 0f && month >= 1 && month <= 2)
            {
                while (emptyJars < 6 && Afford(store, "PintJar", month, false))
                {
                    emptyJars++;
                    jarsBought++;
                }
            }
        }

        // Buy one, selling goods to make up the price. Spare eggs and fresh
        // produce go first; the larder's keeping food (roots, then jars) only
        // for something you can't do without.
        bool Afford(Trader t, string key, int month, bool needed, bool keptToo = true)
        {
            var w = Ware(t, key);
            if (w == null) return false;
            if (w.stock <= 0) { outOfStock++; return false; }
            float price = Price(t, w);
            if (credit < price) Sell(price - credit, month, needed, keptToo);
            return Buy(t, key);
        }

        void Sell(float short_, int month, bool needed, bool keptToo)
        {
            larder.Sort((a, b) => a.DaysLeft.CompareTo(b.DaysLeft));
            // What's kept back for eating: a few days of fresh food, a week of keeping food.
            float freshFloor = 160f, keptFloor = needed ? 80f : 400f;
            int eggsKept = 0;
            float fresh = 0f, kept = 0f;
            foreach (var f in larder)
            {
                if (f.Bowl || f.Key == "Kraut") continue;
                if (f.Kept || Keepers.Contains(f.Key)) kept += f.Value; else fresh += f.Value;
            }
            for (int pass = 0; pass < (keptToo ? 3 : 1) && short_ > 0f; pass++)
            {
                for (int i = 0; i < larder.Count && short_ > 0f;)
                {
                    var f = larder[i];
                    bool sell = false;
                    if (f.Bowl || f.Key == "Kraut" || f.Key == "OpenJar") { i++; continue; }
                    if (pass == 0) sell = f.Key == "Egg" ? ++eggsKept > 6 : !f.Kept && !Keepers.Contains(f.Key) && fresh - f.Value >= freshFloor;
                    else if (pass == 1) sell = !f.Kept && Keepers.Contains(f.Key) && kept - f.Value >= keptFloor;
                    else sell = f.Kept && kept - f.Value >= keptFloor;
                    float price = sell ? Worth(f, month) : 0f;
                    if (price <= 0f) { i++; continue; }
                    credit += price;
                    short_ -= price;
                    sold += f.Value;
                    if (f.Key == "Egg") eggsSold++;
                    if (f.Kept || Keepers.Contains(f.Key)) kept -= f.Value; else fresh -= f.Value;
                    larder.RemoveAt(i);
                }
            }
        }

        // What one thing fetches: full worth from a neighbor who wants it,
        // the store's share otherwise.
        float Worth(Food f, int month)
        {
            string key = f.Key == "Jar" ? "Jar" : f.Key;
            float worth;
            if (f.Key == "Jar")
            {
                worth = trade.sealedJarDefault;
                foreach (var j in trade.jars) if (j.recipe == f.Name) worth = j.worth;
                worth *= trade.RibbonWorth(Ribbon.White);
            }
            else worth = trade.BaseWorth(key, month);
            if (!f.Kept && f.Fresh < 0.5f) worth *= trade.turnedShare;
            foreach (var t in traders)
            {
                if (!t.IsStore && Field<List<string>>(t, "wants").Contains(key)) return worth;
            }
            return worth * trade.storeTakes;
        }

        // Firewood to see out the year from here, burned the way Hour burns it.
        int WinterWood(int day)
        {
            float fuel = fuelLeft, warmth = heat;
            int pieces = 0;
            for (int d = day; d <= calendar.DaysInYear; d++)
            {
                for (int h = 7; h < 31; h++)
                {
                    float house = weather.Model.At(d + h / 24, h % 24) + weatherSettings.houseHolds + (h >= 22 ? weatherSettings.inBed : 0f);
                    bool lit = false;
                    if (WantsFire(house, warmth))
                    {
                        if (fuel <= 0f) { pieces++; fuel += hoursPerPiece; }
                        fuel -= 1f;
                        lit = true;
                    }
                    warmth = lit ? Mathf.Min(1f, warmth + 1f) : Mathf.Max(0f, warmth - 1f / 3f);
                }
            }
            return pieces;
        }

        bool WantsFire(float house, float warmth) =>
            house + weatherSettings.stoveHeat * warmth < survival.comfortableAbove + 5f || (house < survival.comfortableAbove && warmth < 1f);

        float FoodDays()
        {
            float food = 0f;
            foreach (var f in larder) food += f.Value;
            return food / 80f;
        }

        // ---- The hours of the day ----

        void Hour(int day, int h, bool asleep)
        {
            int gameDay = day + h / 24;
            float hour = h % 24;
            if (!asleep)
            {
                if (h == 12 && Policy.Cooks) Cook();
                if (h == 15 && Roll < Policy.CansOnADay) PutUp(day);
                if (h == 8 || h == 13 || h == 19) Meal();
            }

            // The stove, when the house would be cold without it.
            float outside = weather.Model.At(gameDay, hour);
            bool outdoors = !asleep && h >= 9 && h < 11;
            float house = outside + weatherSettings.houseHolds + (asleep ? weatherSettings.inBed : 0f);
            bool lit = false;
            if (WantsFire(house, heat))
            {
                if (fuelLeft <= 0f && firewood > 0)
                {
                    firewood--;
                    burned++;
                    fuelLeft += hoursPerPiece;
                }
                if (fuelLeft > 0f)
                {
                    fuelLeft -= 1f;
                    lit = true;
                }
            }
            heat = lit ? Mathf.Min(1f, heat + 1f) : Mathf.Max(0f, heat - 1f / 3f);
            float felt = outdoors ? outside : house + weatherSettings.stoveHeat * heat;

            float before = needs.starvingHours, beforeFreeze = needs.freezingHours;
            needs.Pass(1f, asleep, survival, felt);
            if (needs.hunger < survival.hungryBelow) { hungryHours++; hungryByMonth[Month(gameDay)]++; }
            if (needs.hunger <= 0f) { starvingHours++; emptyByMonth[Month(gameDay)]++; }
            if (needs.warmth < survival.coldBelow) coldHours++;
            freezingHours += Mathf.Max(0f, needs.freezingHours - beforeFreeze);
            if (needs.Collapsed(survival))
            {
                collapses++;
                if (firstCollapse < 0) firstCollapse = gameDay;
                // Alone this is the end; carry on to see the rest of the year.
                needs.BringRound(survival);
                needs.WarmUp(survival);
            }

            Spoil(1f);
        }

        void Spoil(float hours)
        {
            for (int i = larder.Count - 1; i >= 0; i--)
            {
                var f = larder[i];
                if (f.Kept) continue;
                f.Fresh = Perishable.Spoil(f.Fresh, hours, f.LifeDays, f.Rate);
                if (f.Fresh > 0f) continue;
                spoiled += f.Value;
                larder.RemoveAt(i);
                // Rotten produce goes to the hens.
                if (flock.Alive > 0 && !f.Bowl) flock.AddFeed(flock.Settings.spoiledFeed);
            }
        }

        // Eat until full: what will spoil soonest first, the jars last.
        void Meal()
        {
            larder.Sort((a, b) => Order(a).CompareTo(Order(b)));
            for (int i = 0; i < larder.Count && needs.hunger < 80f;)
            {
                var f = larder[i];
                if (needs.hunger + f.Value > 105f && needs.hunger > 40f) { i++; continue; }
                needs.Eat(f.Value, 0f, survival);
                eaten += f.Value;
                ateToday += f.Value;
                if (f.Key == "Jar") emptyJars++;
                larder.RemoveAt(i);
            }
        }

        static float Order(Food f) => f.Bowl ? f.DaysLeft - 1000f : f.Kept ? 5000f + (f.Key == "CannedBeans" ? 1f : 0f) : f.DaysLeft;

        // A pot of soup from what won't keep, about a day and a half's eating.
        void Cook()
        {
            float inBowls = 0f;
            foreach (var f in larder) if (f.Bowl) inBowls += f.Value;
            if (inBowls >= 60f) return;
            larder.Sort((a, b) => a.DaysLeft.CompareTo(b.DaysLeft));
            var counts = new Dictionary<string, int>();
            var used = new List<Food>();
            int pieces = 0;
            foreach (var f in larder)
            {
                if (f.Kept || f.Bowl || !Cut.TryGetValue(f.Key, out var cut) || pieces + cut.pieces > PotPieces) continue;
                counts.TryGetValue(cut.kind, out int n);
                counts[cut.kind] = n + cut.pieces;
                pieces += cut.pieces;
                used.Add(f);
                if (meals.PotFood(counts, true, true) >= 120f) break;
            }
            float pot = meals.PotFood(counts, true, true);
            if (pot <= 0f) return;
            foreach (var f in used) larder.Remove(f);
            int bowls = Mathf.RoundToInt(1f / meals.bowlShare);
            for (int i = 0; i < bowls; i++)
            {
                Store(new Food { Key = "Bowl", Name = MealSettings.DishName(counts, true), Value = pot / bowls, LifeDays = 1f, Bowl = true });
            }
        }

        // Jars of what won't keep, and cabbage into the crock.
        void PutUp(int day)
        {
            int jars = 0;
            while (jars < JarsADay && emptyJars > 0)
            {
                if (Have("Tomato") >= salsa.tomatoesPerPot && Have("Onion") >= salsa.onionsPerPot && Have("Jalapeno") >= salsa.peppersPerPot && emptyJars >= 3)
                {
                    Take("Tomato", salsa.tomatoesPerPot);
                    Take("Onion", salsa.onionsPerPot);
                    Take("Jalapeno", salsa.peppersPerPot);
                    int made = Mathf.Min(emptyJars, Mathf.FloorToInt(1f / salsa.potPerJar));
                    for (int i = 0; i < made; i++) Seal(salsa);
                    jars += made;
                }
                else if (Have("Tomato") >= tomatoes.wedgesPerJar / 4)
                {
                    Take("Tomato", tomatoes.wedgesPerJar / 4);
                    Seal(tomatoes);
                    jars++;
                }
                else if (Have("Cucumber") * CucumberSlices >= pickles.slicesPerJar)
                {
                    Take("Cucumber", Mathf.CeilToInt(pickles.slicesPerJar / (float)CucumberSlices));
                    if (Have("Dill") > 0) Take("Dill", 1);
                    Seal(pickles);
                    jars++;
                }
                else break;
            }

            // The crock: cabbage in, salted; sauerkraut a few days after the last went in.
            while (Have("Cabbage") > 0 && crockShreds + CabbageShreds <= CrockShreds)
            {
                Take("Cabbage", 1);
                crockShreds += CabbageShreds;
                crockReadyDay = day + 6;
            }
        }

        int Have(string key)
        {
            int n = 0;
            foreach (var f in larder) if (f.Key == key && !f.Kept) n++;
            return n;
        }

        void Take(string key, int count)
        {
            larder.Sort((a, b) => a.DaysLeft.CompareTo(b.DaysLeft));
            for (int i = 0; i < larder.Count && count > 0;)
            {
                if (larder[i].Key == key && !larder[i].Kept) { larder.RemoveAt(i); count--; }
                else i++;
            }
        }

        void Seal(RecipeData recipe)
        {
            emptyJars--;
            float chance = Roll < Policy.BadHeadspace ? badSealFail : sealFail;
            if (Roll < chance)
            {
                // Didn't seal: it keeps like an open jar, in the fridge.
                jarsFailed++;
                Store(new Food { Key = "OpenJar", Name = recipe.displayName, Value = recipe.nutrition, LifeDays = 4f, Rate = FridgeRate });
                emptyJars++;
                return;
            }
            jarsSealed++;
            YearTally.Add(YearTally.Count.Jar);
            larder.Add(new Food { Key = "Jar", Name = recipe.displayName, Value = recipe.nutrition, Kept = true });
        }

        // ---- Night ----

        void Evening(int day)
        {
            // Tonight's coldest, to dawn.
            float coldest = float.MaxValue;
            for (int h = 18; h < 31; h++) coldest = Mathf.Min(coldest, weather.Model.At(day + h / 24, h % 24));
            if (coldest > weatherSettings.killingFrost) return;
            bool hard = coldest <= weather.HardFreeze;
            foreach (var bed in beds)
            {
                // The radio called it: pick what's ripe before it's lost.
                if (!bed.Covered && Roll < Policy.CoversFrost)
                {
                    foreach (var spot in bed.Spots)
                    {
                        foreach (var item in new List<Pickup>(spot.Ripe))
                        {
                            if (item == null) continue;
                            Harvest(item);
                            Net.ItemNet.Remove(item.gameObject);
                        }
                    }
                }
                bool growing = false;
                foreach (var spot in bed.Spots) if (!spot.IsEmpty && !spot.IsDead) growing = true;
                if (growing && !hard && Roll < Policy.CoversFrost) bed.Cover(true);
                frostKilled += bed.Frost(hard);
            }
        }

        IEnumerator Night(int day)
        {
            var dying = new Dictionary<PlantSpot, bool>();
            foreach (var bed in beds) foreach (var spot in bed.Spots) dying[spot] = !spot.IsEmpty && !spot.IsDead;

            string overnight = null;
            System.Action<string> heard = what => overnight = what;
            flock.Overnight += heard;
            clock.SleepUntilMorning();
            flock.Live(Roll < Policy.ForgetsDoor ? Random.value : 1f);
            flock.Overnight -= heard;
            if (overnight != null)
            {
                if (overnight.Contains("the fox took")) foxTook++;
                var parts = overnight.Split(',');
                if (parts.Length > 1) hensStarved += int.Parse(parts[1].Trim().Split(' ')[1]);
                eggs += int.Parse(parts[0].Split(' ')[1]);
            }
            yield return null;
            yield return null;

            foreach (var pair in dying)
            {
                if (!pair.Value || !pair.Key.IsDead) continue;
                if (pair.Key.PestNights >= garden.pestNightsToDie) diedPest++;
                else diedDry++;
            }
            if (store.LastDelivery != lastDelivery)
            {
                lastDelivery = store.LastDelivery;
                deliveries++;
            }
            if (crockReadyDay >= 0 && clock.Day >= crockReadyDay)
            {
                int bowls = crockShreds / 8;
                float perShred = 1.4f;
                for (int i = 0; i < bowls; i++) larder.Add(new Food { Key = "Kraut", Name = "Sauerkraut", Value = 8 * perShred, Kept = true });
                krautBowls += bowls;
                crockShreds = 0;
                crockReadyDay = -1;
            }
            Days.Add(Row(day));
        }

        // ---- Reporting ----

        public const string DayHeader = "policy,seed,day,month,hunger,warmth,ate,harvested,fresh_food,kept_food,jars_sealed,jars_failed,eggs,hens,chicks,feed_scoops,firewood,died_dry,died_pest,frost_killed,deliveries,store_seed_packets,credit,spoiled";

        string Row(int day)
        {
            float fresh = 0f, kept = 0f;
            foreach (var f in larder) { if (f.Kept) kept += f.Value; else fresh += f.Value; }
            int chicks = 0;
            foreach (var hen in flock.Hens) if (hen.Alive && hen.IsChick) chicks++;
            int packets = 0;
            foreach (var w in store.Wares)
            {
                var prefab = w.recipe == null ? SaveManager.Catalog.Item(w.key) : null;
                if (prefab != null && prefab.GetComponent<SeedPacket>() != null) packets += w.stock;
            }
            return string.Join(",", Policy.Name, Seed, day, calendar.months[Month(day)].name, F(needs.hunger), F(needs.warmth), F(ateToday), F(harvestedToday),
                F(fresh), F(kept), jarsSealed, jarsFailed, eggs, flock.Alive - chicks, chicks, feedScoops, firewood, diedDry, diedPest, frostKilled,
                deliveries, packets, F(credit), F(spoiled));
        }

        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        public const string SummaryHeader = "policy,seed,first_harvest_day,empty_stomach_hours_by_month,ate_per_day,hungry_hours,starving_hours,collapses,first_collapse_day,cold_hours,freezing_hours,harvested,spoiled,food_left,sown,died_dry,died_pest,frost_killed,jars_sealed,jars_failed,kraut_bowls,eggs,eggs_sold,chicks_bought,hens_end,fox_took,hens_starved,firewood_burned,logs_bought,beans_bought,seed_packets_bought,jars_bought,food_sold,deliveries,out_of_stock,picked";

        public string Summary()
        {
            float left = 0f;
            foreach (var f in larder) left += f.Value;
            var crops = new StringBuilder();
            foreach (var pair in picked) crops.Append(pair.Key + " " + pair.Value + "; ");
            return string.Join(",", Policy.Name, Seed, firstHarvest, string.Join(" ", System.Linq.Enumerable.Take(emptyByMonth, calendar.months.Count)), F(eaten / calendar.DaysInYear), F(hungryHours), F(starvingHours), collapses, firstCollapse,
                F(coldHours), F(freezingHours), F(harvested), F(spoiled), F(left), sown, diedDry, diedPest, frostKilled, jarsSealed, jarsFailed,
                krautBowls, eggs, eggsSold, chicksBought, flock.Alive, foxTook, hensStarved, burned, logsBought, beansBought, seedPacketsBought, jarsBought, F(sold), deliveries, outOfStock, "\"" + crops + "\"");
        }

        public string HungryByMonth()
        {
            var parts = new List<string>();
            for (int m = 0; m < calendar.months.Count; m++) parts.Add(calendar.months[m].name + " " + hungryByMonth[m] + "/" + emptyByMonth[m]);
            return string.Join(", ", parts);
        }

        // Averages for each kind of player, with the worst year.
        public static string Table(List<Run> runs)
        {
            var text = new StringBuilder();
            var byPolicy = new Dictionary<string, List<Run>>();
            foreach (var r in runs)
            {
                if (!byPolicy.TryGetValue(r.Policy.Name, out var list)) byPolicy[r.Policy.Name] = list = new List<Run>();
                list.Add(r);
            }
            foreach (var pair in byPolicy)
            {
                var list = pair.Value;
                float Avg(System.Func<Run, float> f) { float s = 0f; foreach (var r in list) s += f(r); return s / list.Count; }
                float Max(System.Func<Run, float> f) { float m = float.MinValue; foreach (var r in list) m = Mathf.Max(m, f(r)); return m; }
                int Count(System.Func<Run, bool> f) { int n = 0; foreach (var r in list) if (f(r)) n++; return n; }
                text.AppendLine($"== {pair.Key} ({list.Count} years) ==");
                text.AppendLine($"  ate {Avg(r => r.eaten / r.calendar.DaysInYear):0}/day; hungry {Avg(r => r.hungryHours):0} h (worst {Max(r => r.hungryHours):0}); starving {Avg(r => r.starvingHours):0} h; collapsed in {Count(r => r.collapses > 0)} of {list.Count} years");
                text.AppendLine($"  cold {Avg(r => r.coldHours):0} h; freezing {Avg(r => r.freezingHours):0} h; firewood burned {Avg(r => r.burned):0}, logs bought {Avg(r => r.logsBought):0}; bought beans {Avg(r => r.beansBought):0.0}, seed packets {Avg(r => r.seedPacketsBought):0.0}, jars {Avg(r => r.jarsBought):0.0}; food sold {Avg(r => r.sold):0}");
                text.AppendLine($"  harvested {Avg(r => r.harvested):0} food, spoiled {Avg(r => r.spoiled):0}; sown {Avg(r => r.sown):0}, died dry {Avg(r => r.diedDry):0.0}, pests {Avg(r => r.diedPest):0.0}, frost {Avg(r => r.frostKilled):0.0}");
                text.AppendLine($"  jars sealed {Avg(r => r.jarsSealed):0.0}, failed {Avg(r => r.jarsFailed):0.0}; kraut bowls {Avg(r => r.krautBowls):0}; eggs {Avg(r => r.eggs):0}; chicks bought {Avg(r => r.chicksBought):0.0}; hens at year end {Avg(r => r.flock.Alive):0.0}; fox {Avg(r => r.foxTook):0.0}");
                text.AppendLine($"  store deliveries {Avg(r => r.deliveries):0.0}; wanted but out of stock {Avg(r => r.outOfStock):0}; food left at year end {Avg(r => { float s = 0f; foreach (var f in r.larder) s += f.Value; return s; }):0}");
                text.AppendLine($"  hungry/empty-stomach hours by month (first seed): {list[0].HungryByMonth()}; first harvest on day {Avg(r => r.firstHarvest):0.0}");
            }
            return text.ToString();
        }
    }
}
