using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // The enamel pot for pickling brine. Set it on a burner and step up:
    // pour vinegar to the low line and water to the high line from the
    // warming shelf, add scoops of salt, turn the burner on, and wait for the
    // boil. Boiling brine is ready to pour into jars at the packing towel.
    [RequireComponent(typeof(Cookware))]
    public class BrinePotStation : Station, IPickupGuard, IItemState, IEmptyable
    {
        [SerializeField] RecipeData recipe;
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
        [SerializeField] float grabRadius = 0.08f;
        [Tooltip("How fast the pot fills while pouring (share of the pot per second).")]
        [SerializeField] float pourRate = 0.22f;
        [SerializeField] float pourTilt = 75f;
        [SerializeField] float pourHeight = 0.12f;
        [SerializeField] float followSharpness = 10f;
        [SerializeField] Color waterColor = new Color(0.78f, 0.88f, 0.92f, 0.5f);
        [SerializeField] Color brineColor = new Color(0.86f, 0.83f, 0.5f, 0.6f);

        static readonly List<BrinePotStation> all = new List<BrinePotStation>();

        Cookware cookware;
        PlayerHands hands;
        StoveStaples staples;
        StoveView stoveView;
        StoveStaples.Source pouringFrom;
        MaterialPropertyBlock block;
        int enteredFrame;
        float saltShake;

        public float Vinegar { get; private set; }
        public float Water { get; private set; }
        public int Salt { get; private set; }
        // 0 is cold, 1 is a rolling boil.
        public float Heat { get; private set; }
        public float Liquid => Vinegar + Water;
        public bool IsReady => cookware.OnStove && Liquid > 0.02f && Salt > 0 && Heat >= 1f;
        public Transform Lip => lip;

        // A pot of boiling brine on the stove, if there is one.
        public static BrinePotStation ReadyPot()
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

        protected override void Awake()
        {
            base.Awake();
            cookware = GetComponent<Cookware>();
            block = new MaterialPropertyBlock();
            staples = FindFirstObjectByType<StoveStaples>();
            stoveView = FindFirstObjectByType<StoveView>();
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ShowLiquid();
        }

        void OnEnable() => all.Add(this);
        void OnDisable() => all.Remove(this);

        public bool AllowPickup(PlayerHands player) => !cookware.OnStove;

        // One view of the whole stove front: the shelf, every burner, and the knobs.
        protected override Transform ViewPoint => stoveView != null ? stoveView.transform : base.ViewPoint;

        public override bool CanInteract(PlayerHands player) => cookware.OnStove && player.IsEmpty;

        public override string PromptVerb(PlayerHands player) => "Make brine";

        public override void Interact(PlayerHands player)
        {
            hands = player;
            enteredFrame = Time.frameCount;
            Enter(player);
        }

        public bool HasContents => Liquid > 0f || Salt > 0;

        // Poured out at the sink, to start the brine over.
        public void Empty()
        {
            Vinegar = Water = 0f;
            Salt = 0;
            Heat = 0f;
            ShowLiquid();
        }

        // Pouring brine out into a jar uses some up.
        public void Use(float share)
        {
            float liquid = Liquid;
            if (liquid <= 0f)
            {
                return;
            }
            float keep = Mathf.Max(liquid - share, 0f) / liquid;
            Vinegar *= keep;
            Water *= keep;
            if (Liquid <= 0.02f)
            {
                Vinegar = Water = 0f;
                Salt = 0;
            }
            ShowLiquid();
        }

        public void SetPouring(bool pouring)
        {
            if (pouring && !stream.isEmitting) stream.Play();
            else if (!pouring && stream.isEmitting) stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        protected override IEnumerable<Vector3> SnapTargets()
        {
            if (pouringFrom != null)
            {
                yield break;
            }
            yield return staples.vinegar.vessel.position;
            yield return staples.water.vessel.position;
            yield return staples.salt.vessel.position;
            yield return cookware.Burner.Knob.position;
            yield return cookware.Handle.position;
        }

        protected override void OnLeave() => StopPouring();

        protected override void WhileWorking()
        {
            if (pouringFrom != null)
            {
                PourIn();
                return;
            }

            bool pressed = Time.frameCount != enteredFrame && UseAction.WasPressedThisFrame();
            UseHint = null;
            reticle.Highlight = false;

            if (Near(staples.vinegar.vessel) || Near(staples.water.vessel))
            {
                StoveStaples.Source source = Near(staples.vinegar.vessel) ? staples.vinegar : staples.water;
                Hint(source == staples.vinegar ? "Pour vinegar" : "Pour water");
                if (pressed && Liquid < 1f)
                {
                    pouringFrom = source;
                    reticle.Hidden = true;
                }
            }
            else if (Near(staples.salt.vessel))
            {
                Hint("Add a scoop of salt");
                if (pressed && Liquid > 0f && saltShake <= 0f)
                {
                    Salt++;
                    saltShake = 0.7f;
                }
            }
            else if (Near(cookware.Burner.Knob))
            {
                Hint(cookware.Burner.On ? "Turn the burner off" : "Turn the burner on");
                if (pressed)
                {
                    cookware.Burner.Toggle();
                }
            }
            else if (Near(cookware.Handle))
            {
                Hint("Take off the stove");
                if (pressed)
                {
                    Leave();
                    cookware.LiftOff(hands);
                }
            }
        }

        void PourIn()
        {
            StoveStaples.Source source = pouringFrom;
            UseHint = "Hold to pour";
            if (!UseAction.IsPressed() || Liquid >= 1f)
            {
                StopPouring();
                return;
            }
            bool tipped = source.Tilt >= pourTilt * 0.8f;
            source.SetPouring(tipped);
            if (tipped)
            {
                float amount = Mathf.Min(pourRate * Time.deltaTime, 1f - Liquid);
                if (source == staples.vinegar) Vinegar += amount;
                else Water += amount;
                // Cold water takes the heat down a little.
                Heat = Mathf.Max(Heat - amount * 0.5f, 0f);
                ShowLiquid();
            }
        }

        void StopPouring()
        {
            if (pouringFrom == null)
            {
                return;
            }
            pouringFrom.SetPouring(false);
            pouringFrom = null;
            reticle.Hidden = false;
            LogBrine();
        }

        protected override void Update()
        {
            base.Update();
            MoveStaples();
            Warm();
        }

        void MoveStaples()
        {
            if (staples == null)
            {
                return;
            }
            // The salt box tips over the pot for a moment and shakes some in.
            saltShake = Mathf.Max(saltShake - Time.deltaTime, 0f);
            staples.salt.SetPouring(saltShake > 0.15f && staples.salt.Tilt > pourTilt * 0.6f);

            foreach (var source in new[] { staples.vinegar, staples.water, staples.salt })
            {
                bool tipping = source == staples.salt ? saltShake > 0.25f : source == pouringFrom && IsWorking;
                source.Tilt = Mathf.MoveTowards(source.Tilt, tipping ? pourTilt : 0f, pourTilt / 0.4f * Time.deltaTime);
                float away = (source.vessel.position - source.RestPosition).sqrMagnitude;
                if (source.Tilt <= 0f && away < 0.000004f)
                {
                    // Home on the shelf.
                    source.vessel.SetPositionAndRotation(source.RestPosition, source.RestRotation);
                }
                else
                {
                    Pouring.Follow(source.vessel, source.lip, source.RestPosition, source.RestRotation,
                        bottom.position, source.Tilt, pourTilt, pourHeight + innerHeight, followSharpness);
                }
            }
        }

        // On a lit burner the brine heats toward a boil; off the heat it cools slowly.
        void Warm()
        {
            float before = Heat;
            if (cookware.IsHot && Liquid > 0f)
            {
                Heat = Mathf.Min(Heat + Time.deltaTime / (recipe.heatSeconds * Mathf.Max(Liquid, 0.25f)), 1f);
            }
            else
            {
                Heat = Mathf.Max(Heat - Time.deltaTime / (recipe.heatSeconds * 6f), 0f);
            }

            bool steaming = Heat > 0.5f && Liquid > 0f;
            if (steaming && !steam.isEmitting) steam.Play();
            else if (!steaming && steam.isEmitting) steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (Heat >= 1f && Liquid > 0f)
            {
                // A rolling boil: the surface trembles.
                surface.localPosition = SurfacePosition() + Vector3.up * Mathf.Sin(Time.time * 37f) * 0.0015f;
            }
            else if (before >= 1f)
            {
                ShowLiquid();
            }
        }

        Vector3 SurfacePosition() => bottom.localPosition + Vector3.up * Mathf.Max(Liquid, 0.001f) * innerHeight;

        void ShowLiquid()
        {
            surface.gameObject.SetActive(Liquid > 0f);
            surface.localPosition = SurfacePosition();
            float vinegarShare = Liquid > 0f ? Vinegar / Liquid : 0f;
            surfaceRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.Lerp(waterColor, brineColor, Mathf.Clamp01(vinegarShare * 2f)));
            surfaceRenderer.SetPropertyBlock(block);
        }

        bool Near(Transform target) => IsCursorNear(target.position, grabRadius);

        void Hint(string hint)
        {
            UseHint = hint;
            reticle.Highlight = true;
        }

        void LogBrine()
        {
            // Not shown to the player yet; recorded for tuning brine quality later.
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
        }

        public string Capture() => JsonUtility.ToJson(new State { vinegar = Vinegar, water = Water, salt = Salt, heat = Heat });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Vinegar = state.vinegar;
            Water = state.water;
            Salt = state.salt;
            Heat = state.heat;
            ShowLiquid();
        }
    }
}
