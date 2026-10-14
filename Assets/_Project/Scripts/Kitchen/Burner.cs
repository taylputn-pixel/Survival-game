using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // One burner on the stove. Set a pot or the canner on it, and turn its
    // knob to heat. The coil glows while it's on. Lifting the pot off turns
    // the burner off.
    public class Burner : MonoBehaviour, IInteractable, ISaveable, IAfterLoad
    {
        [Tooltip("Where cookware sits on this burner.")]
        [SerializeField] Transform spot;
        [SerializeField] Transform knob;
        [SerializeField] Renderer coil;
        [SerializeField] Color offColor = new Color(0.12f, 0.12f, 0.13f);
        [SerializeField] Color onColor = new Color(0.85f, 0.32f, 0.16f);
        [SerializeField] float glideSeconds = 0.35f;

        public bool On { get; private set; }
        public Cookware Occupant { get; private set; }
        public Transform Knob => knob;
        public Transform Spot => spot;

        MaterialPropertyBlock block;
        float glow;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            ShowHeat();
        }

        public bool CanInteract(PlayerHands hands)
        {
            return Occupant == null && !hands.IsEmpty && hands.Held.GetComponent<Cookware>() != null;
        }

        public string PromptVerb(PlayerHands hands) => "Set on the stove";

        public void Interact(PlayerHands hands)
        {
            Pickup item = hands.TakeHeld();
            item.AttachTo(spot, Vector3.zero, Quaternion.identity, glideSeconds, collidersOn: true);
            Hold(item.GetComponent<Cookware>());
        }

        public void Hold(Cookware cookware)
        {
            Occupant = cookware;
            cookware.SetBurner(this);
        }

        public void Release()
        {
            Occupant = null;
            SetOn(false);
        }

        public void Toggle() => SetOn(!On);

        public void SetOn(bool on)
        {
            if (on != On)
            {
                Sfx.Play(SoundId.KnobClick, knob.position);
            }
            On = on;
            ShowKnob();
        }

        void ShowKnob() => knob.localRotation = Quaternion.Euler(0f, 0f, On ? -90f : 0f);

        void Update()
        {
            // The coil warms up and cools down gradually.
            float target = On ? 1f : 0f;
            if (!Mathf.Approximately(glow, target))
            {
                glow = Mathf.MoveTowards(glow, target, Time.deltaTime / 1.5f);
                ShowHeat();
            }
        }

        void ShowHeat()
        {
            coil.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.Lerp(offColor, onColor, glow));
            coil.SetPropertyBlock(block);
        }

        public string SaveKey => "burner:" + name;
        public string Capture() => On ? "1" : "0";

        public void Restore(string json)
        {
            On = json == "1";
            ShowKnob();
            glow = On ? 1f : 0f;
            ShowHeat();
        }

        // Cookware left on this burner sits here again.
        public void AfterLoad()
        {
            var cookware = spot.GetComponentInChildren<Cookware>();
            Occupant = null;
            if (cookware != null)
            {
                Hold(cookware);
            }
        }
    }
}
