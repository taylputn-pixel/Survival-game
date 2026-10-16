using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A bottle of lemon juice from the pantry. Stood on the packing towel, a
    // press tips a spoonful into the next jar of tomatoes that needs it.
    [RequireComponent(typeof(Pickup))]
    public class LemonJuice : MonoBehaviour
    {
        [Tooltip("Its pouring lip. Forward points the way it pours.")]
        [SerializeField] Transform lip;
        [SerializeField] ParticleSystem stream;
        [SerializeField] float pourSeconds = 0.5f;
        [SerializeField] float tipAngle = 100f;
        [Tooltip("How far above the jar's mouth the lip is held, in meters.")]
        [SerializeField] float pourHeight = 0.05f;

        SoundLoop sound;

        public bool Busy { get; private set; }

        void Awake()
        {
            sound = Sfx.Loop(SoundId.Pour, lip);
            stream.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Up over the jar, a quick tip, and back where it stood.
        public IEnumerator SpoonInto(JarContents jar)
        {
            Busy = true;
            Transform t = transform;
            Vector3 restPosition = t.position;
            Quaternion restRotation = t.rotation;
            float tilt = 0f;
            for (float time = 0f; time < pourSeconds * 2.2f; time += Time.deltaTime)
            {
                bool pour = time > pourSeconds * 0.5f && time < pourSeconds * 1.5f;
                tilt = Mathf.MoveTowards(tilt, pour ? tipAngle : 0f, tipAngle / (pourSeconds * 0.4f) * Time.deltaTime);
                bool tipped = tilt > tipAngle * 0.8f;
                SetPouring(tipped);
                Pouring.Follow(t, lip, restPosition, restRotation, time < pourSeconds * 1.7f ? jar.Mouth : restPosition, tilt, tipAngle, pourHeight, 14f);
                yield return null;
            }
            SetPouring(false);
            jar.AddLemon();
            for (float time = 0f; time < 0.3f; time += Time.deltaTime)
            {
                float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
                t.SetPositionAndRotation(Vector3.Lerp(t.position, restPosition, k), Quaternion.Slerp(t.rotation, restRotation, k));
                yield return null;
            }
            t.SetPositionAndRotation(restPosition, restRotation);
            Busy = false;
        }

        void SetPouring(bool pouring)
        {
            sound.Set(pouring);
            if (pouring && !stream.isEmitting) stream.Play();
            else if (!pouring && stream.isEmitting) stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }
}
