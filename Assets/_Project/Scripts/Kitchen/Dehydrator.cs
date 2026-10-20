using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Grandma's electric food dehydrator. Lay produce on the tray (look at
    // it holding the food), then turn it on with empty hands. Warm air dries
    // what's on the tray over half a day; dried food keeps many times
    // longer and goes straight into the soup pot. It runs on power.
    public class Dehydrator : Curer
    {
        [SerializeField] Transform dial;
        [SerializeField] Renderer lamp;

        public float DryHours => CureHours;
        protected override Perishable.Cure Makes => Perishable.Cure.Dried;
        protected override string PutVerb => "Lay it on the tray";

        SoundLoop hum;
        MaterialPropertyBlock block;

        protected override void Start()
        {
            hum = Sfx.Loop(SoundId.Simmer, transform);
            block = new MaterialPropertyBlock();
            base.Start();
        }

        public override bool CanInteract(PlayerHands hands) => hands.IsEmpty || base.CanInteract(hands);

        public override string PromptVerb(PlayerHands hands)
        {
            if (hands.IsEmpty) return On ? "Turn off the dehydrator" : "Turn on the dehydrator";
            return base.PromptVerb(hands);
        }

        public override void Interact(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                base.Interact(hands);
                return;
            }
            On = !On;
            Sfx.Play(SoundId.KnobClick, transform.position);
            Show();
        }

        protected override void Show()
        {
            if (hum != null) hum.Set(On ? 0.25f : 0f);
            if (dial != null) dial.localRotation = Quaternion.Euler(0f, 0f, On ? -120f : 0f);
            if (lamp != null && block != null)
            {
                lamp.GetPropertyBlock(block);
                block.SetColor("_BaseColor", On ? new Color(1f, 0.55f, 0.2f) : new Color(0.25f, 0.2f, 0.18f));
                lamp.SetPropertyBlock(block);
            }
        }
    }
}
