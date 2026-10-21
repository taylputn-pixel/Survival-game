using GrandmasHouse.Audio;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Grandma's wind-up kitchen timer by the stove. It sets itself when the
    // canner's lid goes on over a load of jars: its pointer winds back by the
    // recipe's time and creeps home as the jars process (standing still while
    // the water's off the boil). When the time's up it rings, loud enough to
    // hear from the yard. Every computer reads the same canner, so everyone
    // hears it.
    public class KitchenTimer : MonoBehaviour
    {
        [SerializeField] Canner canner;
        [Tooltip("The pointer, turned about its own up axis.")]
        [SerializeField] Transform pointer;
        [Tooltip("Degrees the pointer is wound back for a whole load.")]
        [SerializeField] float fullTurn = 330f;
        [SerializeField] float ringSeconds = 4f;
        [Tooltip("How far off the bell carries, in meters.")]
        [SerializeField] float ringReach = 45f;

        public bool Ringing => Time.time < ringUntil;
        // Share of the time still to go, as the pointer shows it.
        public float Showing { get; private set; }

        bool counting;
        float ringUntil;
        float nextDing;
        Quaternion rest;

        void Awake() => rest = transform.localRotation;

        void Update()
        {
            if (canner == null) return;
            if (canner.Processing)
            {
                counting = true;
                Showing = canner.ProcessTotal > 0f ? canner.ProcessLeft / canner.ProcessTotal : 0f;
            }
            else
            {
                // Time's up (or the jars finished while everyone slept).
                if (counting && canner.Done) Ring();
                counting = false;
                Showing = Mathf.MoveTowards(Showing, 0f, Time.deltaTime);
            }
            if (pointer != null) pointer.localRotation = Quaternion.Euler(0f, -Showing * fullTurn, 0f);

            if (Ringing)
            {
                if (Time.time >= nextDing)
                {
                    Sfx.Play(SoundId.TimerBell, transform.position, 1f, 0.02f, ringReach);
                    nextDing = Time.time + 0.14f;
                }
                // It rattles on the counter while it rings.
                transform.localRotation = rest * Quaternion.Euler(Mathf.Sin(Time.time * 70f) * 2f, 0f, Mathf.Cos(Time.time * 63f) * 2f);
            }
            else
            {
                transform.localRotation = rest;
            }
        }

        public void Ring()
        {
            ringUntil = Time.time + ringSeconds;
            nextDing = Time.time;
        }
    }
}
