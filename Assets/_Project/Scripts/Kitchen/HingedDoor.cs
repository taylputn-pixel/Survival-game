using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A door or lid that swings on a hinge: the refrigerator door, the chest
    // freezer lid. Press Interact to open or close it. This sits on the hinge;
    // the door's colliders are its children.
    public class HingedDoor : MonoBehaviour, IInteractable, ISaveable
    {
        [Tooltip("Unique save key for this door, like \"fridge-door\".")]
        [SerializeField] string saveKey = "door";
        [SerializeField] string noun = "the door";
        [Tooltip("The hinge's local rotation when open, in degrees.")]
        [SerializeField] Vector3 openAngles = new Vector3(0f, -100f, 0f);
        [SerializeField] float swingSeconds = 0.35f;

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

        public void Interact(PlayerHands hands) => Set(!isOpen);

        public void Set(bool opened)
        {
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
        }
    }
}
