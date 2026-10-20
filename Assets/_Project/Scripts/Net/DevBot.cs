#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Food;
using GrandmasHouse.Kitchen;
using UnityEngine;

namespace GrandmasHouse.Net
{
    // Development builds only: started with "-bot", a joined player that
    // runs through the shared basics by itself (pick up, set down, eat,
    // open the fridge, go to bed) and writes each step to the log, so a
    // shared game can be tested on one computer.
    public class DevBot : MonoBehaviour
    {
        public static void StartIfAsked()
        {
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-bot") < 0)
            {
                return;
            }
            var go = new GameObject("Dev Bot");
            DontDestroyOnLoad(go);
            go.AddComponent<DevBot>();
        }

        IEnumerator Start()
        {
            PlayerNet me = null;
            while (me == null)
            {
                me = PlayerNet.All.Find(p => p.IsOwner);
                yield return null;
            }
            Log("joined as client " + me.OwnerClientId);
            yield return new WaitForSeconds(3f);
            PlayerHands hands = me.Hands;

            Pickup tomato = Loose();
            Log(tomato != null ? "found " + tomato.DisplayName + " at " + tomato.transform.position : "no food lying around");
            if (tomato != null)
            {
                Stand(me, new Vector3(tomato.transform.position.x, 0f, tomato.transform.position.z));
                hands.Handle(tomato);
                yield return new WaitForSeconds(1f);
                Log("holding it: " + hands.IsHolding(tomato));

                Stand(me, new Vector3(2.25f, 0f, 0.9f));
                yield return new WaitForSeconds(0.5f);
                hands.TakeHeld().PlaceOn(new Vector3(2.0f, 0.86f, 1.45f), 0f, 0.2f);
                yield return new WaitForSeconds(1f);
                Log("set it down at " + tomato.transform.position);
            }

            Pickup food = Loose(tomato);
            if (food != null)
            {
                Stand(me, new Vector3(food.transform.position.x, 0f, food.transform.position.z));
                hands.Handle(food);
                yield return new WaitForSeconds(1f);
                me.Needs.Set(40f, 100f);
                me.Needs.Eat(food.GetComponent<FreshFood>());
                yield return new WaitForSeconds(1f);
                Log("ate it; hunger now " + me.Needs.State.hunger.ToString("F0") + " (was 40), gone: " + (food == null || !food.gameObject.activeInHierarchy));
            }

            foreach (var door in FindObjectsByType<HingedDoor>(FindObjectsSortMode.None))
            {
                if (door.name != "Door Hinge") continue;
                door.Interact(hands);
                yield return new WaitForSeconds(1f);
                Log("fridge open: " + door.IsOpen);
            }

            yield return Kitchen(me, hands);
            yield return Garden(me, hands);

            var bed = FindAnyObjectByType<Bed>();
            if (bed != null)
            {
                Stand(me, bed.transform.position + Vector3.back * 0.9f);
                me.Needs.Set(me.Needs.State.hunger, 20f);
                bed.Interact(hands);
                yield return new WaitForSeconds(4f);
                Log("asleep: " + me.Needs.Asleep);
                // Stay in bed until the host's player joins in and the night passes.
                float giveUp = Time.time + 90f;
                while (me.Needs.Asleep && Time.time < giveUp) yield return null;
                Log("woke up; rest " + me.Needs.State.rest.ToString("F0") + ", hunger " + me.Needs.State.hunger.ToString("F0"));
            }

            // Collapse from hunger and wait for someone to feed us.
            me.Needs.Set(0f, 80f, me.Needs.Settings.collapseAfterHours);
            yield return new WaitForSeconds(1f);
            Log("collapsed: " + me.Needs.Collapsed);
            float waitForHelp = Time.time + 90f;
            while (me.Needs.Collapsed && Time.time < waitForHelp) yield return null;
            Log("came round: " + !me.Needs.Collapsed + "; hunger " + me.Needs.State.hunger.ToString("F0"));
            Log("done");
        }

