using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // One hen. By day she wanders the run and pecks; at dusk she heads for
    // the coop door and goes in if it's open (or waits outside it if it's
    // shut). In the morning she comes back out if the door is open. In a
    // shared game the host moves her and everyone else sees her follow.
    public class Hen : MonoBehaviour
    {
        [SerializeField] Flock flock;
        [Tooltip("Where she roosts inside the coop.")]
        [SerializeField] Transform roost;
        [SerializeField] Transform head;

        public bool Alive
        {
            get => alive;
            set
            {
                alive = value;
                foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = value;
            }
        }
        public int DaysHungry { get; set; }
        // Roosting in the coop for the night.
        public bool Inside { get; private set; }

        bool alive = true;
        Vector3 target;
        float peckUntil;
        Vector3 headRest;
        Vector3 lastPosition;

        void Awake()
        {
            headRest = head.localPosition;
            target = transform.position;
            lastPosition = transform.position;
        }

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        void Update()
        {
            if (alive && Hosting)
            {
                Think();
            }
            Peck();
        }

        void Think()
        {
            var settings = flock.Settings;
            bool night = flock.Clock.IsNight;
            if (Inside)
            {
                // Out again in the morning, if the door's open.
                if (!night && flock.DoorOpen)
                {
                    Inside = false;
                    transform.position = flock.Doorway.position;
                    PickSpot();
                }
                return;
            }
            if (night)
            {
                // Home to roost.
                if (WalkTo(flock.Doorway.position, settings.walkSpeed) && flock.DoorOpen)
                {
                    Inside = true;
                    transform.position = roost.position;
                }
                return;
            }
            if (Time.time < peckUntil)
            {
                return;
            }
            if (WalkTo(target, settings.walkSpeed))
            {
                peckUntil = Time.time + Random.Range(settings.peckSeconds.x, settings.peckSeconds.y);
                PickSpot();
            }
        }

        void PickSpot() => target = flock.SpotInRun();

        // A step toward a place; true once she's there.
        bool WalkTo(Vector3 place, float speed)
        {
            Vector3 flat = place - transform.position;
            flat.y = 0f;
            if (flat.magnitude < 0.05f)
            {
                return true;
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-8f * Time.deltaTime));
            transform.position += flat.normalized * Mathf.Min(speed * Time.deltaTime, flat.magnitude);
            return false;
        }

        // Standing still, she pecks at the ground now and then.
        void Peck()
        {
            float moved = (transform.position - lastPosition).magnitude;
            lastPosition = transform.position;
            float dip = moved < 0.0005f && alive ? Mathf.Max(0f, Mathf.Sin(Time.time * 7f + GetInstanceID())) * 0.04f : 0f;
            head.localPosition = Vector3.Lerp(head.localPosition, headRest + new Vector3(0f, -dip, dip * 0.6f), 1f - Mathf.Exp(-20f * Time.deltaTime));
        }
    }
}
