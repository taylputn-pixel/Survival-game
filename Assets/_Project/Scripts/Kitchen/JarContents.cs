using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using GrandmasHouse.Stations;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // What's inside a glass jar: slices, garlic cloves or tomato wedges
    // packed in, a head of dill, peppercorns, lemon juice, and brine (or the
    // tomatoes' own juice), or salsa ladled in from the pot. Everything is
    // visible through the glass. What it's filled with decides what it is,
    // and so which label it gets: dill pickles need mostly cucumber and a
    // head of dill, salsa needs tomato, onion and jalapeño. Anything that
    // isn't one of Grandma's recipes is a "Don't eat!" jar.
    [RequireComponent(typeof(Pickup))]
    public class JarContents : MonoBehaviour, IItemState, IEmptyable, IPickupGuard, IInteractable, GrandmasHouse.Interaction.ISharedUse
    {
        [Tooltip("The bottom of the inside of the jar.")]
        [SerializeField] Transform inside;
        [SerializeField] float innerHeight = 0.12f;
        [SerializeField] float innerRadius = 0.036f;
        [Tooltip("A see-through cylinder that rises with the brine.")]
        [SerializeField] Transform brine;
        [SerializeField] int capacity = 22;
        [SerializeField] float sliceStep = 0.0052f;
        [SerializeField] int clovesPerLayer = 5;
        [SerializeField] float cloveLayer = 0.024f;
        [SerializeField] int wedgesPerLayer = 4;
        [SerializeField] float wedgeLayer = 0.028f;
        [Tooltip("Everything this jar can be put up as.")]
        [SerializeField] RecipeData[] recipes;
        [Tooltip("What it is when it isn't any of those: nobody wants to eat it.")]
        [SerializeField] RecipeData inedible;

        [Tooltip("The lid and ring, hidden until the jar is closed up.")]
        [SerializeField] Transform lid;
        [Tooltip("The paper label, put on by hand at the label box once the jar has sealed.")]
        [SerializeField] GameObject label;
        [SerializeField] float lidSeconds = 0.25f;
        [Header("Rebuilding a saved jar")]
        [SerializeField] Material chipMaterial;
        [Tooltip("Diameter and thickness of a pickle chip, in meters.")]
        [SerializeField] Vector2 chipSize = new Vector2(0.05f, 0.006f);
        [SerializeField] Material cloveMaterial;
        [Tooltip("Width, height and length of a clove, in meters.")]
        [SerializeField] Vector3 cloveSize = new Vector3(0.014f, 0.012f, 0.024f);
        [SerializeField] Material peppercornMaterial;
        [SerializeField] float peppercornSize = 0.005f;
        [SerializeField] Material wedgeMaterial;
        [Tooltip("Width, height and length of a tomato wedge, in meters.")]
        [SerializeField] Vector3 wedgeSize = new Vector3(0.034f, 0.026f, 0.036f);
        [Tooltip("Slices of other produce (onion, jalapeño). Anything else comes back as a pickle chip.")]
        [SerializeField] PieceLook[] sliceLooks;

        // How much of each kind is in it: slices and wedges by count, salsa
        // by share of the jar ("Tomato", "Onion", "Jalapeño").
        readonly Dictionary<string, float> amounts = new Dictionary<string, float>();

        public int SliceCount { get; private set; }
        public int CloveCount { get; private set; }
        public int WedgeCount { get; private set; }
        public bool HasLemon { get; private set; }
        // Filled with salsa from the pot (its level is BrineLevel).
        public bool HasSalsa { get; private set; }
        public int PeppercornCount { get; private set; }
        public bool HasLid { get; private set; }
        // Through the water bath and out to cool.
        public bool IsProcessed { get; private set; }
        public bool IsSealed { get; private set; }
        public bool HasDill { get; private set; }
        public bool IsLabeled { get; private set; }
        public bool CanLabel => IsSealed && !IsLabeled;
        // 0 is empty, 1 is right to the rim.
        public float BrineLevel { get; private set; }
        // How packed it is: slices and cloves both take up room.
        public float Fullness => SliceCount / (float)capacity + CloveCount / (float)CloveCapacity + WedgeCount / (float)WedgeCapacity;
        public bool IsFull => Fullness >= 0.999f;
        public int RoomForSlices => Mathf.Max(0, Mathf.FloorToInt((1f - Fullness) * capacity + 0.001f));
        public int RoomForCloves => Mathf.Max(0, Mathf.FloorToInt((1f - Fullness) * CloveCapacity + 0.001f));
        public int RoomForWedges => Mathf.Max(0, Mathf.FloorToInt((1f - Fullness) * WedgeCapacity + 0.001f));
        // Poured from a pot that wasn't salted, vinegared and boiled.
        public bool BadBatch { get; private set; }
        int WedgeCapacity => RecipeFor(RecipeData.Fill.Wedges) is RecipeData r ? Mathf.Max(1, r.wedgesPerJar) : 12;

        RecipeData RecipeFor(RecipeData.Fill fill)
        {
            foreach (var recipe in recipes)
            {
                if (recipe != null && recipe.fill == fill)
                {
                    return recipe;
                }
            }
            return null;
        }

        // Anything in it can have a lid put on; what it turns out to be is
        // up to what went in.
        public bool CanTakeLid => !HasLid && HasContents;

        // Brine goes in jars whose recipe uses it (or that are still empty);
        // never over something ladled from the pot.
        public bool TakesBrine => !HasSalsa;
        public bool IsInedible => Recipe != null && Recipe == inedible;
        // Salsa goes in a clean empty jar, or tops up one that has salsa.
        public bool TakesSalsa => HasSalsa || !HasContents;
        // Cloves that fill a jar packed with garlic alone, from the garlic recipe.
        int CloveCapacity
        {
            get
            {
                foreach (var recipe in recipes)
                {
                    if (recipe != null && recipe.fill == RecipeData.Fill.Cloves)
                    {
                        return Mathf.Max(1, recipe.clovesPerJar);
                    }
                }
                return 20;
            }
        }
        // What it's being put up as (JarRecipe has the rules).
        public RecipeData Recipe => JarRecipe.Resolve(recipes, inedible, new JarFacts
        {
            amounts = amounts, slices = SliceCount, cloves = CloveCount, wedges = WedgeCount, salsa = HasSalsa,
            dill = HasDill, lemon = HasLemon, badBatch = BadBatch, brine = BrineLevel,
        });

        // What it'll be called once it's labeled.
        public string Contents => Recipe != null ? Recipe.displayName : null;

        void Count(string kind, float amount)
        {
            amounts.TryGetValue(kind, out float had);
            amounts[kind] = had + amount;
        }
        public Vector3 Mouth => inside.position + inside.up * innerHeight;

        Vector3 lidSeat;
        Vector3 lidSize;

        void Awake()
        {
            lidSeat = lid.localPosition;
            lidSize = lid.localScale;
            ShowBrine();
            lid.gameObject.SetActive(false);
            label.SetActive(false);
        }

        // The lid drops on in one motion.
        public void PutOnLid()
        {
            if (HasLid)
            {
                return;
            }
            HasLid = true;
            StartCoroutine(LidDrop());
        }

        // Out of the canner: as it cools, the lid seals with a ping.
        public void CoolAndSeal(float seconds)
        {
            IsProcessed = true;
            StartCoroutine(Cooling(seconds));
        }

        IEnumerator LidDrop()
        {
            Vector3 seated = lid.localPosition;
            lid.gameObject.SetActive(true);
            for (float time = 0f; time < lidSeconds; time += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / lidSeconds);
                lid.localPosition = seated + Vector3.up * (1f - t) * 0.04f;
                yield return null;
            }
            lid.localPosition = seated;
            Sfx.Play(SoundId.LidClink, lid.position);
        }

        // Smoothed on from the label box: it unrolls around the front.
        public void PutOnLabel()
        {
            if (!CanLabel)
            {
                return;
            }
            IsLabeled = true;
            ShowLabel();
            StartCoroutine(Unroll());
        }

        IEnumerator Unroll()
        {
            label.SetActive(true);
            Sfx.Play(SoundId.Paper, label.transform.position, 0.8f, 0.1f);
            Transform t = label.transform;
            const float seconds = 0.3f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0.2f, 1f, time / seconds);
                t.localScale = new Vector3(k, 1f, k);
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        IEnumerator Cooling(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            // Ping: the center of the lid pulls down as it seals.
            Vector3 size = lid.localScale;
            for (float time = 0f; time < 0.15f; time += Time.deltaTime)
            {
                lid.localScale = Vector3.Scale(size, new Vector3(1f, 1f - Mathf.Sin(time / 0.15f * Mathf.PI) * 0.4f, 1f));
                yield return null;
            }
            lid.localScale = size;
            IsSealed = true;
            Sfx.Play(SoundId.LidPing, lid.position, 1f, 0.03f);
            if (Net.ItemNet.Runs(this)) DayCycle.YearTally.Add(DayCycle.YearTally.Count.Jar);
        }

        [System.Serializable]
        class State
        {
            public int slices;
            public int cloves;
            public int wedges;
            public bool lemon;
            public bool salsa;
            public bool badBatch;
            public List<string> kinds = new List<string>();
            public List<float> kindAmounts = new List<float>();
            public int peppercorns;
            public bool dill;
            public float brine;
            public bool lid;
            public bool processed;
            public bool sealedUp;
            public bool labeled;
        }

        public string Capture()
        {
            var state = new State
            {
                slices = SliceCount, cloves = CloveCount, wedges = WedgeCount, lemon = HasLemon, salsa = HasSalsa, badBatch = BadBatch, peppercorns = PeppercornCount, dill = HasDill, brine = BrineLevel, lid = HasLid, processed = IsProcessed, sealedUp = IsSealed, labeled = IsLabeled,
            };
            foreach (var kind in amounts)
            {
                state.kinds.Add(kind.Key);
                state.kindAmounts.Add(kind.Value);
            }
            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            amounts.Clear();
            if (state.kinds != null && state.kinds.Count > 0)
            {
                for (int i = 0; i < state.kinds.Count; i++) amounts[state.kinds[i]] = state.kindAmounts[i];
            }
            else
            {
                // Older saves: slices were all cucumber.
                if (state.slices > 0) amounts["Cucumber"] = state.slices;
                if (state.cloves > 0) amounts["Clove"] = state.cloves;
                if (state.wedges > 0) amounts["Tomato"] = state.wedges;
            }
            // Slices come back in their own kinds, in the jar's proportions.
            var sliceKinds = new List<string>();
            foreach (var kind in amounts)
            {
                if (state.salsa || kind.Key == "Clove" || kind.Key == "Tomato") continue;
                for (int n = 0; n < Mathf.RoundToInt(kind.Value); n++) sliceKinds.Add(kind.Key);
            }
            for (int i = 0; i < state.slices; i++)
            {
                string kind = i < sliceKinds.Count ? sliceKinds[i] : "Cucumber";
                Material look = chipMaterial;
                Vector2 size = chipSize;
                foreach (var known in sliceLooks)
                {
                    if (known.kind == kind)
                    {
                        look = known.material;
                        size = known.size;
                    }
                }
                var chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chip.name = kind + " Slice";
                Destroy(chip.GetComponent<Collider>());
                chip.GetComponent<Renderer>().sharedMaterial = look;
                chip.transform.localScale = new Vector3(size.x, size.y / 2f, size.x);
                chip.transform.SetParent(inside, false);
                NoShadows(chip.transform);
                Vector2 jitter = Random.insideUnitCircle * (innerRadius - 0.026f);
                chip.transform.SetLocalPositionAndRotation(
                    new Vector3(jitter.x, 0.004f + i * sliceStep, jitter.y),
                    Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f)));
            }
            SliceCount = state.slices;
            CloveCount = 0;
            for (int i = 0; i < state.cloves; i++)
            {
                var clove = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                clove.name = "Clove";
                Destroy(clove.GetComponent<Collider>());
                clove.GetComponent<Renderer>().sharedMaterial = cloveMaterial;
                clove.transform.localScale = cloveSize;
                clove.transform.SetParent(inside, false);
                NoShadows(clove.transform);
                CloveCount++;
                clove.transform.SetLocalPositionAndRotation(ClovePlace(), Random.rotation);
            }
            WedgeCount = 0;
            for (int i = 0; i < state.wedges; i++)
            {
                Transform wedge = MakeWedge(wedgeMaterial, wedgeSize);
                wedge.SetParent(inside, false);
                WedgeCount++;
                wedge.SetLocalPositionAndRotation(WedgePlace(), WedgeRotation());
            }
            HasLemon = state.lemon;
            HasSalsa = state.salsa;
            BadBatch = state.badBatch;
            PeppercornCount = state.peppercorns;
            for (int i = 0; i < state.peppercorns; i++)
            {
                var corn = MakePeppercorn(peppercornMaterial, peppercornSize);
                corn.SetParent(inside, false);
                corn.localPosition = PeppercornPlace();
            }
            HasDill = state.dill;
            BrineLevel = state.brine;
            HasLid = state.lid;
            IsProcessed = state.processed;
            IsSealed = state.sealedUp;
            IsLabeled = state.labeled;
            lid.gameObject.SetActive(HasLid);
            label.SetActive(IsLabeled);
            ShowLabel();
            ShowBrine();
        }

        // Small things packed inside the glass would shimmer with their own
        // shadows, so they don't cast any.
        static void NoShadows(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // An open jar on the packing towel is being packed there; once its lid
        // is on it can be picked straight up.
        //
        // In the canner, a jar lifts out only into empty hands, so looking into
        // the canner with a jar in hand still puts it in.
        public bool AllowPickup(PlayerHands hands)
        {
            if (GetComponentInParent<Canner>() != null)
            {
                return hands.IsEmpty;
            }
            return HasLid || GetComponentInParent<JarPackingStation>() == null;
        }

        // Carrying a handful of cut pieces, look at an open jar to drop them in.
        public bool CanInteract(PlayerHands hands)
        {
            var handful = Handful.Held(hands);
            return handful != null && !HasLid && !HasSalsa && RoomFor(handful) > 0;
        }

        public string PromptVerb(PlayerHands hands) => "Drop in";

        public void Interact(PlayerHands hands)
        {
            var handful = Handful.Held(hands);
            var pieces = handful.Take(RoomFor(handful));
            for (int i = 0; i < pieces.Count; i++)
            {
                if (handful.IsWedges) AddWedge(pieces[i], i * 0.1f);
                else if (handful.IsCloves) AddClove(pieces[i], i * 0.08f);
                else AddSlice(pieces[i], i * 0.06f);
            }
            handful.DoneWith(hands);
        }

        int RoomFor(Handful handful) => handful.IsWedges ? RoomForWedges : handful.IsCloves ? RoomForCloves : RoomForSlices;

        // Any jar can be poured out at the sink or thrown out in the trash,
        // sealed or not; it comes back clean and empty, to be packed again.
        public bool HasContents => SliceCount > 0 || CloveCount > 0 || WedgeCount > 0 || HasLemon || HasSalsa || PeppercornCount > 0 || HasDill || BrineLevel > 0f;

        public void Empty()
        {
            for (int i = inside.childCount - 1; i >= 0; i--)
            {
                Transform child = inside.GetChild(i);
                if (child != brine)
                {
                    Destroy(child.gameObject);
                }
            }
            SliceCount = 0;
            CloveCount = 0;
            WedgeCount = 0;
            HasLemon = false;
            HasSalsa = false;
            BadBatch = false;
            amounts.Clear();
            PeppercornCount = 0;
            HasDill = false;
            BrineLevel = 0f;
            ShowBrine();
            // The lid and label come off with it; the jar is ready to use again.
            StopAllCoroutines();
            HasLid = false;
            IsProcessed = false;
            IsSealed = false;
            IsLabeled = false;
            lid.gameObject.SetActive(false);
            lid.localPosition = lidSeat;
            lid.localScale = lidSize;
            label.SetActive(false);
            // Washed out, it's fresh to fill again.
            if (TryGetComponent(out Food.Perishable perishable))
            {
                perishable.Refresh();
            }
        }

        // A jar has no spout to stream from; it just empties. A lid pops off
        // as it tips.
        public void SetPouring(bool pouring)
        {
            if (pouring && HasLid)
            {
                lid.gameObject.SetActive(false);
                Sfx.Play(SoundId.LidClink, lid.position);
            }
        }

        // A slice drops in and settles on the pile inside, a little askew.
        public void AddSlice(Transform slice, float delay)
        {
            int index = SliceCount++;
            Count(RecipeData.KindOf(slice.name), 1f);
            float y = 0.004f + index * sliceStep;
            Vector2 jitter = Random.insideUnitCircle * (innerRadius - 0.026f);
            var end = new Vector3(jitter.x, y, jitter.y);
            var tilt = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
            slice.SetParent(inside, worldPositionStays: true);
            NoShadows(slice);
            StartCoroutine(Drop(slice, end, tilt, delay));
        }

        // A garlic clove drops in. Among slices it tucks in anywhere (past the
        // recipe's count it's just extra garlic); in a jar of garlic alone the
        // cloves stack up in layers.
        public void AddClove(Transform clove, float delay)
        {
            CloveCount++;
            Count("Clove", 1f);
            clove.SetParent(inside, worldPositionStays: true);
            NoShadows(clove);
            StartCoroutine(Drop(clove, ClovePlace(), Random.rotation, delay));
        }

        Vector3 ClovePlace()
        {
            if (SliceCount > 0)
            {
                // Somewhere low in the jar, among the slices.
                float top = Mathf.Max(0.01f, SliceCount * sliceStep);
                Vector2 jitter = Random.insideUnitCircle * (innerRadius - 0.012f);
                return new Vector3(jitter.x, Random.Range(0.006f, Mathf.Min(top, innerHeight - 0.01f)), jitter.y);
            }
            // Around the jar in rings, one layer on top of the last.
            int index = CloveCount - 1;
            int layer = index / clovesPerLayer;
            float angle = (index % clovesPerLayer) * Mathf.PI * 2f / clovesPerLayer + layer * 0.5f;
            float ring = index % clovesPerLayer == clovesPerLayer - 1 ? 0f : innerRadius - 0.014f;
            return new Vector3(Mathf.Cos(angle) * ring, 0.008f + layer * cloveLayer, Mathf.Sin(angle) * ring);
        }

        // A tomato wedge is pressed in; the juice it gives up rises with the
        // packing, so a full jar is full of its own juice.
        public void AddWedge(Transform wedge, float delay)
        {
            WedgeCount++;
            Count("Tomato", 1f);
            wedge.SetParent(inside, worldPositionStays: true);
            NoShadows(wedge);
            StartCoroutine(Drop(wedge, WedgePlace(), WedgeRotation(), delay));
            if (Recipe != null && !Recipe.usesBrine)
            {
                BrineLevel = Mathf.Max(BrineLevel, Mathf.Min(Fullness, 1f) * Recipe.brineLine);
                ShowBrine();
            }
        }

        // Wedges go round the jar in layers, skin side out.
        Vector3 WedgePlace()
        {
            int index = WedgeCount - 1;
            int layer = index / wedgesPerLayer;
            float angle = (index % wedgesPerLayer) * Mathf.PI * 2f / wedgesPerLayer + layer * 0.6f;
            float ring = innerRadius - wedgeSize.x * 0.45f;
            return new Vector3(Mathf.Cos(angle) * ring, wedgeSize.y * 0.5f + layer * wedgeLayer, Mathf.Sin(angle) * ring);
        }

        Quaternion WedgeRotation()
        {
            int index = WedgeCount - 1;
            int layer = index / wedgesPerLayer;
            float angle = (index % wedgesPerLayer) * 360f / wedgesPerLayer + layer * 0.6f * Mathf.Rad2Deg;
            return Quaternion.Euler(0f, 90f - angle, Random.Range(-10f, 10f));
        }

        public static Transform MakeWedge(Material material, Vector3 size)
        {
            var wedge = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wedge.name = "Tomato Wedge";
            Destroy(wedge.GetComponent<Collider>());
            var look = wedge.GetComponent<Renderer>();
            look.sharedMaterial = material;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            wedge.transform.localScale = size;
            return wedge.transform;
        }

        // A spoonful of lemon juice.
        public void AddLemon()
        {
            HasLemon = true;
        }

        // A pinch of peppercorns falls in and settles among what's packed.
        public void AddPeppercorns(System.Collections.Generic.List<Transform> corns)
        {
            for (int i = 0; i < corns.Count; i++)
            {
                PeppercornCount++;
                corns[i].SetParent(inside, worldPositionStays: true);
                StartCoroutine(Drop(corns[i], PeppercornPlace(), Random.rotation, i * 0.03f));
            }
        }

        Vector3 PeppercornPlace()
        {
            float top = Mathf.Clamp(Fullness * innerHeight, 0.01f, innerHeight - 0.01f);
            Vector2 jitter = Random.insideUnitCircle * (innerRadius - 0.006f);
            return new Vector3(jitter.x, Random.Range(0.004f, top), jitter.y);
        }

        public static Transform MakePeppercorn(Material material, float size)
        {
            var corn = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            corn.name = "Peppercorn";
            Destroy(corn.GetComponent<Collider>());
            var look = corn.GetComponent<Renderer>();
            look.sharedMaterial = material;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            corn.transform.localScale = Vector3.one * size;
            return corn.transform;
        }

        // The label shows what's inside.
        void ShowLabel()
        {
            RecipeData recipe = Recipe;
            if (recipe != null && recipe.label != null)
            {
                label.GetComponent<Renderer>().sharedMaterial = recipe.label;
            }
        }

        // The dill goes in standing up along the side of the jar.
        public void AddDill(Pickup dill)
        {
            HasDill = true;
            Sfx.Play(SoundId.DillRustle, Mouth);
            dill.AttachTo(inside, new Vector3(innerRadius * 0.4f, 0.01f, 0f), Quaternion.Euler(0f, 0f, 8f), 0.35f, collidersOn: false);
            dill.transform.localScale = Vector3.Scale(dill.transform.localScale, new Vector3(0.6f, 0.9f, 0.6f));
            NoShadows(dill.transform);
        }

        // Ladled in from the pot: what's in it comes along in the pot's proportions.
        public void AddSalsa(float amount, IDictionary<string, float> shares, bool doneRight)
        {
            HasSalsa = true;
            BadBatch |= !doneRight;
            float before = BrineLevel;
            AddBrine(amount);
            float added = BrineLevel - before;
            foreach (var share in shares)
            {
                Count(share.Key, added * share.Value);
            }
            ShowBrine();
        }

        // Brine from the pot; brine that wasn't made right spoils the jar.
        public void AddBrine(float amount, bool doneRight)
        {
            BadBatch |= !doneRight;
            AddBrine(amount);
        }

        public void AddBrine(float amount)
        {
            BrineLevel = Mathf.Clamp01(BrineLevel + amount);
            ShowBrine();
        }

        Material brineLook;

        void ShowBrine()
        {
            // Tomatoes show their own red juice instead of clear brine.
            var look = brine.GetComponent<Renderer>();
            if (brineLook == null) brineLook = look.sharedMaterial;
            RecipeData recipe = Recipe;
            // A jar that went wrong only looks murky if it came out of the pot
            // that way; brined slices look like brine until they're labeled.
            bool show = recipe != null && recipe.liquid != null && (recipe != inedible || HasSalsa);
            look.sharedMaterial = show ? recipe.liquid : brineLook;
            brine.gameObject.SetActive(BrineLevel > 0f);
            float height = BrineLevel * innerHeight;
            brine.localScale = new Vector3(innerRadius * 2f, height / 2f, innerRadius * 2f);
            brine.localPosition = Vector3.up * height / 2f;
        }

        IEnumerator Drop(Transform slice, Vector3 end, Quaternion endRotation, float delay)
        {
            yield return new WaitForSeconds(delay);
            Vector3 start = slice.localPosition;
            Quaternion startRotation = slice.localRotation;
            // Up over the rim, then down inside.
            Vector3 overRim = new Vector3(end.x, innerHeight + 0.04f, end.z);
            const float seconds = 0.4f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float t = time / seconds;
                Vector3 a = Vector3.Lerp(start, overRim, t);
                Vector3 b = Vector3.Lerp(overRim, end, t);
                slice.SetLocalPositionAndRotation(Vector3.Lerp(a, b, t), Quaternion.Slerp(startRotation, endRotation, t));
                yield return null;
            }
            slice.SetLocalPositionAndRotation(end, endRotation);
            Sfx.Play(SoundId.SliceDrop, slice.position, 1f, 0.15f);
        }
    }
}
