using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Net
{
    // An item in a shared game. The host decides who gets it when two
    // people reach for the same tomato; whoever holds it moves it, and
    // everyone else sees it follow. When it's put down, its state (how full
    // the jar is, how fresh the tomato) goes out to everyone.
    [RequireComponent(typeof(Pickup))]
    public class ItemNet : NetworkBehaviour
    {
        public const ulong NoOne = ulong.MaxValue;
        const char Separator = '\u001f';

        static readonly List<ItemNet> all = new List<ItemNet>();

        readonly NetworkVariable<ulong> holder = new NetworkVariable<ulong>(NoOne);
        Pickup pickup;

        public ulong Holder => holder.Value;
        // Shared with other players right now.
        public bool Live => IsSpawned;

        void Awake()
        {
            pickup = GetComponent<Pickup>();
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            holder.OnValueChanged += OnHolderChanged;
            if (!IsServer)
            {
                // Someone else moves it: here it never falls on its own.
                if (holder.Value != NoOne) pickup.HeldElsewhere();
                else if (!IsOwner && pickup.InContainer == null) pickup.RestingElsewhere(pickup.CollidersOn);
                StateRequestRpc();
            }
        }

        public override void OnNetworkDespawn()
        {
            all.Remove(this);
            holder.OnValueChanged -= OnHolderChanged;
        }

        // Someone left with this in their hands: it falls where they stood.
        public static void LetGoFor(ulong clientId)
        {
            foreach (var item in all.ToArray())
            {
                if (item.holder.Value != clientId) continue;
                item.holder.Value = NoOne;
                item.NetworkObject.RemoveOwnership();
                item.pickup.Drop();
            }
        }

        // ---- Picking up ----

        // Asks the host for this; if nobody else has it, it lands in the
        // asker's hands on their computer.
        public void RequestHold() => HoldRpc();

        [Rpc(SendTo.Server)]
        void HoldRpc(RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            if (holder.Value != NoOne && holder.Value != who)
            {
                return;
            }
            holder.Value = who;
            GiveTo(who);
            HeldRpc(who);
        }

        // A container's contents go with it.
        void GiveTo(ulong who)
        {
            if (OwnerClientId != who) NetworkObject.ChangeOwnership(who);
            if (TryGetComponent(out Container container))
            {
                foreach (var inside in container.Contents)
                {
                    if (inside.TryGetComponent(out ItemNet net) && net.IsSpawned) net.GiveTo(who);
                }
            }
        }

        [Rpc(SendTo.Everyone)]
        void HeldRpc(ulong who)
        {
            if (who == NetworkManager.LocalClientId)
            {
                PlayerNet me = PlayerNet.Owning(who);
                if (me != null) me.Hands.AcceptHold(pickup);
            }
            else
            {
                // Lifted off a burner or a shelf: that spot is free again here too.
                Transform before = transform.parent;
                pickup.HeldElsewhere();
                TakeIn(before);
            }
        }

        // Out of the hands that held it (set down, dropped, handed to a station).
        public void Released()
        {
            if (IsSpawned) ReleaseRpc();
        }

        [Rpc(SendTo.Server)]
        void ReleaseRpc(RpcParams rpc = default)
        {
            if (holder.Value == rpc.Receive.SenderClientId) holder.Value = NoOne;
        }

        void OnHolderChanged(ulong before, ulong now)
        {
            if (now == NoOne && !IsOwner)
            {
                pickup.RestingElsewhere(collidersOn: true);
            }
        }

        // ---- Baskets ----

        // Puts this into a container, for everyone.
        public void RequestInto(Container container)
        {
            var target = container.GetComponent<NetworkObject>();
            IntoRpc(target);
        }

        [Rpc(SendTo.Server)]
        void IntoRpc(NetworkObjectReference containerRef, RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            if ((holder.Value != NoOne && holder.Value != who) || !containerRef.TryGet(out NetworkObject target))
            {
                return;
            }
            holder.Value = NoOne;
            if (OwnerClientId != target.OwnerClientId) NetworkObject.ChangeOwnership(target.OwnerClientId);
            IntoEveryoneRpc(containerRef);
        }

        [Rpc(SendTo.Everyone)]
        void IntoEveryoneRpc(NetworkObjectReference containerRef)
        {
            if (!containerRef.TryGet(out NetworkObject target) || !target.TryGetComponent(out Container container))
            {
                return;
            }
            if (pickup.InContainer == container || !container.Accepts(pickup))
            {
                return;
            }
            PlayerNet me = PlayerNet.Owning(NetworkManager.LocalClientId);
            if (me != null && me.Hands.IsHolding(pickup)) me.Hands.Take(pickup);
            container.Add(pickup, 0.25f);
        }

        // ---- Eating ----

        // Gone for good (eaten): hidden here at once, removed by the host.
        public void Consume()
        {
            if (IsServer)
            {
                NetworkObject.Despawn(true);
                return;
            }
            gameObject.SetActive(false);
            ConsumeRpc();
        }

        [Rpc(SendTo.Server)]
        void ConsumeRpc()
        {
            if (IsSpawned) NetworkObject.Despawn(true);
        }

        // ---- State ----
        //
        // Whoever last handled an item runs it (a pot you set on the burner
        // heats on your computer) and sends its state whenever it changes:
        // what IItemState holds, and where it sits (in a basket, on the board,
        // on a towel). If someone else changes it (packs the jar, salts the
        // pot), their version goes through the host to everyone. Where it
        // sits is applied the way a save is: the station there takes it in.

        const char Part = '\u001e';
        const float CheckEvery = 0.3f;
        string lastKnown;
        string lastStates;
        float nextCheck;

        // Runs its own changes over time (heating, boiling, spoiling) here.
        // Elsewhere it only shows what the one running it sends.
        public bool Simulates => !IsSpawned || IsOwner;

        public static bool Runs(Component thing) =>
            !thing.TryGetComponent(out ItemNet net) || net.Simulates;

        // Removes an item for good (chopped up, eaten, thrown away), for everyone.
        public static void Remove(GameObject item)
        {
            if (item.TryGetComponent(out ItemNet net) && net.IsSpawned)
            {
                net.Consume();
                return;
            }
            Destroy(item);
        }

        void Update()
        {
            if (!IsSpawned || Time.time < nextCheck)
            {
                return;
            }
            nextCheck = Time.time + CheckEvery;
            Check();
        }

        // Sends this item's state now if it changed here.
        public void Share()
        {
            if (IsSpawned) Check();
        }

        void Check()
        {
            // Someone else has it in their hands: they say how it is.
            if (holder.Value != NoOne && !IsOwner)
            {
                return;
            }
            if (IsOwner)
            {
                string now = Snapshot(out string pose);
                if (now == lastKnown)
                {
                    return;
                }
                lastKnown = now;
                lastStates = States();
                SnapshotRpc(now, pose);
                return;
            }
            // Not ours to run: only what's in it can change here (packed,
            // salted, labeled). Where it sits is the runner's to say.
            string states = States();
            if (states == lastStates)
            {
                return;
            }
            lastStates = states;
            ProposeRpc(states);
        }

        string States()
        {
            var states = GetComponents<IItemState>();
            var parts = new string[states.Length];
            for (int i = 0; i < states.Length; i++) parts[i] = states[i].Capture();
            return string.Join(Separator.ToString(), parts);
        }

        // What it looks like here now, after taking in someone else's news.
        void Settled()
        {
            lastKnown = Snapshot(out _);
            lastStates = States();
        }

        // The states and where it sits, without the exact pose (that comes
        // through its NetworkTransform once it's in place).
        string Snapshot(out string pose)
        {
            Transform t = transform;
            pose = Round(t.localPosition) + ";" + Round(t.localEulerAngles);
            return States() + Part + Place();
        }

        static string Round(Vector3 v) =>
            v.x.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," +
            v.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "," +
            v.z.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        static Vector3 Parse(string text)
        {
            string[] n = text.Split(',');
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return new Vector3(float.Parse(n[0], c), float.Parse(n[1], c), float.Parse(n[2], c));
        }

        string Place()
        {
            string touch = pickup.CollidersOn ? "1" : "0";
            if (holder.Value != NoOne) return "h";
            if (pickup.InContainer != null && pickup.InContainer.TryGetComponent(out NetworkObject box)) return "c:" + box.NetworkObjectId;
            Transform parent = transform.parent;
            if (parent == null) return "f:" + touch;
            // On (or inside) another item: a jar in the canner.
            var owner = parent.GetComponentInParent<ItemNet>();
            if (owner != null && owner != this) return "i:" + owner.NetworkObjectId + ":" + PathBetween(owner.transform, parent) + ":" + touch;
            // On something in the house: the board, a towel, a burner.
            return "p:" + PathBetween(null, parent) + ":" + touch;
        }

        [Rpc(SendTo.NotMe)]
        void SnapshotRpc(string state, string pose)
        {
            Apply(state, pose);
            Settled();
        }

        // Someone who doesn't run it changed what's in it: the host passes it on.
        [Rpc(SendTo.Server)]
        void ProposeRpc(string states)
        {
            Apply(states, "");
            Settled();
            ContentsRpc(states);
        }

        [Rpc(SendTo.NotServer)]
        void ContentsRpc(string states)
        {
            Apply(states, "");
            Settled();
        }

        // Someone who just joined asks how this item is.
        [Rpc(SendTo.Server)]
        void StateRequestRpc(RpcParams rpc = default)
        {
            string state = Snapshot(out string pose);
            StateToRpc(state, pose, RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void StateToRpc(string state, string pose, RpcParams rpc = default)
        {
            Apply(state, pose);
            Settled();
        }

        void Apply(string state, string pose)
        {
            int split = state.LastIndexOf(Part);
            string states = split >= 0 ? state.Substring(0, split) : state;
            string place = split >= 0 ? state.Substring(split + 1) : "";

            var mine = GetComponents<IItemState>();
            string[] parts = states.Split(Separator);
            for (int i = 0; i < mine.Length && i < parts.Length; i++)
            {
                if (mine[i].Capture() != parts[i]) mine[i].Restore(parts[i]);
            }
            Settle(place, pose);
        }

        void Settle(string place, string pose)
        {
            if (place.Length == 0 || place == "h" || holder.Value != NoOne)
            {
                return;
            }
            if (place.StartsWith("c:"))
            {
                if (ulong.TryParse(place.Substring(2), out ulong id) && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject box)
                    && box.TryGetComponent(out Container container) && pickup.InContainer != container && container.Accepts(pickup))
                {
                    container.Add(pickup, 0f);
                }
                return;
            }
            string[] bits = place.Split(':');
            bool touch = bits[bits.Length - 1] == "1";
            Transform target = null;
            if (bits[0] == "p")
            {
                target = FindInScene(bits[1]);
            }
            else if (bits[0] == "i" && ulong.TryParse(bits[1], out ulong id) && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out NetworkObject on))
            {
                target = bits[2].Length == 0 ? on.transform : on.transform.Find(bits[2]);
            }

            Transform before = transform.parent;
            if (target != before)
            {
                if (pickup.InContainer != null) pickup.InContainer.Remove(pickup);
                transform.SetParent(target, worldPositionStays: target == null);
                if (target != null)
                {
                    string[] p = pose.Split(';');
                    transform.SetLocalPositionAndRotation(Parse(p[0]), Quaternion.Euler(Parse(p[1])));
                }
                TakeIn(before);
                TakeIn(target);
            }
            if (!Simulates || target != null)
            {
                pickup.SetResting(true, touch);
            }
            else
            {
                pickup.SetCollidersEnabled(touch);
            }
        }

        // The station (or burner, or canner) there takes in what's on it now.
        static void TakeIn(Transform place)
        {
            if (place == null) return;
            foreach (var station in place.GetComponentsInParent<IAfterLoad>())
            {
                station.AfterLoad();
            }
        }

        static string PathBetween(Transform from, Transform to)
        {
            var names = new List<string>();
            for (Transform t = to; t != null && t != from; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }

        static Transform FindInScene(string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == rootName)
                {
                    return slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
                }
            }
            return null;
        }
    }
}
