using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A little smokehouse in the yard. Hang meat (or anything fresh) on the
    // hooks, light the fire underneath with empty hands, and it smokes over
    // most of a day. The fire burns down on its own after `fireHours`;
    // relight it if the meat isn't done. Smoked food keeps for weeks.
    public class Smokehouse : Curer
    {
        [Tooltip("In-game hours a fire lasts.")]
        [SerializeField] float fireHours = 10f;
        [SerializeField] Renderer embers;
        [SerializeField] Light glow;

        public float FireLeft { get; private set; }
        protected override Perishable.Cure Makes => Perishable.Cure.Smoked;
        protected override string PutVerb => "Hang it up";

        MaterialPropertyBlock block;

        protected override void Start()
        {
            block = new MaterialPropertyBlock();
            base.Start();
        }

        protected override void Tick(float hours)
        {
            if (!On)
            {
                return;
            }
            base.Tick(Mathf.Min(hours, FireLeft));
            FireLeft -= hours;
            if (FireLeft <= 0f)
            {
                FireLeft = 0f;
                On = false;
                Show();
            }
        }

        public override bool CanInteract(PlayerHands hands) => hands.IsEmpty || base.CanInteract(hands);

        public override string PromptVerb(PlayerHands hands)
        {
            if (hands.IsEmpty) return On ? "Put out the fire" : "Light the fire";
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
            FireLeft = On ? fireHours : 0f;
            Sfx.Play(SoundId.Paper, transform.position);
            Show();
        }

        protected override void Show()
        {
            if (glow != null) glow.enabled = On;
            if (embers != null && block != null)
            {
                embers.GetPropertyBlock(block);
                block.SetColor("_BaseColor", On ? new Color(1f, 0.42f, 0.12f) : new Color(0.16f, 0.14f, 0.13f));
                embers.SetPropertyBlock(block);
            }
        }

        protected override State Save() => new State { on = On, left = Mathf.Round(FireLeft * 100f) / 100f };

        protected override void Load(State state)
        {
            On = state.on;
            FireLeft = state.left;
        }
    }
}
