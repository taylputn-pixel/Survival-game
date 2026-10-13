using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // A metal watering can. Fill it at the spigot; it pours from the spout
    // until it runs dry. You can see how much is left by looking inside.
    [RequireComponent(typeof(Pickup))]
    public class WateringCan : MonoBehaviour, IItemState
    {
        [SerializeField] GardenSettings settings;
        [Tooltip("The tip of the spout. Its forward axis points the way water comes out.")]
        [SerializeField] Transform spout;
        [SerializeField] ParticleSystem stream;
        [Tooltip("The top opening; it shows water when there's water inside.")]
        [SerializeField] Renderer opening;
        [SerializeField] Color emptyColor = new Color(0.12f, 0.12f, 0.13f);
        [SerializeField] Color waterColor = new Color(0.35f, 0.55f, 0.7f);
        [Range(0f, 1f)]
        [SerializeField] float level;

        public float Level => level;
        public bool IsEmpty => level <= 0f;
        public bool IsFull => level >= 1f;
        public Transform Spout => spout;

        MaterialPropertyBlock block;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            ShowLevel();
            SetPouring(false);
        }

        public string Capture() => level.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public void Restore(string json)
        {
            level = float.Parse(json, System.Globalization.CultureInfo.InvariantCulture);
            ShowLevel();
        }

        public void Fill(float amount)
        {
            level = Mathf.Clamp01(level + amount);
            ShowLevel();
        }

        // Pours for a moment. Returns false once the can is empty.
        public bool Pour(float seconds)
        {
            if (IsEmpty)
            {
                SetPouring(false);
                return false;
            }
            level = Mathf.Max(level - seconds / settings.canPourSeconds, 0f);
            ShowLevel();
            SetPouring(!IsEmpty);
            return true;
        }

        public void SetPouring(bool pouring)
        {
            if (pouring && !stream.isEmitting)
            {
                stream.Play();
            }
            else if (!pouring && stream.isEmitting)
            {
                stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        void ShowLevel()
        {
            opening.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.Lerp(emptyColor, waterColor, Mathf.Sqrt(level)));
            opening.SetPropertyBlock(block);
        }
    }
}
