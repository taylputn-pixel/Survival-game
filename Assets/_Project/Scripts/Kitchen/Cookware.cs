using GrandmasHouse.Carrying;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A pot or the canner: carried like anything else, kept in the pantry,
    // and set on a burner to cook. Its station only works on the stove.
    [RequireComponent(typeof(Pickup))]
    public class Cookware : MonoBehaviour
    {
        [Tooltip("Where you take hold of it to lift it off the stove.")]
        [SerializeField] Transform handle;

        public Burner Burner { get; private set; }
        public bool OnStove => Burner != null;
        public bool IsHot => Burner != null && Burner.On;
        public Transform Handle => handle;
        public Pickup Pickup { get; private set; }

        void Awake()
        {
            Pickup = GetComponent<Pickup>();
            Pickup.HeldChanged += held =>
            {
                if (held && Burner != null)
                {
                    Burner.Release();
                    Burner = null;
                }
            };
        }

        public void SetBurner(Burner burner) => Burner = burner;

        // Off the stove and into your hands.
        public void LiftOff(PlayerHands hands)
        {
            if (Burner != null)
            {
                Burner.Release();
                Burner = null;
            }
            hands.Give(Pickup);
        }
    }
}
