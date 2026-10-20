using System;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Food
{
    // Food that goes bad with time: fresh at 1, rotten at 0. It keeps longer
    // somewhere cold (ColdStorage) and darkens as it turns. Something else on
    // the item can keep it from spoiling for now (a sealed jar, an empty one).
    // Dried (in the dehydrator) it shrinks, darkens a little, and keeps many
    // times longer.
    public class Perishable : MonoBehaviour, IItemState, IAfterLoad
    {
        [Tooltip("Days it keeps at room temperature, from fresh to rotten.")]
        [Min(0.1f)] [SerializeField] float shelfLifeDays = 5f;
        [Tooltip("Darken the item as it spoils. Off for jars, whose glass shouldn't change.")]
        [SerializeField] bool darken = true;
        [Tooltip("Dried food keeps this many times longer.")]
        [SerializeField] float driedKeeps = 30f;
        [Tooltip("How often it checks the clock and where it sits, in real seconds.")]
        [SerializeField] float checkSeconds = 1f;

        static readonly Color RotColor = new Color(0.42f, 0.36f, 0.24f);
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public float Freshness { get; private set; } = 1f;
        public bool IsRotten => Freshness <= 0f;
        public bool Dried { get; private set; }
        // Hours spent drying so far.
        public float DryingHours { get; private set; }
        public float ShelfLifeDays => shelfLifeDays;
        // Turned off by its owner while it can't spoil (a sealed jar).
        public Func<bool> CanSpoil;

        DayClock clock;
        float lastHours;
        float nextCheck;
        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock block;
        Vector3 baseScale;

        // The freshness left after some hours at a given rate (1 at room
        // temperature, less somewhere cold, 0 frozen).
        public static float Spoil(float freshness, float hours, float shelfLifeDays, float rate) =>
            Mathf.Max(0f, freshness - hours * rate / (shelfLifeDays * 24f));

        void Awake() => baseScale = transform.localScale;

        void Start()
        {
            clock = FindAnyObjectByType<DayClock>();
            lastHours = clock != null ? clock.TotalHours : 0f;
            if (clock != null) clock.Jumped += AfterLoad;
            if (darken)
            {
                renderers = GetComponentsInChildren<Renderer>();
                baseColors = new Color[renderers.Length];
                block = new MaterialPropertyBlock();
                for (int i = 0; i < renderers.Length; i++)
                {
                    Material material = renderers[i].sharedMaterial;
                    baseColors[i] = material != null && material.HasProperty(BaseColor) ? material.GetColor(BaseColor) : Color.white;
                }
            }
            Show();
        }

        void Update()
        {
            if (clock == null || Time.time < nextCheck)
            {
                return;
            }
            nextCheck = Time.time + checkSeconds;
            float now = clock.TotalHours;
            float hours = now - lastHours;
            lastHours = now;
            // In a shared game it ages on the computer running it.
            if (!Net.ItemNet.Runs(this))
            {
                return;
            }
            if (hours <= 0f || !clock.Mode.foodSpoils || (CanSpoil != null && !CanSpoil()))
            {
                return;
            }
            Freshness = Spoil(Freshness, hours, Dried ? shelfLifeDays * driedKeeps : shelfLifeDays, ColdStorage.RateAt(transform.position));
            Show();
        }

        // Warm air through the dehydrator for a while. Dried once it's had enough.
        public void Dry(float hours, float hoursNeeded)
        {
            if (Dried || IsRotten)
            {
                return;
            }
            DryingHours += hours;
            if (DryingHours >= hoursNeeded)
            {
                Dried = true;
                Show();
            }
        }

        void OnDestroy()
        {
            if (clock != null) clock.Jumped -= AfterLoad;
        }

        // As good as new (a jar that's been emptied and washed).
        public void Refresh()
        {
            Freshness = 1f;
            Show();
        }

        // Grays and browns once it's past half fresh.
        void Show()
        {
            if (renderers == null || block == null)
            {
                return;
            }
            float rot = Mathf.InverseLerp(0.5f, 0f, Freshness);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(block);
                Color color = Color.Lerp(baseColors[i], baseColors[i] * RotColor, rot);
                if (Dried) color *= new Color(0.78f, 0.7f, 0.6f, 1f);
                block.SetColor(BaseColor, color);
                renderers[i].SetPropertyBlock(block);
            }
            // Dried, it shrinks and wrinkles up.
            transform.localScale = Dried ? baseScale * 0.72f : baseScale;
        }

        [Serializable]
        class State
        {
            public float freshness;
            public bool dried;
            public float drying;
        }

        public string Capture() => JsonUtility.ToJson(new State { freshness = Freshness, dried = Dried, drying = Mathf.Round(DryingHours * 100f) / 100f });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Freshness = state.freshness;
            Dried = state.dried;
            DryingHours = state.drying;
            Show();
        }

        // The clock jumped to the saved time; don't count that jump as aging.
        public void AfterLoad()
        {
            if (clock == null) clock = FindAnyObjectByType<DayClock>();
            if (clock != null) lastHours = clock.TotalHours;
        }
    }
}
