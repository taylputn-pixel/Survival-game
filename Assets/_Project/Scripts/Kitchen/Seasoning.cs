using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Something from the pantry that goes into the cooking pot: the vinegar
    // bottle (pour it, holding the button) or the salt box (a scoop per press).
    [RequireComponent(typeof(Pickup))]
    public class Seasoning : MonoBehaviour
    {
        public enum Type { Vinegar, Salt }

        [SerializeField] Type kind;
        [Tooltip("Its pouring lip. Forward points the way it pours.")]
        [SerializeField] Transform lip;
        [SerializeField] ParticleSystem stream;

        SoundLoop sound;

        public Type Kind => kind;
        public Transform Lip => lip;

        void Awake()
        {
            // The salt box shakes; only the bottle has a pouring sound.
            if (kind == Type.Vinegar)
            {
                sound = Sfx.Loop(SoundId.Pour, lip);
            }
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void SetPouring(bool pouring)
        {
            if (sound != null) sound.Set(pouring);
            if (pouring && !stream.isEmitting) stream.Play();
            else if (!pouring && stream.isEmitting) stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
