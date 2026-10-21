using System;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Vehicles
{
    // Grandma's old pickup. Get in with empty hands and drive: the move
    // stick (or keys) is the gas, brake and wheel, and you can still look
    // around. Interact gets you out by the driver's door. Logs ride in the
    // bed (TruckBed). In a shared game the driver's computer drives it and
    // everyone else sees it go; one driver at a time.
    [RequireComponent(typeof(Rigidbody))]
    public class Truck : MonoBehaviour, IInteractable, ISharedUse, ISeat, ISaveable
    {
        [SerializeField] Transform seat;
        [Tooltip("Where you step out: the driver's side first, then the other.")]
        [SerializeField] Transform[] doors;
        [SerializeField] Transform[] wheels;
        [SerializeField] float topSpeed = 11f;
        [SerializeField] float reverseSpeed = 4f;
        [SerializeField] float acceleration = 4f;
        [SerializeField] float braking = 9f;
        [SerializeField] float coastDrag = 1.5f;
        [Tooltip("Degrees a second it turns at full lock and speed.")]
        [SerializeField] float turnRate = 55f;
        [SerializeField] float wheelRadius = 0.35f;
        [Tooltip("The box it takes up, above wheel height (for bumping into things).")]
        [SerializeField] Vector3 bodyCenter = new Vector3(0f, 1.1f, 0f);
        [SerializeField] Vector3 bodyHalfSize = new Vector3(0.95f, 0.6f, 2.35f);
        [SerializeField] string saveKey = "truck";

        public float Speed { get; private set; }
        public PlayerHands Driver { get; private set; }
        public bool Occupied => Driver != null || (net != null && net.Taken);
        public string LeaveVerb => "Get out";

        Rigidbody body;
        TruckNet net;
        InputAction moveAction;
        Quaternion lastRotation;
        SoundLoop engine;

        void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            net = GetComponent<TruckNet>();
            moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
            lastRotation = transform.rotation;
        }

        void Start() => engine = Sfx.Loop(SoundId.Simmer, transform);

        // ---- Getting in and out ----

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty && !Occupied && hands.Seat == null;
        public string PromptVerb(PlayerHands hands) => "Drive the truck";

        public void Interact(PlayerHands hands)
        {
            if (net != null && net.IsSpawned)
            {
                net.Ask(hands);
                return;
            }
            SitIn(hands);
        }

        // Behind the wheel (on this computer, once the truck is ours to drive).
        public void SitIn(PlayerHands hands)
        {
            Driver = hands;
            hands.Seat = this;
            hands.GetComponent<CharacterController>().enabled = false;
            hands.Controller.Seated = true;
            lastRotation = transform.rotation;
            Carry();
            Sfx.Play(SoundId.KnobClick, transform.position);
        }

        public void Leave(PlayerHands hands)
        {
            if (Driver != hands) return;
            Speed = 0f;
            Driver = null;
            hands.Seat = null;
            hands.Controller.Seated = false;
            Vector3 outside = DoorOut();
            hands.transform.SetPositionAndRotation(outside, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            hands.GetComponent<CharacterController>().enabled = true;
            if (net != null && net.IsSpawned) net.Release();
        }

        // A door with room to stand.
        Vector3 DoorOut()
        {
            foreach (var door in doors)
            {
                Vector3 at = door.position;
                if (!Physics.CheckCapsule(at + Vector3.up * 0.4f, at + Vector3.up * 1.6f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) return at;
            }
            return doors.Length > 0 ? doors[0].position + Vector3.up * 0.5f : transform.position + transform.right * -2f;
        }

        // ---- Driving ----

        void Update()
        {
            float spin = Speed * Time.deltaTime / wheelRadius * Mathf.Rad2Deg;
            foreach (var wheel in wheels) wheel.Rotate(0f, spin, 0f, Space.Self);
            if (engine != null) engine.Set(Driver != null ? Mathf.Lerp(0.12f, 0.35f, Mathf.Abs(Speed) / topSpeed) : 0f);
            if (Driver == null) return;
            Drive(Driver.Controller.InputEnabled ? moveAction.ReadValue<Vector2>() : Vector2.zero, Time.deltaTime);
        }

        void LateUpdate()
        {
            if (Driver != null) Carry();
        }

        // Gas and brake on y, the wheel on x.
        public void Drive(Vector2 input, float dt)
        {
            float target = input.y >= 0f ? input.y * topSpeed : input.y * reverseSpeed;
            bool braking = (Speed > 0.1f && input.y < -0.1f) || (Speed < -0.1f && input.y > 0.1f);
            float rate = braking ? this.braking : Mathf.Abs(input.y) > 0.05f ? acceleration : coastDrag;
            Speed = Mathf.MoveTowards(Speed, Mathf.Abs(input.y) > 0.05f ? target : 0f, rate * dt);

            // It only turns while it's rolling, and backwards it turns the other way.
            float grip = Mathf.Clamp(Speed / 3f, -1f, 1f);
            float turn = input.x * turnRate * grip * dt;
            Quaternion turned = Quaternion.Euler(0f, transform.eulerAngles.y + turn, 0f);
            Vector3 step = turned * Vector3.forward * Speed * dt;

            // Stop short of anything in the way.
            if (step.sqrMagnitude > 0f && Blocked(turned, step))
            {
                Speed = 0f;
                step = Vector3.zero;
            }
            transform.SetPositionAndRotation(transform.position + step, turned);
            body.position = transform.position;
            body.rotation = transform.rotation;
        }

        // Anything solid in the truck's path (not the ground under it, not
        // loose things it would just shove, not itself).
        bool Blocked(Quaternion facing, Vector3 step)
        {
            Vector3 center = transform.position + facing * bodyCenter;
            Vector3 direction = step.normalized;
            foreach (var hit in Physics.BoxCastAll(center, bodyHalfSize, direction, facing, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<Pickup>() != null) continue;
                // Already touching: only blocked going further into it.
                if (hit.distance <= 0f && Vector3.Dot(direction, hit.collider.bounds.center - center) <= 0f) continue;
                return true;
            }
            return false;
        }

        // The driver rides along, still free to look about.
        void Carry()
        {
            Transform player = Driver.transform;
            Quaternion look = Quaternion.Inverse(lastRotation) * player.rotation;
            player.SetPositionAndRotation(seat.position, transform.rotation * look);
            lastRotation = transform.rotation;
        }

        // ---- Saving (where it's parked) ----

        [Serializable]
        class State
        {
            public Vector3 position;
            public float yaw;
        }

        public string SaveKey => saveKey;
        public string Capture() => JsonUtility.ToJson(new State { position = transform.position, yaw = transform.eulerAngles.y });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            transform.SetPositionAndRotation(state.position, Quaternion.Euler(0f, state.yaw, 0f));
            lastRotation = transform.rotation;
        }
    }
}
