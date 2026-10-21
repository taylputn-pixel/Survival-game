using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // On the packing towel: with empty hands, the second action puts it all away.
    public class CanningPutAway : MonoBehaviour, IAltInteractable, ISharedUse
    {
        public bool CanAltInteract(PlayerHands hands) => hands.IsEmpty && CanningKit.Instance != null && CanningKit.Instance.IsSetUp;
        public void AltInteract(PlayerHands hands) => CanningKit.Instance.PutAway(hands);
        public string AltVerb(PlayerHands hands) => "Put the canning things away";
    }
}
