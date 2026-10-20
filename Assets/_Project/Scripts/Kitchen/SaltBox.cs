using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A wooden box of salt in the root cellar for curing meat. Pour salt in
    // (from the salt), then pack meat into it; in about three days it's
    // salted through and keeps all winter. Each piece cured uses up a scoop
    // of the salt; with no salt in the box nothing cures.
    public class SaltBox : Curer
    {
        [SerializeField] int maxSalt = 6;
        [Tooltip("The salt's surface, raised as it fills.")]
        [SerializeField] Transform saltLevel;
        [SerializeField] float fullHeight = 0.18f;

        public int Salt { get; private set; }
        protected override Perishable.Cure Makes => Perishable.Cure.Salted;
        protected override bool Working => Salt > 0;
        protected override string PutVerb => "Pack it in the salt";

        // Only meat is salted down.
        protected override bool Takes(Pickup held) =>
            base.Takes(held) && held.TryGetComponent(out FreshFood food) && food.IsMeat;

        protected override void Finished(Perishable food)
        {
            Salt = Mathf.Max(0, Salt - 1);
            Show();
        }

        static bool IsSalt(PlayerHands hands) =>
            !hands.IsEmpty && hands.Held.TryGetComponent(out Seasoning s) && s.Kind == Seasoning.Type.Salt;

        public override bool CanInteract(PlayerHands hands) =>
            (IsSalt(hands) && Salt < maxSalt) || base.CanInteract(hands);

        public override string PromptVerb(PlayerHands hands) =>
            IsSalt(hands) ? "Pour in salt" : base.PromptVerb(hands);

        public override void Interact(PlayerHands hands)
        {
            if (!IsSalt(hands))
            {
                base.Interact(hands);
                return;
            }
            Salt = Mathf.Min(maxSalt, Salt + 1);
            Sfx.Play(SoundId.SaltShake, transform.position);
            Show();
        }

        protected override void Show()
        {
            if (saltLevel == null) return;
            saltLevel.gameObject.SetActive(Salt > 0);
            saltLevel.localScale = new Vector3(saltLevel.localScale.x, Mathf.Max(0.001f, fullHeight * Salt / maxSalt), saltLevel.localScale.z);
        }

        protected override State Save() => new State { salt = Salt };
        protected override void Load(State state) => Salt = state.salt;
    }
}
