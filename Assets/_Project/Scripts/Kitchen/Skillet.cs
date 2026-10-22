using System;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Grandma's cast-iron skillet, for a quick meal of one or two plates:
    // eggs cracked in, sliced potatoes, onion, squash or chicken off the
    // cutting board (carry the skillet to the board and scrape them in, or
    // drop in a handful), a pinch of salt. Set it on a lit burner and it
    // cooks in a little while; leave it on much longer and it burns. You
    // tell by looking: egg whites go from clear-gray to white with golden
    // edges and the yolk turns from deep orange to yellow; potatoes and the
    // rest go golden. Past done it steams, then smokes and darkens, then
    // it's burnt. Hold an empty plate and look at it to serve. Food works like the pot's
    // (MealSettings): each piece's worth, a bonus for variety, salt helps,
    // and frying makes it worth a little less per piece than soup.
    [RequireComponent(typeof(Cookware))]
    public class Skillet : MonoBehaviour, IInteractable, IItemState, IEmptyable, ISharedUse
    {
        [SerializeField] MealSettings meals;
        [Tooltip("The pan's floor, where food lies (local up is up).")]
        [SerializeField] Transform bottom;
        [SerializeField] float panRadius = 0.1f;
        [Tooltip("Most pieces it holds (an egg counts as two).")]
        [SerializeField] int capacity = 24;
        [Tooltip("Real seconds on a lit burner to fry, and to burn.")]
        [SerializeField] float fryAfter = 12f;
        [SerializeField] float burnAfter = 40f;
        [Tooltip("Food per piece compared with the same pieces in soup.")]
        [SerializeField] float fryShare = 0.85f;
        [Tooltip("What burnt food is still worth.")]
        [SerializeField] float burntShare = 0.4f;
        [Tooltip("More food than this serves two plates.")]
        [SerializeField] float onePlate = 30f;
        [Serializable] public class Look { public string kind; public Material material; }
        [SerializeField] Look[] looks;
        [SerializeField] Material eggWhite;
        [SerializeField] Material eggYolk;
        [Tooltip("Wisps off the pan: steam while it cooks, smoke as it starts to burn.")]
        [SerializeField] ParticleSystem smoke;

        readonly List<string> kinds = new List<string>();
        readonly List<Transform> shown = new List<Transform>();
        Cookware cookware;
        SoundLoop sizzle;
        MaterialPropertyBlock block;
        float tinted = -1f;

        public float Frying { get; private set; }
        public int Salt { get; private set; }
        public int Served { get; private set; }
        public bool Fried => Frying >= fryAfter;
        public bool Burnt => Frying >= burnAfter;
        // 0 raw to 1 done, then 0 done to 1 burnt.
        public float Cooked => fryAfter > 0f ? Mathf.Clamp01(Frying / fryAfter) : 1f;
        public float Charred => Mathf.Clamp01(Mathf.InverseLerp(fryAfter, burnAfter, Frying));
        public enum Doneness { Raw, Cooking, Done, Burning, Burnt }
        public Doneness Stage => Burnt ? Doneness.Burnt : Charred >= 0.6f ? Doneness.Burning
            : Fried ? Doneness.Done : Frying >= fryAfter * 0.35f ? Doneness.Cooking : Doneness.Raw;
        public bool HasFood => kinds.Count > 0;
        public int Room => Mathf.Max(0, capacity - Size);
        int Size { get { int n = 0; foreach (var k in kinds) n += k == "Egg" ? 2 : 1; return n; } }
        public int Plates => FullFood > onePlate ? 2 : 1;
        bool InHands
        {
            get
            {
                if (TryGetComponent(out Net.ItemNet net) && net.IsSpawned) return net.Holder != Net.ItemNet.NoOne;
                return cookware.Pickup.HeldBy != null;
            }
        }

        void Awake()
        {
            cookware = GetComponent<Cookware>();
            block = new MaterialPropertyBlock();
            sizzle = Sfx.Loop(SoundId.Simmer, transform);
        }

        void Update()
        {
            bool cooking = HasFood && cookware.IsHot;
            sizzle.Set(cooking ? (Burnt ? 0.4f : 1f) : 0f);
            sizzle.Pitch(1.6f);
            if (!cooking || !Net.ItemNet.Runs(this)) return;
            Frying += Time.deltaTime;
        }

        void LateUpdate()
        {
            // The look follows the cooking a little at a time (guests' pans too).
            if (Mathf.Abs(Cooked + Charred - tinted) > 0.02f) Tint();
            Wisps(HasFood && cookware.IsHot && Frying >= fryAfter * 0.5f);
        }

        void Wisps(bool on)
        {
            if (smoke == null) return;
            if (on)
            {
                var main = smoke.main;
                // Steam while it cooks, gray then dark smoke as it burns.
                main.startColor = Color.Lerp(new Color(1f, 1f, 1f, 0.35f), new Color(0.2f, 0.2f, 0.2f, 0.8f), Mathf.InverseLerp(0.4f, 1f, Charred));
                var emission = smoke.emission;
                emission.rateOverTimeMultiplier = Mathf.Lerp(3f, 18f, Charred);
                if (!smoke.isEmitting) smoke.Play();
            }
            else if (smoke.isEmitting) smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ---- What's in it ----

        public Dictionary<string, int> Counts()
        {
            var counts = new Dictionary<string, int>();
            foreach (var kind in kinds)
            {
                counts.TryGetValue(kind, out int had);
                counts[kind] = had + 1;
            }
            return counts;
        }

        // Everything in the pan, as food, before any plates are served.
        public float FullFood
        {
            get
            {
                if (meals == null || !HasFood) return 0f;
                float food = meals.PotFood(Counts(), Fried, Salt > 0) * (Fried ? fryShare : 1f);
                return Burnt ? food * burntShare : food;
            }
        }

        public string Dish
        {
            get
            {
                var counts = Counts();
                bool Has(string k) => counts.ContainsKey(k);
                string name = Has("Chicken") ? "Fried chicken"
                    : Has("Potato") && Has("Onion") ? "Potato and onion hash"
                    : Has("Egg") && Has("Potato") ? "Fried eggs and potatoes"
                    : Has("Egg") && counts.Count == 1 ? (counts["Egg"] > 1 ? "Fried eggs" : "Fried egg")
                    : Has("Winter") ? (Has("Onion") ? "Fried squash and onions" : "Fried squash")
                    : Has("Potato") ? "Fried potatoes"
                    : "Fried vegetables";
                switch (Stage)
                {
                    case Doneness.Burnt: return "Burnt " + name.ToLowerInvariant();
                    case Doneness.Done: case Doneness.Burning: return name;
                    case Doneness.Cooking: return "Half-cooked " + name.ToLowerInvariant();
                    default: return "Uncooked " + name.ToLowerInvariant();
                }
            }
        }

        // ---- Putting things in, serving ----

        static bool HoldingEgg(PlayerHands hands) => !hands.IsEmpty && hands.Held.Category == ItemCategory.Egg;

        public bool CanInteract(PlayerHands hands)
        {
            if (hands.IsEmpty || InHands) return false;
            if (Handful.Held(hands) != null) return Room > 0 && Served == 0;
            if (HoldingEgg(hands)) return Room >= 2 && Served == 0;
            if (hands.Held.TryGetComponent(out Food.Bowl bowl)) return bowl.IsPlate && bowl.IsEmpty && HasFood;
            if (hands.Held.TryGetComponent(out Seasoning salt)) return salt.Kind == Seasoning.Type.Salt && !salt.IsEmpty && HasFood && Served == 0;
            return false;
        }

        public string PromptVerb(PlayerHands hands)
        {
            if (Handful.Held(hands) != null) return "Drop in the skillet";
            if (HoldingEgg(hands)) return "Crack an egg into the skillet";
            if (hands.Held.GetComponent<Food.Bowl>() != null) return "Serve a plate";
            return "Add a pinch of salt";
        }

        public void Interact(PlayerHands hands)
        {
            if (Handful.Held(hands) is Handful handful)
            {
                AddPieces(handful.Take(Room));
                handful.DoneWith(hands);
            }
            else if (HoldingEgg(hands))
            {
                Pickup egg = hands.TakeHeld();
                Net.ItemNet.Remove(egg.gameObject);
                AddEgg();
            }
            else if (hands.Held.TryGetComponent(out Food.Bowl bowl))
            {
                Serve(bowl);
            }
            else if (hands.Held.TryGetComponent(out Seasoning salt) && salt.Use(1f) > 0f)
            {
                Salt++;
                Sfx.Play(SoundId.SaltShake, transform.position);
            }
            Share();
        }

        // Chopped pieces off the board or from a handful, laid in the pan.
        public void AddPieces(List<Transform> added)
        {
            foreach (var piece in added)
            {
                if (piece == null) continue;
                if (Room <= 0) { Destroy(piece.gameObject); continue; }
                kinds.Add(RecipeData.KindOf(piece.name));
                Lay(piece);
            }
            Sfx.Play(SoundId.SliceDrop, transform.position, 1f, 0.1f);
            Frying = Mathf.Min(Frying, fryAfter * 0.5f);
        }

        public void AddEgg()
        {
            if (Room < 2) return;
            kinds.Add("Egg");
            Lay(MakeEgg());
            Sfx.Play(SoundId.SliceDrop, transform.position, 0.6f, 0.1f);
            Frying = Mathf.Min(Frying, fryAfter * 0.5f);
        }

        // One plate's worth onto an empty plate; the last plate empties the pan.
        public void Serve(Food.Bowl bowl)
        {
            if (!HasFood) return;
            int plates = Plates;
            bowl.Fill(Dish, FullFood / plates, Burnt ? new Color(0.15f, 0.12f, 0.1f) : Fried ? new Color(0.82f, 0.63f, 0.33f) : new Color(0.85f, 0.8f, 0.62f));
            Sfx.Play(SoundId.SetDown, transform.position, 0.7f, 0.1f);
            Served++;
            if (Served >= plates) Empty();
            Share();
        }

        // Scraped out at the sink or the trash, to start over.
        public bool HasContents => HasFood;

        public void Empty()
        {
            kinds.Clear();
            foreach (var t in shown) if (t != null) Destroy(t.gameObject);
            shown.Clear();
            Frying = 0f;
            Salt = 0;
            Served = 0;
        }

        public void SetPouring(bool pouring) { }

        void Share()
        {
            if (TryGetComponent(out Net.ItemNet net)) net.Share();
        }

        // ---- Looks ----

        void Lay(Transform piece)
        {
            piece.SetParent(bottom, worldPositionStays: false);
            int i = shown.Count;
            float angle = i * 2.4f;
            float r = panRadius * 0.75f * Mathf.Sqrt((i % capacity + 0.5f) / capacity);
            piece.localPosition = new Vector3(Mathf.Cos(angle) * r, 0.004f + (i / capacity) * 0.004f, Mathf.Sin(angle) * r);
            piece.localRotation = piece.name == "Egg" ? Quaternion.identity : Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f);
            foreach (var r2 in piece.GetComponentsInChildren<Renderer>()) r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shown.Add(piece);
            Tint();
        }

        Transform MakeEgg()
        {
            var egg = new GameObject("Egg").transform;
            Piece(egg, "Edge", new Vector3(0f, -0.0004f, 0f), new Vector3(0.078f, 0.0016f, 0.078f), eggWhite, PrimitiveType.Cylinder);
            Piece(egg, "White", Vector3.zero, new Vector3(0.07f, 0.002f, 0.07f), eggWhite, PrimitiveType.Cylinder);
            Piece(egg, "Yolk", new Vector3(0.006f, 0.004f, 0.004f), new Vector3(0.026f, 0.012f, 0.026f), eggYolk, PrimitiveType.Sphere);
            return egg;
        }

        // A slice as it lies in the pan (a rebuilt one, after loading).
        Transform MakeSlice(string kind)
        {
            Material look = null;
            if (looks != null) foreach (var l in looks) if (l.kind == kind) look = l.material;
            var slice = new GameObject(kind + " Slice").transform;
            Piece(slice, "Slice", Vector3.zero, new Vector3(0.04f, 0.003f, 0.04f), look, PrimitiveType.Cylinder);
            return slice;
        }

        static void Piece(Transform parent, string name, Vector3 at, Vector3 size, Material mat, PrimitiveType shape)
        {
            var go = GameObject.CreatePrimitive(shape);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        // How done it looks. Egg whites start a clear gray and set white,
        // their edges crisp golden; the yolk goes from deep orange to a
        // cooked yellow. Everything else goes golden. Past done it browns,
        // then goes black.
        static readonly Color rawWhite = new Color(0.72f, 0.72f, 0.66f);
        static readonly Color setWhite = new Color(0.98f, 0.97f, 0.93f);
        static readonly Color rawYolk = new Color(1f, 0.52f, 0.02f);
        static readonly Color setYolk = new Color(1f, 0.78f, 0.3f);
        static readonly Color golden = new Color(0.8f, 0.55f, 0.22f);
        static readonly Color browned = new Color(0.45f, 0.3f, 0.15f);
        static readonly Color charred = new Color(0.09f, 0.07f, 0.06f);

        Color Shade(string part, Color baseColor)
        {
            float c = Cooked, b = Charred;
            Color done;
            switch (part)
            {
                case "White": done = Color.Lerp(rawWhite, setWhite, c); break;
                case "Edge": done = Color.Lerp(Color.Lerp(rawWhite, setWhite, c), golden, Mathf.InverseLerp(0.6f, 1f, c)); break;
                case "Yolk": done = Color.Lerp(rawYolk, setYolk, c); break;
                default: done = Color.Lerp(baseColor, golden, 0.45f * c); break;
            }
            Color shade = Color.Lerp(Color.Lerp(done, browned, Mathf.InverseLerp(0.25f, 0.6f, b)), charred, Mathf.InverseLerp(0.5f, 1f, b));
            shade.a = 1f;
            return shade;
        }

        void Tint()
        {
            tinted = Cooked + Charred;
            foreach (var piece in shown)
            {
                if (piece == null) continue;
                foreach (var look in piece.GetComponentsInChildren<Renderer>())
                {
                    Color baseColor = look.sharedMaterial != null && look.sharedMaterial.HasProperty("_BaseColor") ? look.sharedMaterial.GetColor("_BaseColor") : Color.white;
                    string part = piece.name == "Egg" ? look.name : "";
                    look.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", Shade(part, baseColor));
                    look.SetPropertyBlock(block);
                    // A cooked yolk sits lower.
                    if (part == "Yolk") look.transform.localScale = new Vector3(0.026f, Mathf.Lerp(0.012f, 0.008f, Cooked), 0.026f);
                }
            }
        }

        // ---- Saving and sharing ----

        [Serializable]
        class State
        {
            public List<string> kinds = new List<string>();
            public float frying;
            public int salt;
            public int served;
        }

        public string Capture() => JsonUtility.ToJson(new State { kinds = new List<string>(kinds), frying = Mathf.Round(Frying * 10f) / 10f, salt = Salt, served = Served });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            bool same = state.kinds.Count == kinds.Count && state.kinds.TrueForAll(k => kinds.Contains(k));
            if (!same)
            {
                Empty();
                foreach (var kind in state.kinds)
                {
                    kinds.Add(kind);
                    Lay(kind == "Egg" ? MakeEgg() : MakeSlice(kind));
                }
            }
            Frying = state.frying;
            Salt = state.salt;
            Served = state.served;
            Tint();
        }
    }
}
