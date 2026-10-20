using System;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Food
{
    // Food that goes bad with time: fresh at 1, rotten at 0. It keeps longer
    // somewhere cold (ColdStorage) and darkens as it turns. Something else on
    // the item can keep it from spoiling for now (a sealed jar, an empty one).
    public class Perishable : MonoBehaviour, IItemState, IAfterLoad
    {
        [Tooltip("Days it keeps at room temperature, from fresh to rotten.")]
        [Min(0.1f)] [SerializeField] float shelfLifeDays = 5f;
        [Tooltip("Darken the item as it spoils. Off for jars, whose glass shouldn't change.")]
        [SerializeField] bool darken = true;
        [Tooltip("How often it checks the clock and where it sits, in real seconds.")]
        [SerializeField] float checkSeconds = 1f;

        static readonly Color RotColor = new Color(0.42f, 0.36f, 0.24f);
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        public float Freshness { get; private set; } = 1f;
        public bool IsRotten => Freshness <= 0f;
        public float ShelfLifeDays => shelfLifeDays;
        // Turned off by its owner while it can't spoil (a sealed jar).
        public Func<bool> CanSpoil;

        DayClock clock;
        float lastHours;
        float nextCheck;
        Renderer[] renderers;
        Color[] baseColors;
        MaterialPropertyBlock block;

        // The freshness left after some hours at a given rate (1 at room
        // temperature, less somewhere cold, 0 frozen).
        public static float Spoil(float freshness, float hours, float shelfLifeDays, float rate) =>
            Mathf.Max(0f, freshness - hours * rate / (shelfLifeDays * 24f));

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
            if (hours <= 0f || !clock.Mode.foodSpoils || (CanSpoil != null && !CanSpoil()))
            {
                return;
            }
            Freshness = Spoil(Freshness, hours, shelfLifeDays, ColdStorage.RateAt(transform.position));
            Show();
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
            if (renderers == null)
            {
                return;
            }
            float rot = Mathf.InverseLerp(0.5f, 0f, Freshness);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].GetPropertyBlock(block);
                block.SetColor(BaseColor, Color.Lerp(baseColors[i], baseColors[i] * RotColor, rot));
                renderers[i].SetPropertyBlock(block);
            }
        }

        [Serializable]
        class State
        {
            public float freshness;
        }

        public string Capture() => JsonUtility.ToJson(new State { freshness = Freshness });

        public void Restore(string json)
        {
            Freshness = JsonUtility.FromJson<State>(json).freshness;
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
