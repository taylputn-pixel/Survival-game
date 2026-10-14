using GrandmasHouse.Audio;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // What Grandma keeps on the warming shelf over the stove: a jug of
    // vinegar, a pitcher of water, and the salt box. The cooking pot's
    // station reaches up for them.
    public class StoveStaples : MonoBehaviour
    {
        [System.Serializable]
        public class Source
        {
            public Transform vessel;
            [Tooltip("Its pouring lip. Forward points the way it pours.")]
            public Transform lip;
            public ParticleSystem stream;

            public Vector3 RestPosition { get; private set; }
            public Quaternion RestRotation { get; private set; }
            public float Tilt { get; set; }
            // The sound of pouring (none for the salt box, which shakes).
            public SoundLoop Sound { get; set; }

            public void Remember()
            {
                RestPosition = vessel.position;
                RestRotation = vessel.rotation;
            }

            public void SetPouring(bool pouring)
            {
                if (Sound != null) Sound.Set(pouring);
                if (pouring && !stream.isEmitting) stream.Play();
                else if (!pouring && stream.isEmitting) stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        public Source vinegar;
        public Source water;
        public Source salt;

        void Awake()
        {
            foreach (var source in new[] { vinegar, water, salt })
            {
                source.Remember();
                if (source != salt)
                {
                    source.Sound = Sfx.Loop(SoundId.Pour, source.lip);
                }
                source.stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
