using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A little smokehouse in the yard. Hang meat (or anything fresh) on the
    // hooks, put firewood in the fire pit underneath and light it with empty
    // hands, and it smokes over most of a day. Each piece burns a few hours;
    // when the last one's gone the fire dies, so feed it or relight it if
    // the meat isn't done. Smoked food keeps for weeks.
    public class Smokehouse : Curer
    {
        [Tooltip("Pieces of firewood the fire pit holds.")]
        [SerializeField] int capacity = 4;
        [Tooltip("In-game hours one piece burns.")]
        [SerializeField] float hoursPerLog = 4f;
        [SerializeField] Renderer embers;
        [SerializeField] Light glow;

        public float FireLeft { get; private set; }
        public int Wood { get; private set; }
        public int Capacity => capacity;
        public float HoursPerLog => hoursPerLog;
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
            // Burn through the pieces in the pit, one after another; the
            // smoke cures for as long as it burned, even if it dies partway.
            float burned = 0f;
            float left = hours;
            bool outOfWood = false;
            while (left > 0f && !outOfWood)
            {
                float step = Mathf.Min(left, FireLeft);
                burned += step;
                left -= step;
                FireLeft -= step;
                if (FireLeft > 0f) break;
                if (Wood > 0)
                {
                    Wood--;
                    FireLeft = hoursPerLog;
                }
                else
                {
                    FireLeft = 0f;
                    outOfWood = true;
                }
            }
            base.Tick(burned);
            if (outOfWood) On = false;
            Show();
        }

        static bool IsWood(PlayerHands hands) => !hands.IsEmpty && hands.Held.GetComponent<Firewood>() != null;

        public override bool CanInteract(PlayerHands hands)
        {
            if (IsWood(hands)) return Wood < capacity;
            if (hands.IsEmpty) return On || Wood > 0;
            return base.CanInteract(hands);
        }

        public override string PromptVerb(PlayerHands hands)
        {
            if (IsWood(hands)) return "Put wood in the fire";
            if (hands.IsEmpty) return On ? "Put out the fire" : "Light the fire";
            return base.PromptVerb(hands);
        }

        public override void Interact(PlayerHands hands)
        {
            if (IsWood(hands))
            {
                if (Wood >= capacity) return;
                Pickup log = hands.TakeHeld();
                Wood++;
                Net.ItemNet.Remove(log.gameObject);
                Sfx.Play(SoundId.SetDown, transform.position, 0.7f);
                return;
            }
            if (!hands.IsEmpty)
            {
                base.Interact(hands);
                return;
            }
            if (On)
            {
                // Put out: what's burning now is lost; the rest stays in the pit.
                On = false;
                FireLeft = 0f;
            }
            else if (Wood > 0)
            {
                // A match to it: the first piece catches.
                Wood--;
                FireLeft = hoursPerLog;
                On = true;
            }
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

        protected override State Save() => new State { on = On, left = Mathf.Round(FireLeft * 100f) / 100f, wood = Wood };

        protected override void Load(State state)
        {
            On = state.on;
            FireLeft = state.left;
            Wood = state.wood;
        }
    }
}
