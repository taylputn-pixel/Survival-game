using System;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Animals
{
    // The hens and their coop. Each night every hen eats and drinks from the
    // feeder and waterer; a fed and watered hen lays an egg in a nest box
    // by morning (less often in fall and winter), and one that goes without
    // for a few days dies. Hens go in at dusk; leave the coop door open at
    // night, or shut a hen out, and the fox may take one (it leaves
    // feathers). Leave the run gate open and hens wander out; a hen out of
    // the run is loose (she fends for herself, lays nothing you'll find and
    // is the fox's first pick) until someone carries her back. In a shared
    // game the host keeps the flock; anyone can feed and water it
    // (SharedState).
    public class Flock : MonoBehaviour, ISaveable
    {
        [SerializeField] FlockSettings settings;
        [SerializeField] DayClock clock;
        [Tooltip("For the month: hens lay less in winter.")]
        [SerializeField] CalendarSettings calendar;
        [SerializeField] Hen[] hens;
        [SerializeField] HingedDoor coopDoor;
        [Tooltip("The run's gate; left open, hens may wander out.")]
        [SerializeField] HingedDoor gate;
        [Tooltip("Just inside the gate, then just outside it: the way out.")]
        [SerializeField] Transform gateInside;
        [SerializeField] Transform gateOutside;
        [Tooltip("Where a hen goes in and out.")]
        [SerializeField] Transform doorway;
        [Tooltip("The run's floor, inside the fence (a box: its center and size).")]
        [SerializeField] BoxCollider run;
        [SerializeField] Transform[] nests;
        [SerializeField] Pickup eggPrefab;
        [Tooltip("What a hen taken for the pot gives.")]
        [SerializeField] Pickup chickenPrefab;
        [SerializeField] Transform feedLevel;
        [SerializeField] Transform waterLevel;
        [Tooltip("Shown for a day where the fox took a hen.")]
        [SerializeField] GameObject feathers;

        public float Feed { get; private set; } = 0.6f;
        public float Water { get; private set; } = 0.6f;
        public FlockSettings Settings => settings;
        public DayClock Clock => clock;
        public bool DoorOpen => coopDoor != null && coopDoor.IsOpen;
        public bool GateOpen => gate != null && gate.IsOpen && gateInside != null && gateOutside != null;
        public Transform Doorway => doorway;
        public int Alive { get { int n = 0; foreach (var h in hens) if (h.Alive) n++; return n; } }
        // Something happened overnight, for the player to find (and tests).
        public event Action<string> Overnight;

        int feathersDay = -1;
        float nextLostCheck;

        void OnEnable() => clock.NewDay += Night;
        void OnDisable() => clock.NewDay -= Night;

        void Update()
        {
            FreeLostHens();
            if (feedLevel != null) feedLevel.localScale = new Vector3(1f, Mathf.Max(0.02f, Feed), 1f);
            if (waterLevel != null) waterLevel.localScale = new Vector3(1f, Mathf.Max(0.02f, Water), 1f);
            if (feathers != null) feathers.SetActive(feathersDay == clock.Day);
        }

        // Inside the run's fence (from above).
        public bool RunContains(Vector3 point)
        {
            Vector3 local = run.transform.InverseTransformPoint(point) - run.center;
            Vector3 half = run.size / 2f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.z) <= half.z;
        }

        // Through the gate: just inside it, then just outside.
        public Vector3[] WayOut()
        {
            float y = run.transform.position.y;
            Vector3 a = gateInside.position, b = gateOutside.position;
            return new[] { new Vector3(a.x, y, a.z), new Vector3(b.x, y, b.z) };
        }

        // Through the gate and loose.
        public void GotOut(Hen hen) => hen.GotOut();

        // Somewhere near where a loose hen settled that she can walk to
        // without going through a fence or a wall, and not back into the run.
        public Vector3 SpotNear(Hen hen)
        {
            Vector3 from = hen.transform.position;
            for (int tries = 0; tries < 8; tries++)
            {
                Vector2 step = UnityEngine.Random.insideUnitCircle * settings.looseRange;
                Vector3 to = hen.Settled + new Vector3(step.x, 0f, step.y);
                if (RunContains(to) || !OnGround(ref to, hen.Settled.y)) continue;
                if (Blocked(from, to)) continue;
                return to;
            }
            return from;
        }

        static bool OnGround(ref Vector3 point, float level)
        {
            if (!Physics.Raycast(point + Vector3.up * 0.6f, Vector3.down, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < 0.7f || Mathf.Abs(hit.point.y - level) > 0.3f) return false;
            point.y = hit.point.y;
            return true;
        }

        static bool Blocked(Vector3 from, Vector3 to)
        {
            Vector3 up = Vector3.up * 0.15f;
            foreach (var hit in Physics.RaycastAll(from + up, to - from, Vector3.Distance(from, to) + 0.15f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<Hen>() != null || hit.collider.GetComponentInParent<Pickup>() != null) continue;
                return true;
            }
            return false;
        }

        // A random spot on the run's floor for a hen to wander to.
        public Vector3 SpotInRun()
        {
            Vector3 half = run.size / 2f;
            var local = new Vector3(UnityEngine.Random.Range(-half.x, half.x) * 0.9f, 0f, UnityEngine.Random.Range(-half.z, half.z) * 0.9f);
            Vector3 world = run.transform.TransformPoint(run.center + local);
            world.y = run.transform.position.y;
            return world;
        }

        // ---- Feeding (anyone, any time) ----

        public bool FeederFull => Feed >= 1f;
        public bool WatererFull => Water >= 1f;

        public void AddFeed(float amount) => Feed = Mathf.Clamp01(Feed + amount);
        public void AddWater(float amount) => Water = Mathf.Clamp01(Water + amount);

        // ---- Overnight (the host) ----

        // This computer's player in a shared game (0 playing alone, as the host is).
        static ulong LocalId => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening ? NetworkManager.Singleton.LocalClientId : 0;

        // A hen no one is really holding any more (a game loaded with her in
        // someone's arms, or a friend who left with her) goes back to the run.
        void FreeLostHens()
        {
            if (Time.time < nextLostCheck) return;
            nextLostCheck = Time.time + 1f;
            LiveHen[] held = null;
            foreach (var hen in hens)
            {
                if (!hen.Alive || !hen.Carried) continue;
                bool lost;
                if (hen.Carrier == LocalId)
                {
                    held ??= FindObjectsByType<LiveHen>(FindObjectsSortMode.None);
                    lost = Array.Find(held, l => l.Hen == hen) == null;
                }
                else
                {
                    // Only the host decides about a friend's hen; playing alone, no friend has her.
                    bool shared = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
                    lost = Hosting && (!shared || !NetworkManager.Singleton.ConnectedClients.ContainsKey(hen.Carrier));
                }
                if (lost) LetGo(hen, hen.transform.position);
            }
        }

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        void Night(int day)
        {
            if (!Hosting) return;
            Live(UnityEngine.Random.value);
        }

        // One night for the flock. The roll decides the fox.
        public void Live(float foxRoll)
        {
            int laid = 0, starved = 0;
            float layChance = calendar != null ? settings.LayChance(calendar.DateOf(clock.Day).month) : 1f;
            foreach (var hen in hens)
            {
                // A hen out of the run forages for herself and lays where no one finds it.
                if (!hen.Alive || hen.Loose || hen.Carried) continue;
                bool fed = Feed >= settings.feedPerHen && Water >= settings.waterPerHen;
                if (fed)
                {
                    Feed -= settings.feedPerHen;
                    Water -= settings.waterPerHen;
                    hen.DaysHungry = 0;
                    if ((layChance >= 1f || UnityEngine.Random.value < layChance) && Lay()) { laid++; DayCycle.YearTally.Add(DayCycle.YearTally.Count.Egg); }
                }
                else if (++hen.DaysHungry >= settings.daysToStarve)
                {
                    hen.Alive = false;
                    starved++;
                }
            }

            // The fox comes for a hen left out, or through a door left open.
            bool shutOut = false;
            foreach (var hen in hens) if (hen.Alive && !hen.Inside) shutOut = true;
            float chance = shutOut ? settings.foxChanceShutOut : DoorOpen ? settings.foxChanceDoorOpen : 0f;
            string taken = null;
            if (foxRoll < chance)
            {
                Hen victim = null;
                // Loose in the yard first, then one left out by the coop.
                foreach (var hen in hens) if (hen.Alive && (victim == null || Exposed(hen) > Exposed(victim))) victim = hen;
                if (victim != null)
                {
                    victim.Alive = false;
                    taken = victim.name;
                    feathersDay = clock.Day;
                }
            }
            Overnight?.Invoke($"laid {laid}, starved {starved}" + (taken != null ? ", the fox took " + taken : ""));
        }

        static int Exposed(Hen hen) => hen.Loose ? 2 : hen.Inside ? 0 : 1;

        // ---- Taking a hen for the pot ----

        // Into your arms. Lay her on the chopping block for the axe.
        public void PickUp(Hen hen, PlayerHands hands)
        {
            if (!hen.Alive || hen.Carried) return;
            var live = LiveHen.Make(hen, this);
            hen.SetCarried(true, hen.transform.position, LocalId);
            hands.Give(live.Pickup);
            Sfx.Play(SoundId.Cluck, hen.transform.position, 1f, 0.2f);
        }

        // Set down anywhere but the block, she stays where she's put: back
        // in the run if that's where, loose if not.
        public void LetGo(Hen hen, Vector3 at)
        {
            if (!hen.Alive) return;
            hen.SetCarried(false, at);
            Sfx.Play(SoundId.Cluck, hen.transform.position, 1f, 0.2f);
        }

        // The axe on the block: she's gone for good and leaves a chicken
        // there. The host does it; anyone else asks the host.
        public void Butcher(Hen hen, Vector3 at)
        {
            int index = Array.IndexOf(hens, hen);
            if (index < 0 || !hen.Alive) return;
            if (!Hosting && TryGetComponent(out FlockNet net))
            {
                net.Butcher(index, at);
                return;
            }
            Butcher(index, at);
        }

        public void Butcher(int index, Vector3 at)
        {
            Hen hen = hens[index];
            if (!hen.Alive) return;
            hen.Alive = false;
            hen.SetCarried(false, hen.transform.position);
            Pickup bird = Instantiate(chickenPrefab, at + Vector3.up * 0.15f, Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
            bird.name = chickenPrefab.name;
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && bird.TryGetComponent(out NetworkObject shared))
            {
                shared.Spawn();
            }
        }

        // An egg in the first nest box with room.
        bool Lay()
        {
            foreach (var nest in nests)
            {
                int eggs = nest.GetComponentsInChildren<Pickup>().Length;
                if (eggs >= settings.eggsPerNest) continue;
                Pickup egg = Instantiate(eggPrefab, nest.position, Quaternion.identity);
                egg.name = eggPrefab.name;
                float across = (eggs - 1) * 0.045f;
                egg.AttachTo(nest, new Vector3(across, egg.RestHalfHeight, 0f), Quaternion.Euler(0f, 0f, 90f), 0f, collidersOn: true);
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && egg.TryGetComponent(out NetworkObject shared))
                {
                    shared.Spawn();
                }
                return true;
            }
            return false;
        }

        // ---- Saving (and sharing) ----

        [Serializable]
        class State
        {
            public float feed;
            public float water;
            public List<bool> alive = new List<bool>();
            public List<int> hungry = new List<int>();
            // Out of the run in someone's arms, and whose (a client id).
            public List<bool> carried = new List<bool>();
            public List<long> carrier = new List<long>();
            // Out of the run, and where she was set down or got out to.
            public List<bool> loose = new List<bool>();
            public List<Vector3> settled = new List<Vector3>();
            public int feathersDay = -1;
        }

        public string SaveKey => "flock";

        public string Capture()
        {
            var state = new State { feed = Mathf.Round(Feed * 1000f) / 1000f, water = Mathf.Round(Water * 1000f) / 1000f, feathersDay = feathersDay };
            foreach (var hen in hens)
            {
                state.alive.Add(hen.Alive);
                state.hungry.Add(hen.DaysHungry);
                state.carried.Add(hen.Carried);
                state.carrier.Add((long)hen.Carrier);
                state.loose.Add(hen.Loose);
                Vector3 at = hen.Settled;
                state.settled.Add(new Vector3(Mathf.Round(at.x * 100f) / 100f, Mathf.Round(at.y * 100f) / 100f, Mathf.Round(at.z * 100f) / 100f));
            }
            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            Feed = state.feed;
            Water = state.water;
            feathersDay = state.feathersDay;
            for (int i = 0; i < hens.Length && i < state.alive.Count; i++)
            {
                hens[i].Alive = state.alive[i];
                hens[i].DaysHungry = state.hungry[i];
                // Picked up or let go on another computer.
                bool carried = i < state.carried.Count && state.carried[i];
                ulong carrier = i < state.carrier.Count ? (ulong)state.carrier[i] : 0;
                bool loose = i < state.loose.Count && state.loose[i];
                Vector3 settled = i < state.settled.Count ? state.settled[i] : hens[i].Settled;
                if (carried != hens[i].Carried || (carried && carrier != hens[i].Carrier))
                {
                    hens[i].SetCarried(carried, carried ? hens[i].transform.position : settled, carrier);
                }
                else if (!carried && (loose != hens[i].Loose || (settled - hens[i].Settled).sqrMagnitude > 0.0004f))
                {
                    // Set down or got out on another computer, or a loaded game.
                    hens[i].PlaceAt(settled, loose);
                }
            }
        }
    }
}
