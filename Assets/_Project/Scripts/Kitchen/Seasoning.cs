using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Something from the pantry that goes into the cooking pot: the vinegar
    // bottle (pour it, holding the button) or the salt box (a scoop per press).
    // Each holds only so much; an empty one is good for nothing until it's
    // traded for a full one at the store.
    [RequireComponent(typeof(Pickup))]
    public class Seasoning : MonoBehaviour, IItemState
    {
        public enum Type { Vinegar, Salt }

        [SerializeField] Type kind;
        [Tooltip("Its pouring lip. Forward points the way it pours.")]
        [SerializeField] Transform lip;
        [SerializeField] ParticleSystem stream;
        [Tooltip("How much it holds: scoops of salt, or pot-fulls of vinegar (a batch of brine is about half).")]
        [SerializeField] float capacity = 40f;

        SoundLoop sound;

        public Type Kind => kind;
        public Transform Lip => lip;
        public float Left { get; private set; } = -1f;
        public bool IsEmpty => Left <= 0.0001f;
        public float Capacity => capacity;

        void Awake()
        {
            if (Left < 0f) Left = capacity;
            // The salt box shakes; only the bottle has a pouring sound.
            if (kind == Type.Vinegar)
            {
                sound = Sfx.Loop(SoundId.Pour, lip);
            }
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Takes up to this much out; says how much there was to take.
        public float Use(float amount)
        {
            if (Left < 0f) Left = capacity;
            float used = Mathf.Min(amount, Left);
            Left -= used;
            if (IsEmpty) SetPouring(false);
            return used;
        }

        public string Capture() => (Mathf.Round(Left * 1000f) / 1000f).ToString(System.Globalization.CultureInfo.InvariantCulture);

        public void Restore(string json)
        {
            if (float.TryParse(json, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float left)) Left = left;
        }

        public void SetPouring(bool pouring)
        {
            if (sound != null) sound.Set(pouring);
            if (pouring && !stream.isEmitting) stream.Play();
            else if (!pouring && stream.isEmitting) stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
