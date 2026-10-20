using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A door or lid that swings on a hinge: the refrigerator door, the chest
    // freezer lid. Press Interact to open or close it. This sits on the hinge;
    // the door's colliders are its children.
    public class HingedDoor : NetworkBehaviour, IInteractable, ISaveable, ISharedUse
    {
        [Tooltip("Unique save key for this door, like \"fridge-door\".")]
        [SerializeField] string saveKey = "door";
        [SerializeField] string noun = "the door";
        [Tooltip("The hinge's local rotation when open, in degrees.")]
        [SerializeField] Vector3 openAngles = new Vector3(0f, -100f, 0f);
        [SerializeField] float swingSeconds = 0.35f;

        readonly NetworkVariable<bool> shared = new NetworkVariable<bool>(false);
        Quaternion closed;
        Quaternion open;
        bool isOpen;
        float t;

        public bool IsOpen => isOpen;

        void Awake()
        {
            closed = transform.localRotation;
            open = closed * Quaternion.Euler(openAngles);
        }

        public bool CanInteract(PlayerHands hands) => true;

        public string PromptVerb(PlayerHands hands) => (isOpen ? "Close " : "Open ") + noun;

        public void Interact(PlayerHands hands)
        {
            if (IsSpawned)
            {
                ToggleRpc();
            }
            else
            {
                Set(!isOpen);
            }
        }

        // In a shared game the host keeps whether it's open.
        [Rpc(SendTo.Server)]
        void ToggleRpc() => Set(!isOpen);

        public override void OnNetworkSpawn()
        {
            shared.OnValueChanged += (_, now) => Swing(now);
            if (!IsServer)
            {
                Swing(shared.Value);
            }
            else
            {
                shared.Value = isOpen;
            }
        }

        public void Set(bool opened)
        {
            Swing(opened);
            if (IsSpawned && IsServer)
            {
                shared.Value = opened;
            }
        }

        void Swing(bool opened)
        {
            if (opened == isOpen) return;
            isOpen = opened;
            Sfx.Play(SoundId.KnobClick, transform.position);
        }

        void Update()
        {
            float target = isOpen ? 1f : 0f;
            if (Mathf.Approximately(t, target))
            {
                return;
            }
            t = Mathf.MoveTowards(t, target, Time.deltaTime / swingSeconds);
            transform.localRotation = Quaternion.Slerp(closed, open, Mathf.SmoothStep(0f, 1f, t));
        }

        public string SaveKey => saveKey;
        public string Capture() => isOpen ? "1" : "0";

        public void Restore(string json)
        {
            isOpen = json == "1";
            t = isOpen ? 1f : 0f;
            transform.localRotation = isOpen ? open : closed;
            if (IsSpawned && IsServer)
            {
                shared.Value = isOpen;
            }
        }
    }
}
