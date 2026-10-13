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
        // How fast water leaves the spout, for working out where it lands.
        public float StreamSpeed => stream.main.startSpeed.constant;
        public float StreamGravity => stream.main.gravityModifier.constant * -Physics.gravity.y;

        // Where water leaving the spout now would hit a surface at that height.
        public Vector3 LandingOn(Plane ground)
        {
            return Landing(spout.position, spout.forward * StreamSpeed, ground);
        }

        public Vector3 Landing(Vector3 from, Vector3 velocity, Plane ground)
        {
            float height = ground.GetDistanceToPoint(from);
            float g = Mathf.Max(StreamGravity, 0.01f);
            float time = (velocity.y + Mathf.Sqrt(Mathf.Max(velocity.y * velocity.y + 2f * g * height, 0f))) / g;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z) * time;
            return ground.ClosestPointOnPlane(from + flat);
        }

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