        // Put the pot on a burner and light it, and set a cucumber on the board.
        IEnumerator Kitchen(PlayerNet me, PlayerHands hands)
        {
            var pot = FindAnyObjectByType<CookingPot>();
            Burner burner = null;
            if (pot != null)
            {
                Stand(me, new Vector3(pot.transform.position.x, 0f, pot.transform.position.z));
                hands.Handle(pot.GetComponent<Pickup>());
                yield return new WaitForSeconds(1f);
                foreach (var b in FindObjectsByType<Burner>(FindObjectsSortMode.None))
                {
                    if (b.CanInteract(hands)) { burner = b; break; }
                }
                if (burner != null)
                {
                    burner.Interact(hands);
                    yield return new WaitForSeconds(0.5f);
                    burner.Toggle();
                }
                yield return new WaitForSeconds(1f);
                Log("kitchen: pot on " + (burner != null ? burner.name : "no burner") + ", lit " + (burner != null && burner.On));
            }

            var board = FindAnyObjectByType<Stations.CuttingBoardStation>();
            Pickup cucumber = null;
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
            {
                if (p.DisplayName == "Cucumber" && p.CollidersOn && p.GetComponentInParent<Stations.Station>() == null) { cucumber = p; break; }
            }
            if (board != null && cucumber != null)
            {
                Stand(me, new Vector3(cucumber.transform.position.x, 0f, cucumber.transform.position.z));
                hands.Handle(cucumber);
                yield return new WaitForSeconds(1f);
                bool canPut = board.CanInteract(hands);
                if (canPut) board.Interact(hands);
                yield return new WaitForSeconds(1.5f);
                Log("kitchen: cucumber on the board: " + (cucumber.GetComponentInParent<Stations.CuttingBoardStation>() != null) + " (could put: " + canPut + ")");

                // The host chops it; the slices should land on our board too.
                float giveUp = Time.time + 60f;
                while (board.SliceCount == 0 && Time.time < giveUp) yield return new WaitForSeconds(0.5f);
                Log("kitchen: slices on the board here: " + board.SliceCount + ", cucumber gone: " + (cucumber == null || !cucumber.gameObject.activeInHierarchy));
            }
        }

        // Plant, water and weed a bed, then wait for the host to grow it and
        // pick what ripens.
        IEnumerator Garden(PlayerNet me, PlayerHands hands)
        {
            var garden = FindAnyObjectByType<Garden.GardenBed>();
            Garden.SeedPacket packet = null;
            foreach (var p in FindObjectsByType<Garden.SeedPacket>(FindObjectsSortMode.None))
            {
                if (p.HasSeeds && p.Crop.name.Contains("Tomato")) packet = p;
            }
            Garden.PlantSpot spot = null;
            foreach (var s in garden.Spots) if (s.IsEmpty) { spot = s; break; }
            if (packet == null || spot == null)
            {
                Log("garden: nothing to plant");
                yield break;
            }
            int index = System.Array.IndexOf(System.Linq.Enumerable.ToArray(garden.Spots), spot);
            Stand(me, new Vector3(packet.transform.position.x, 0f, packet.transform.position.z));
            hands.Handle(packet.GetComponent<Pickup>());
            yield return new WaitForSeconds(1f);
            packet.TakeSeed();
            garden.Plant(spot, packet.Crop, 0.35f);
            garden.Water(spot, 1f);
            int weedsBefore = garden.Weeds.Count;
            if (weedsBefore > 0) garden.PullOut(garden.Weeds[0]);
            hands.TakeHeld().Drop();
            Log("garden: planted " + packet.Crop.name + " in spot " + index + ", watered, weeds " + weedsBefore + " -> " + garden.Weeds.Count);

            // The host grows it; pick the first ripe one that shows up.
            float giveUp = Time.time + 60f;
            Pickup ripe = null;
            while (ripe == null && Time.time < giveUp)
            {
                foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                {
                    if (p.DisplayName == "Tomato" && Vector3.Distance(p.transform.position, spot.transform.position) < 0.6f && p.CollidersOn)
                    {
                        ripe = p;
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
            Log("garden: stage " + spot.Stage + " (host grew it: " + (spot.Stage >= 0) + "), ripe tomato seen: " + (ripe != null));
            if (ripe != null)
            {
                hands.Handle(ripe);
                yield return new WaitForSeconds(1f);
                Log("garden: picked a ripe tomato: " + hands.IsHolding(ripe));
                hands.TakeHeld().Drop();
            }
        }

        // Something to eat lying loose (not on a station or in a basket).
        static Pickup Loose(Pickup not = null)
        {
            foreach (var p in FindObjectsByType<Pickup>(FindObjectsSortMode.None))
            {
                if (p != not && p.GetComponent<FreshFood>() != null && p.transform.parent != null && p.transform.parent.parent == null && p.CollidersOn && p.InContainer == null && p.gameObject.activeInHierarchy)
                {
                    return p;
                }
            }
            return null;
        }

        static void Stand(PlayerNet me, Vector3 near)
        {
            var body = me.GetComponent<CharacterController>();
            body.enabled = false;
            me.transform.position = new Vector3(near.x, near.y + 0.05f, near.z) + Vector3.back * 0.6f;
            body.enabled = true;
        }

        static void Log(string message) => Debug.Log("[bot] " + message);
    }
}
#endif
