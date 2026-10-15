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
    // Grandma's enamel cooking pot. Brine is the first thing made in it, all
    // by hand with no station: fill it with water at the sink, bring the
    // vinegar and the salt box from the pantry and pour and shake them in
    // (look at the pot while holding them), set it on a burner and turn the
    // knob. Once it has boiled the brine stays ready, so the pot can move to
    // the hot pad by the packing towel.
    [RequireComponent(typeof(Cookware))]
    public class CookingPot : MonoBehaviour, IInteractable, IItemState, IEmptyable
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
        [Tooltip("How fast the pot fills while pouring vinegar (share of the pot per second).")]
        [SerializeField] float pourRate = 0.22f;
        [SerializeField] float pourTilt = 75f;
        [SerializeField] float pourHeight = 0.12f;
        [SerializeField] float followSharpness = 10f;
        [SerializeField] Color waterColor = new Color(0.78f, 0.88f, 0.92f, 0.5f);
        [SerializeField] Color brineColor = new Color(0.86f, 0.83f, 0.5f, 0.6f);

        static readonly List<CookingPot> all = new List<CookingPot>();

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
        public bool IsFull => Liquid >= 1f;
        // Boiled brine in a pot that's set down somewhere (not in your hands).
        public bool IsReady => !InHands && Liquid > 0.02f && Salt > 0 && Heat >= 1f;
        public Transform Lip => lip;
        bool InHands => player != null && (player.Held == cookware.Pickup || player.Extra == cookware.Pickup);

        // A pot of boiled brine set down somewhere, if there is one.
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
            var seasoning = hands.Held.GetComponent<Seasoning>();
            if (seasoning == null)
            {
                return false;
            }
            // Salt goes into something already in the pot.
            return seasoning.Kind == Seasoning.Type.Salt ? Liquid > 0f : !IsFull;
        }

        public string PromptVerb(PlayerHands hands)
        {
            return hands.Held.GetComponent<Seasoning>().Kind == Seasoning.Type.Salt ? "Add a scoop of salt" : "Hold to pour vinegar";
        }

        public void Interact(PlayerHands hands)
        {
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
                pouring = pouring && interactAction.IsPressed() && !IsFull;
                tilt = Mathf.MoveTowards(tilt, pouring ? pourTilt : 0f, pourTilt / 0.4f * Time.deltaTime);
                Pouring.Follow(t, bottle.Lip, restPosition, restRotation, bottom.position, tilt, pourTilt, pourHeight + innerHeight, followSharpness);
                bool tipped = pouring && tilt >= pourTilt * 0.8f;
                bottle.SetPouring(tipped);
                if (tipped)
                {
                    float amount = Mathf.Min(pourRate * Time.deltaTime, 1f - Liquid);
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
                    Salt++;
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

        // Cold water from the tap, held under the faucet at the sink.
        public void AddWater(float amount)
        {
            amount = Mathf.Min(amount, 1f - Liquid);
            Water += amount;
            Cool(amount);
            ShowLiquid();
        }

        // Anything cold poured in takes the heat down a little.
        void Cool(float amount) => Heat = Mathf.Max(Heat - amount * 0.5f, 0f);

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
            pourSound.Set(pouring);
            if (pouring && !stream.isEmitting) stream.Play();
            else if (!pouring && stream.isEmitting) stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        void Update() => Warm();

        // On a lit burner the brine heats toward a boil. Off the heat, brine
        // that has boiled stays ready (Cozy); anything short of a boil cools slowly.
        void Warm()
        {
            float before = Heat;
            if (cookware.IsHot && Liquid > 0f)
            {
                Heat = Mathf.Min(Heat + Time.deltaTime / (recipe.heatSeconds * Mathf.Max(Liquid, 0.25f)), 1f);
            }
            else if (Heat < 1f || Liquid <= 0f)
            {
                Heat = Mathf.Max(Heat - Time.deltaTime / (recipe.heatSeconds * 6f), 0f);
            }

            // It starts to simmer as it warms, then rolls into a boil, but only over the flame.
            bool overHeat = cookware.IsHot && Liquid > 0f;
            simmerSound.Set(overHeat ? Mathf.InverseLerp(0.3f, 0.95f, Heat) : 0f);
            boilSound.Set(overHeat && Heat >= 1f);

            bool steaming = Heat > 0.5f && Liquid > 0f;
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

        Vector3 SurfacePosition() => bottom.localPosition + Vector3.up * Mathf.Max(Liquid, 0.001f) * innerHeight;

        // The water's surface, where a faucet's stream lands.
        public Vector3 SurfaceWorld => transform.TransformPoint(SurfacePosition());

        void ShowLiquid()
        {
            surface.gameObject.SetActive(Liquid > 0f);
            surface.localPosition = SurfacePosition();
            float vinegarShare = Liquid > 0f ? Vinegar / Liquid : 0f;
            surfaceRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.Lerp(waterColor, brineColor, Mathf.Clamp01(vinegarShare * 2f)));
            surfaceRenderer.SetPropertyBlock(block);
        }

        public void LogBrine()
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
