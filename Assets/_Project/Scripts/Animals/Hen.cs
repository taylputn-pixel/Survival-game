using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // One hen. By day she wanders the run and pecks; at dusk she heads for
    // the coop door and goes in if it's open (or waits outside it if it's
    // shut). In the morning she comes back out if the door is open. Leave
    // the run gate open and she may wander out through it. Out of the run
    // she's loose: she pecks about wherever she is, settles there for the
    // night (the fox's for the taking), and stays out until someone carries
    // her back. In a shared game the host moves her and everyone else sees
    // her follow. With empty hands you can pick her up and carry her
    // (LiveHen); lay her on the chopping block and take the axe to her for a
    // chicken to cook or cure. Set her down and she stays where she's put.
    // She may start as a chick (small, yellow, no comb): a chick eats less,
    // lays nothing and isn't for the pot, and grows into a hen after enough
    // fed nights (FlockSettings.daysToLay).
    public class Hen : MonoBehaviour, IInteractable, ISharedUse
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
                Show();
            }
        }
        // In someone's arms (or on the block): out of the run for now.
        public bool Carried { get; private set; }
        // Who has her (their client id; 0 playing alone).
        public ulong Carrier { get; private set; }
        public int DaysHungry { get; set; }
        // Fed nights since she came as a chick (grown hens start grown).
        public int AgeDays
        {
            get => ageDays;
            set
            {
                ageDays = value;
                Show();
            }
        }
        public bool IsChick => flock != null && ageDays < flock.Settings.daysToLay;
        // Roosting in the coop for the night.
        public bool Inside { get; private set; }
        // Out of the run, wandering wherever she got to.
        public bool Loose { get; private set; }
        // Where she was last set down or got out to; a loose hen keeps near it.
        public Vector3 Settled { get; private set; }
        // On her way out through the open gate.
        public bool HeadingOut => wayOut.Count > 0;

        bool alive = true;
        int ageDays = 999;
        Vector3 grownScale = Vector3.one;
        MaterialPropertyBlock block;
        Vector3 target;
        float peckUntil;
        Vector3 headRest;
        Vector3 lastPosition;
        float nextCluck;
        readonly List<Vector3> wayOut = new List<Vector3>();

        void Awake()
        {
            grownScale = transform.localScale;
            headRest = head.localPosition;
            target = transform.position;
            lastPosition = transform.position;
            Settled = transform.position;
        }

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        void Show()
        {
            bool seen = alive && !Carried;
            bool chick = IsChick;
            var settings = flock != null ? flock.Settings : null;
            transform.localScale = chick && settings != null ? grownScale * settings.chickScale : grownScale;
            block ??= new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                // A chick has no comb yet, and is fluffy yellow all over but her beak and legs.
                r.enabled = seen && !(chick && r.name == "Comb");
                bool fluff = chick && r.name != "Beak" && r.name != "Leg" && settings != null;
                if (fluff)
                {
                    r.GetPropertyBlock(block);
                    block.SetColor("_BaseColor", settings.chickColor);
                    r.SetPropertyBlock(block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
            foreach (var c in GetComponents<Collider>()) c.enabled = seen;
        }

        // Picked up, or set down at this spot (in the run or loose outside it).
        public void SetCarried(bool carried, Vector3 at, ulong carrier = 0)
        {
            Carried = carried;
            Carrier = carried ? carrier : 0;
            Show();
            if (carried)
            {
                wayOut.Clear();
                return;
            }
            PlaceAt(at, !flock.RunContains(at));
        }

        // Standing here, loose or in the run (a load, or news from the host).
        public void PlaceAt(Vector3 at, bool loose)
        {
            Inside = false;
            Loose = loose;
            Settled = at;
            wayOut.Clear();
            transform.position = at;
            lastPosition = at;
            peckUntil = 0f;
            PickSpot();
        }

        void Update()
        {
            if (Carried) return;
            if (alive && Hosting)
            {
                Think();
            }
            Peck();
            Cluck();
        }

        // Now and then she clucks to herself (quieter on the roost at night).
        void Cluck()
        {
            if (!alive || Time.time < nextCluck) return;
            if (nextCluck > 0f) Sfx.Play(SoundId.Cluck, head.position, Inside ? 0.4f : 1f, 0.12f);
            nextCluck = Time.time + Random.Range(5f, 16f);
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
            if (Loose)
            {
                // Out here she settles wherever dark finds her.
                if (night) return;
                Wander(settings);
                return;
            }
            if (night)
            {
                // Home to roost.
                wayOut.Clear();
                if (WalkTo(flock.Doorway.position, settings.walkSpeed) && flock.DoorOpen)
                {
                    Inside = true;
                    transform.position = roost.position;
                }
                return;
            }
            if (HeadingOut)
            {
                // Shut in after all if someone closes the gate first.
                if (!flock.GateOpen)
                {
                    wayOut.Clear();
                    PickSpot();
                    return;
                }
                if (WalkTo(wayOut[0], settings.walkSpeed))
                {
                    wayOut.RemoveAt(0);
                    if (wayOut.Count == 0) flock.GotOut(this);
                }
                return;
            }
            Wander(settings);
        }

        void Wander(Data.FlockSettings settings)
        {
            if (Time.time < peckUntil) return;
            if (WalkTo(target, settings.walkSpeed))
            {
                peckUntil = Time.time + Random.Range(settings.peckSeconds.x, settings.peckSeconds.y);
                PickSpot();
            }
        }

        void PickSpot()
        {
            if (Loose)
            {
                target = flock.SpotNear(this);
                return;
            }
            // Through an open gate now and then, on a whim.
            if (flock.GateOpen && !flock.Clock.IsNight && Random.value < flock.Settings.wanderOutChance)
            {
                HeadOut();
                return;
            }
            target = flock.SpotInRun();
        }

        // Off out of the run through the open gate.
        public void HeadOut()
        {
            if (Loose || Inside || !flock.GateOpen) return;
            wayOut.Clear();
            wayOut.AddRange(flock.WayOut());
        }

        // Through the gate and out: loose from here.
        public void GotOut()
        {
            Loose = true;
            Settled = transform.position;
            PickSpot();
        }

        public bool CanInteract(PlayerHands hands) => alive && !Inside && !Carried && hands.IsEmpty;
        public string PromptVerb(PlayerHands hands) => IsChick ? "Pick up the chick" : "Pick her up";
        public void Interact(PlayerHands hands) => flock.PickUp(this, hands);

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
