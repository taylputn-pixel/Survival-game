using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using GrandmasHouse.Garden;
using GrandmasHouse.Interaction;
using GrandmasHouse.Net;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // The hens' feeder and waterer (Kind). Look at the feeder holding the
    // feed sack to scoop some in, or holding any produce to toss it in (a
    // hen doesn't mind if it's gone bad), or the treat pail to tip in the
    // hornworms. Look at the waterer holding a watering can to fill it.
    public class Feeder : MonoBehaviour, IInteractable, ISharedUse
    {
        public enum Kind { Food, Water }

        [SerializeField] Flock flock;
        [SerializeField] Kind kind = Kind.Food;

        public bool CanInteract(PlayerHands hands)
        {
            if (hands.IsEmpty) return false;
            Pickup held = hands.Held;
            if (kind == Kind.Water)
            {
                return held.TryGetComponent(out WateringCan can) && !can.IsEmpty && !flock.WatererFull;
            }
            if (flock.FeederFull) return false;
            if (held.TryGetComponent(out FeedSack sack)) return sack.HasFeed;
            if (held.TryGetComponent(out TreatPail pail)) return pail.Worms > 0;
            return held.GetComponent<FreshFood>() != null;
        }

        public string PromptVerb(PlayerHands hands)
        {
            Pickup held = hands.Held;
            if (kind == Kind.Water) return "Fill the waterer";
            if (held.GetComponent<FeedSack>() != null) return "Scoop in feed";
            if (held.GetComponent<TreatPail>() != null) return "Tip in the hornworms";
            return "Toss to the hens";
        }

        public void Interact(PlayerHands hands)
        {
            Pickup held = hands.Held;
            var settings = flock.Settings;
            if (kind == Kind.Water)
            {
                var can = held.GetComponent<WateringCan>();
                float room = 1f - flock.Water;
                float poured = Mathf.Min(can.Level, room / settings.waterPerCan);
                can.Fill(-poured);
                flock.AddWater(poured * settings.waterPerCan);
                Share(held);
                return;
            }
            if (held.TryGetComponent(out FeedSack sack))
            {
                sack.TakeScoop();
                flock.AddFeed(settings.scoop);
                Share(held);
                return;
            }
            if (held.TryGetComponent(out TreatPail pail))
            {
                flock.AddFeed(pail.Worms * settings.feedPerWorm);
                pail.Empty();
                Share(held);
                return;
            }
            var food = held.GetComponent<FreshFood>();
            var spoiled = held.GetComponent<Perishable>();
            flock.AddFeed(spoiled != null && spoiled.IsRotten ? settings.spoiledFeed : food.Nutrition * settings.feedPerFood);
            ItemNet.Remove(hands.Take(held).gameObject);
        }

        static void Share(Pickup held)
        {
            if (held.TryGetComponent(out ItemNet net)) net.Share();
        }
    }
}
