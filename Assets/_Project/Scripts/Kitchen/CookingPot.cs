using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Kitchen
{
    // Grandma's enamel cooking pot. Brine is made in it all by hand with no
    // station: fill it with water at the sink, bring the vinegar and the
    // salt box from the pantry and pour and shake them in (look at the pot
    // while holding them), set it on a burner and turn the knob. Once it has
    // boiled the brine stays ready, so the pot can move to the hot pad by
    // the packing towel.
    //
    // Salsa is cooked in it too: carry the pot to the cutting board and
    // scrape the chopped tomatoes, onion and peppers in, add a splash of
    // vinegar and a scoop of salt, and bring it to a boil. It's ladled into
    // jars at the towel the same way brine is poured.
    //
    // Soup and stew are the same with water instead of vinegar: potatoes,
    // carrots, squash, onions, whatever's chopped. Hold an empty bowl and
    // look at the pot to ladle some out. Cooked, it feeds far more than the
    // same vegetables eaten raw (MealSettings).
    [RequireComponent(typeof(Cookware))]
    public class CookingPot : MonoBehaviour, IInteractable, IItemState, IEmptyable, IFillable, GrandmasHouse.Interaction.ISharedUse
    {
        [SerializeField] RecipeData recipe;
        [Tooltip("What chopped vegetables cooked in the pot become.")]
        [SerializeField] RecipeData salsaRecipe;
        [Tooltip("The brine's surface; it rises as you pour and sits on the bottom when empty.")]
        [SerializeField] Transform surface;
        [SerializeField] Renderer surfaceRenderer;
        [Tooltip("The inside bottom of the pot.")]
        [SerializeField] Transform bottom;
        [SerializeField] float innerHeight = 0.12f;
        [Tooltip("The pot's own lip and stream, used when pouring brine into a jar.")]
        [SerializeField] Transform lip;
        [SerializeField] ParticleSystem stream;
        [SerializeField] ParticleSystem steam;

        [Header("Feel")]
        [Tooltip("How fast the pot fills while pouring vinegar (share of the pot per second).")]
        [SerializeField] float pourRate = 0.22f;
        [SerializeField] float pourTilt = 75f;
        [SerializeField] float pourHeight = 0.12f;
        [SerializeField] float followSharpness = 10f;
        [SerializeField] Color waterColor = new Color(0.78f, 0.88f, 0.92f, 0.5f);
        [SerializeField] Color brineColor = new Color(0.86f, 0.83f, 0.5f, 0.6f);

        [Header("Salsa")]
        [Tooltip("How far from the middle chopped pieces settle, in meters.")]
        [SerializeField] float innerRadius = 0.08f;
        [SerializeField] Color rawSalsaColor = new Color(0.85f, 0.35f, 0.25f, 0.35f);
        [SerializeField] Color cookedSalsaColor = new Color(0.72f, 0.2f, 0.12f, 0.7f);
        [Tooltip("For rebuilding saved pieces.")]
        [SerializeField] Material tomatoPiece;
        [SerializeField] Material onionPiece;
        [SerializeField] Material pepperPiece;
        [SerializeField] Material garlicPiece;

        [Header("Soup")]
        [SerializeField] MealSettings meals;
        [SerializeField] Material potatoPiece;
        [SerializeField] Material carrotPiece;
        [SerializeField] Material squashPiece;
        [SerializeField] Material eggPiece;
        [SerializeField] Color rawSoupColor = new Color(0.82f, 0.78f, 0.62f, 0.45f);
        [SerializeField] Color cookedSoupColor = new Color(0.86f, 0.66f, 0.32f, 0.85f);

        static readonly List<CookingPot> all = new List<CookingPot>();
        readonly List<Transform> pieces = new List<Transform>();

        Cookware cookware;
        PlayerHands player;
        InputAction interactAction;
        MaterialPropertyBlock block;
        bool busy;
        SoundLoop pourSound;
        SoundLoop simmerSound;
        SoundLoop boilSound;

        public float Vinegar { get; private set; }
        public float Water { get; private set; }
        public int Salt { get; private set; }
        // 0 is cold, 1 is a rolling boil.
        public float Heat { get; private set; }
        public float Liquid => Vinegar + Water;
        // A full batch of chopped vegetables comes most of the way up the
        // pot, leaving room for the vinegar.
        const float ChoppedShare = 0.85f;
        // Chopped vegetables, as a share of a full pot.
        public float Chopped => salsaRecipe != null ? pieces.Count / (float)Mathf.Max(1, salsaRecipe.piecesPerPot) * ChoppedShare : 0f;
        // Everything in the pot, liquid and chopped.
        public float Fill => Liquid + Chopped;
        public bool IsFull => Fill >= 1f;
        public bool HasSalsa => pieces.Count > 0;
        // Chopped vegetables in water, not vinegar: soup, for bowls not jars.
        public bool IsSoup => pieces.Count > 0 && Water > Vinegar;
        public RecipeData SalsaRecipe => salsaRecipe;
        RecipeData Cooking => HasSalsa ? salsaRecipe : recipe;
        // Something in a pot that's set down somewhere (not in your hands) can
        // be poured or ladled out, whatever state it's in.
        public bool IsReady => !InHands && Fill > 0.02f;
        // Made the way it should be: vinegar and salt in, brought to a boil.
        // Brine or salsa that isn't goes into the jar anyway, and the jar
        // knows it didn't come out.
        public bool DoneRight => Vinegar > 0f && Salt > 0 && Heat >= 1f;
        public Transform Lip => lip;
        // In someone's hands (anyone's, in a shared game).
        bool InHands
        {
            get
            {
                if (TryGetComponent(out Net.ItemNet net) && net.IsSpawned) return net.Holder != Net.ItemNet.NoOne;
                if (player == null) player = FindAnyObjectByType<PlayerHands>();
                return player != null && player.IsHolding(cookware.Pickup);
            }
        }

        // A pot with something in it set down somewhere, if there is one.
        public static CookingPot ReadyPot()
        {
            foreach (var pot in all)
            {
                if (pot.IsReady)
                {
                    return pot;
                }
            }
            return null;
        }

        void Awake()
        {
            cookware = GetComponent<Cookware>();
            block = new MaterialPropertyBlock();
            player = FindAnyObjectByType<PlayerHands>();
            interactAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            pourSound = Sfx.Loop(SoundId.Pour, lip);
            simmerSound = Sfx.Loop(SoundId.Simmer, transform);
            boilSound = Sfx.Loop(SoundId.Boil, transform);
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ShowLiquid();
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        // Holding the vinegar or the salt box, look at the pot to add it.
        public bool CanInteract(PlayerHands hands)
        {
            if (busy || hands.IsEmpty)
            {
                return false;
            }
            if (Handful.Held(hands) != null)
            {
                return IsSetDown && RoomForPieces > 0;
            }
            if (hands.Held.TryGetComponent(out Food.Bowl bowl))
            {
                return bowl.IsEmpty && IsSoup && IsSetDown;
            }
            if (hands.Held.Category == ItemCategory.Egg || IsDried(hands.Held))
            {
                return IsSetDown && Water > 0f && RoomForPieces > 0;
            }
            var seasoning = hands.Held.GetComponent<Seasoning>();
            if (seasoning == null)
            {
                return false;
            }
            // Salt goes into something already in the pot.
            if (seasoning.IsEmpty) return false;
            return seasoning.Kind == Seasoning.Type.Salt ? Fill > 0f : !IsFull;
        }

        public string PromptVerb(PlayerHands hands)
        {
            if (Handful.Held(hands) != null)
            {
                return "Drop in pot";
            }
            if (hands.Held.GetComponent<Food.Bowl>() != null)
            {
                return "Ladle into bowl";
            }
            if (hands.Held.Category == ItemCategory.Egg)
            {
                return "Put the egg in the pot";
            }
            if (IsDried(hands.Held))
            {
                return "Add to the pot";
            }
            return hands.Held.GetComponent<Seasoning>().Kind == Seasoning.Type.Salt ? "Add a scoop of salt" : "Hold to pour vinegar";
        }

        public void Interact(PlayerHands hands)
        {
            if (Handful.Held(hands) is Handful handful)
            {
                AddPieces(handful.Take(RoomForPieces));
                handful.DoneWith(hands);
                return;
            }
            if (hands.Held.TryGetComponent(out Food.Bowl bowl))
            {
                Serve(bowl);
                return;
            }
            if (hands.Held.Category == ItemCategory.Egg)
            {
                Pickup egg = hands.TakeHeld();
                AddEgg();
                Net.ItemNet.Remove(egg.gameObject);
                return;
            }
            if (IsDried(hands.Held))
            {
                Pickup dried = hands.TakeHeld();
                AddDried(dried);
                Net.ItemNet.Remove(dried.gameObject);
                return;
            }
            var seasoning = hands.Held.GetComponent<Seasoning>();
            StartCoroutine(seasoning.Kind == Seasoning.Type.Salt ? ShakeSalt(hands, seasoning) : PourVinegar(hands, seasoning));
        }

        // The bottle leaves your hand, swings over the pot and tips while you
        // hold the button; let go and it comes back.
        IEnumerator PourVinegar(PlayerHands hands, Seasoning bottle)
        {
            busy = true;
            hands.Controller.InputEnabled = false;
            Pickup item = hands.TakeHeld();
            Transform t = item.transform;
            Vector3 restPosition = t.position;
            Quaternion restRotation = t.rotation;
            float tilt = 0f;
            bool pouring = true;
            while (pouring || tilt > 0f || (t.position - restPosition).sqrMagnitude > 0.000004f)
            {
                pouring = pouring && interactAction.IsPressed() && !IsFull && !bottle.IsEmpty;
                tilt = Mathf.MoveTowards(tilt, pouring ? pourTilt : 0f, pourTilt / 0.4f * Time.deltaTime);
                Pouring.Follow(t, bottle.Lip, restPosition, restRotation, bottom.position, tilt, pourTilt, pourHeight + innerHeight, followSharpness);
                bool tipped = pouring && tilt >= pourTilt * 0.8f;
                bottle.SetPouring(tipped);
                if (tipped)
                {
                    float amount = bottle.Use(Mathf.Min(pourRate * Time.deltaTime, 1f - Fill));
                    Vinegar += amount;
                    Cool(amount);
                    ShowLiquid();
                }
                yield return null;
            }
            bottle.SetPouring(false);
            hands.Give(item);
            hands.Controller.InputEnabled = true;
            busy = false;
            LogBrine();
        }

        // The box tips over the pot for a moment and shakes in one scoop.
        IEnumerator ShakeSalt(PlayerHands hands, Seasoning box)
        {
            busy = true;
            hands.Controller.InputEnabled = false;
            Pickup item = hands.TakeHeld();
            Transform t = item.transform;
            Vector3 restPosition = t.position;
            Quaternion restRotation = t.rotation;
            const float seconds = 0.7f;
            bool added = false;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float phase = time / seconds;
                float tilt = Mathf.Sin(phase * Mathf.PI) * pourTilt;
                Pouring.Follow(t, box.Lip, restPosition, restRotation, bottom.position, tilt, pourTilt, pourHeight + innerHeight, followSharpness * 1.5f);
                box.SetPouring(tilt > pourTilt * 0.6f);
                if (!added && phase > 0.45f)
                {
                    added = true;
                    if (box.Use(1f) > 0f) Salt++;
                    Sfx.Play(SoundId.SaltShake, t.position);
                }
                yield return null;
            }
            box.SetPouring(false);
            hands.Give(item);
            hands.Controller.InputEnabled = true;
            busy = false;
            LogBrine();
        }

        public void DoneFilling() => LogBrine();

        // Pieces in the pot, by kind (Potato, Carrot, Winter, Onion...).
        public Dictionary<string, int> Counts()
        {
            var counts = new Dictionary<string, int>();
            foreach (var piece in pieces)
            {
                if (piece == null) continue;
                string kind = RecipeData.KindOf(piece.name);
                counts.TryGetValue(kind, out int had);
                counts[kind] = had + 1;
            }
            return counts;
        }

        bool Cooked => Heat >= 1f;

        static bool IsDried(Pickup item) => item.TryGetComponent(out Food.Perishable food) && food.Cured;

        // Dried, smoked or salted food goes in whole, no chopping: it plumps up in the pot as
        // the pieces it would have been cut into.
        public void AddDried(Pickup dried)
        {
            int count = dried.DisplayName switch
            {
                "Potato" => 8, "Winter Squash" => 12, "Carrot" => 6, "Onion" => 6, "Tomato" => 4,
                "Cucumber" => 8, "Jalapeño" => 3, "Chicken" => 6, _ => 2,
            };
            var look = dried.GetComponent<Renderer>();
            RebuildPieces(dried.DisplayName + " Piece", look != null ? look.sharedMaterial : potatoPiece, Mathf.Min(count, RoomForPieces), new Vector3(0.03f, 0.008f, 0.03f));
            Sfx.Play(SoundId.SliceDrop, transform.position, 1f, 0.1f);
            ShowLiquid();
        }

        // An egg lowered into the water, to cook with whatever else is in.
        public void AddEgg()
        {
            RebuildPieces("Egg", eggPiece, 1, new Vector3(0.042f, 0.055f, 0.042f));
            Cool(0.02f);
            Sfx.Play(SoundId.SliceDrop, transform.position, 0.6f, 0.1f);
            ShowLiquid();
        }

        // What the whole pot would feed, if it were all eaten.
        public float PotFood => meals != null ? meals.PotFood(Counts(), Cooked, Salt > 0) : 0f;

        // One bowl's worth, ladled out.
        public void Serve(Food.Bowl bowl)
        {
            float fill = Fill;
            if (fill <= 0f || meals == null)
            {
                return;
            }
            float share = Mathf.Min(meals.bowlShare, fill) / fill;
            var counts = Counts();
            bowl.Fill(MealSettings.DishName(counts, Cooked), PotFood * share, Cooked ? cookedSoupColor : rawSoupColor);
            Sfx.Play(SoundId.Pour, transform.position, 0.7f, 0.1f);
            Use(meals.bowlShare);
            if (TryGetComponent(out Net.ItemNet net)) net.Share();
        }

        // Cold water from the tap, held under the faucet at the sink.
        public void AddWater(float amount)
        {
            amount = Mathf.Min(amount, 1f - Fill);
            Water += amount;
            Cool(amount);
            ShowLiquid();
        }

        // Anything cold poured in takes the heat down a little.
        void Cool(float amount) => Heat = Mathf.Max(Heat - amount * 0.5f, 0f);

        public bool HasContents => Fill > 0f || Salt > 0;

        // Poured out at the sink, to start over.
        public void Empty()
        {
            Vinegar = Water = 0f;
            Salt = 0;
            Heat = 0f;
            ClearPieces();
            ShowLiquid();
        }

        // Pouring brine or ladling salsa into a jar uses some up.
        public void Use(float share)
        {
            float fill = Fill;
            if (fill <= 0f)
            {
                return;
            }
            float keep = Mathf.Max(fill - share, 0f) / fill;
            Vinegar *= keep;
            Water *= keep;
            int keepPieces = Mathf.CeilToInt(pieces.Count * keep - 0.001f);
            while (pieces.Count > keepPieces)
            {
                Destroy(pieces[pieces.Count - 1].gameObject);
                pieces.RemoveAt(pieces.Count - 1);
            }
            if (Fill <= 0.02f)
            {
                Vinegar = Water = 0f;
                Salt = 0;
                ClearPieces();
            }
            ShowLiquid();
        }

        void ClearPieces()
        {
            foreach (var piece in pieces)
            {
                if (piece != null) Destroy(piece.gameObject);
            }
            pieces.Clear();
        }

        // Room for this many more chopped pieces.
        public int RoomForPieces => salsaRecipe != null ? Mathf.Max(0, Mathf.FloorToInt((1f - Fill) / ChoppedShare * salsaRecipe.piecesPerPot + 0.001f)) : 0;

        // Chopped vegetables scraped in off the cutting board tumble down
        // and settle in a loose heap on the bottom.
        public void AddPieces(List<Transform> added)
        {
            for (int i = 0; i < added.Count; i++)
            {
                Transform piece = added[i];
                pieces.Add(piece);
                piece.SetParent(bottom, worldPositionStays: true);
                StartCoroutine(Settle(piece, PiecePlace(pieces.Count - 1), Random.rotation, i * 0.02f));
            }
            // Cold vegetables take the heat down.
            Cool(added.Count / (float)Mathf.Max(1, salsaRecipe.piecesPerPot) * ChoppedShare);
            Sfx.Play(SoundId.SliceDrop, transform.position, 1f, 0.1f);
            ShowLiquid();
            LogBrine();
        }

        Vector3 PiecePlace(int index)
        {
            Vector2 spot = Random.insideUnitCircle * innerRadius;
            float height = Mathf.Min((index + 1f) / Mathf.Max(1, salsaRecipe.piecesPerPot), 1f) * ChoppedShare * innerHeight;
            return new Vector3(spot.x, Random.Range(0.005f, Mathf.Max(0.01f, height)), spot.y);
        }

        IEnumerator Settle(Transform piece, Vector3 end, Quaternion endRotation, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (piece == null)
            {
                yield break;
            }
            Vector3 start = piece.localPosition;
            Quaternion startRotation = piece.localRotation;
            Vector3 over = new Vector3(end.x, innerHeight + 0.05f, end.z);
            const float seconds = 0.35f;
            for (float time = 0f; time < seconds && piece != null; time += Time.deltaTime)
            {
                float t = time / seconds;
                piece.SetLocalPositionAndRotation(Vector3.Lerp(Vector3.Lerp(start, over, t), Vector3.Lerp(over, end, t), t), Quaternion.Slerp(startRotation, endRotation, t));
                yield return null;
            }
            if (piece != null)
            {
                piece.SetLocalPositionAndRotation(end, endRotation);
            }
        }

        // What's in it, kind by kind, as shares of the chopped pieces.
        public Dictionary<string, float> Composition()
        {
            var shares = new Dictionary<string, float>();
            foreach (var piece in pieces)
            {
                if (piece == null) continue;
                string kind = RecipeData.KindOf(piece.name);
                shares.TryGetValue(kind, out float had);
                shares[kind] = had + 1f / pieces.Count;
            }
            return shares;
        }

        // Set down somewhere, not in your hands.
        public bool IsSetDown => !InHands;


        public static IEnumerable<CookingPot> All => all;

        static int CountOf(List<Transform> list, string prefix)
        {
            int count = 0;
            foreach (var piece in list)
            {
                if (piece != null && piece.name.StartsWith(prefix)) count++;
            }
            return count;
        }

        public void SetPouring(bool pouring)
        {
            pourSound.Set(pouring);
            if (pouring && !stream.isEmitting) stream.Play();
            else if (!pouring && stream.isEmitting) stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // In a shared game only the computer running the pot heats it; the
        // others show what it sends.
        void Update()
        {
            if (Net.ItemNet.Runs(this)) Warm();
        }

        // On a lit burner the brine heats toward a boil. Off the heat, brine
        // that has boiled stays ready (Cozy); anything short of a boil cools slowly.
        void Warm()
        {
            float before = Heat;
            float heatSeconds = Cooking.heatSeconds;
            if (cookware.IsHot && Fill > 0f)
            {
                Heat = Mathf.Min(Heat + Time.deltaTime / (heatSeconds * Mathf.Max(Fill, 0.25f)), 1f);
                if (HasSalsa && before < 0.6f != Heat < 0.6f)
                {
                    ShowLiquid();
                }
            }
            else if (Heat < 1f || Fill <= 0f)
            {
                Heat = Mathf.Max(Heat - Time.deltaTime / (heatSeconds * 6f), 0f);
            }

            // It starts to simmer as it warms, then rolls into a boil, but only over the flame.
            bool overHeat = cookware.IsHot && Fill > 0f;
            simmerSound.Set(overHeat ? Mathf.InverseLerp(0.3f, 0.95f, Heat) : 0f);
            boilSound.Set(overHeat && Heat >= 1f);

            bool steaming = Heat > 0.5f && Fill > 0f;
            if (steaming && !steam.isEmitting) steam.Play();
            else if (!steaming && steam.isEmitting) steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (overHeat && Heat >= 1f)
            {
                // A rolling boil: the surface trembles.
                surface.localPosition = SurfacePosition() + Vector3.up * Mathf.Sin(Time.time * 37f) * 0.0015f;
            }
            else if (before >= 1f && surface.localPosition != SurfacePosition())
            {
                ShowLiquid();
            }
        }

        Vector3 SurfacePosition() => bottom.localPosition + Vector3.up * Mathf.Max(Fill, 0.001f) * innerHeight;

        // The water's surface, where a faucet's stream lands.
        public Vector3 SurfaceWorld => transform.TransformPoint(SurfacePosition());

        void ShowLiquid()
        {
            surface.gameObject.SetActive(Fill > 0f);
            surface.localPosition = SurfacePosition();
            float vinegarShare = Liquid > 0f ? Vinegar / Liquid : 0f;
            Color color = Color.Lerp(waterColor, brineColor, Mathf.Clamp01(vinegarShare * 2f));
            if (IsSoup)
            {
                // Broth turns golden as it cooks.
                color = Heat >= 0.6f ? cookedSoupColor : rawSoupColor;
            }
            else if (HasSalsa)
            {
                // Chopped vegetables show through at first and cook down red.
                color = Heat >= 0.6f ? cookedSalsaColor : rawSalsaColor;
            }
            surfaceRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            surfaceRenderer.SetPropertyBlock(block);
        }

        public void LogBrine()
        {
            // Not shown to the player yet; recorded for tuning quality later.
            if (IsSoup)
            {
                Debug.Log($"Soup: {Fill:P0} full, {pieces.Count} pieces, water {Water:P0}, {Salt} scoops of salt, feeds {PotFood:F0} in all");
                return;
            }
            if (HasSalsa)
            {
                Debug.Log($"Salsa: {Fill:P0} full, {CountOf(pieces, "Tomato")} tomato pieces, {CountOf(pieces, "Onion")} onion, {CountOf(pieces, "Jalape")} jalapeño, {CountOf(pieces, "Clove")} garlic, vinegar {Vinegar:P0}, {Salt} scoops of salt (recipe {salsaRecipe.saltScoops})");
                return;
            }
            float share = Liquid > 0f ? Vinegar / Liquid : 0f;
            Debug.Log($"Brine: {Liquid:P0} full, {share:P0} vinegar (recipe {recipe.vinegarShare:P0}), {Salt} scoops of salt (recipe {recipe.saltScoops})");
        }

        [System.Serializable]
        class State
        {
            public float vinegar;
            public float water;
            public int salt;
            public float heat;
            public int tomato;
            public int onion;
            public int pepper;
            public int garlic;
            public int potato;
            public int carrot;
            public int squash;
            public int egg;
        }

        public string Capture() => JsonUtility.ToJson(new State
        {
            vinegar = Vinegar, water = Water, salt = Salt, heat = Heat,
            tomato = CountOf(pieces, "Tomato"), onion = CountOf(pieces, "Onion"), pepper = CountOf(pieces, "Jalape"), garlic = CountOf(pieces, "Clove"),
            potato = CountOf(pieces, "Potato"), carrot = CountOf(pieces, "Carrot"), squash = CountOf(pieces, "Winter"), egg = CountOf(pieces, "Egg"),
        });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Vinegar = state.vinegar;
            Water = state.water;
            Salt = state.salt;
            Heat = state.heat;
            ClearPieces();
            RebuildPieces("Tomato Wedge", tomatoPiece, state.tomato, new Vector3(0.034f, 0.026f, 0.036f));
            RebuildPieces("Onion Slice", onionPiece, state.onion, new Vector3(0.05f, 0.004f, 0.05f));
            RebuildPieces("Jalapeño Slice", pepperPiece, state.pepper, new Vector3(0.022f, 0.004f, 0.022f));
            RebuildPieces("Clove", garlicPiece, state.garlic, new Vector3(0.014f, 0.012f, 0.024f));
            RebuildPieces("Potato Slice", potatoPiece, state.potato, new Vector3(0.05f, 0.006f, 0.04f));
            RebuildPieces("Carrot Slice", carrotPiece, state.carrot, new Vector3(0.026f, 0.005f, 0.026f));
            RebuildPieces("Winter Squash Slice", squashPiece, state.squash, new Vector3(0.06f, 0.01f, 0.06f));
            RebuildPieces("Egg", eggPiece, state.egg, new Vector3(0.042f, 0.055f, 0.042f));
            ShowLiquid();
        }

        void RebuildPieces(string pieceName, Material material, int count, Vector3 size)
        {
            for (int i = 0; i < count; i++)
            {
                var piece = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                piece.name = pieceName;
                Destroy(piece.GetComponent<Collider>());
                piece.GetComponent<Renderer>().sharedMaterial = material;
                piece.transform.SetParent(bottom, false);
                piece.transform.localScale = size;
                pieces.Add(piece.transform);
                piece.transform.SetLocalPositionAndRotation(PiecePlace(pieces.Count - 1), Random.rotation);
            }
        }
    }
}
