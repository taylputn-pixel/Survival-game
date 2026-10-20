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
            pickup.HeldChanged += OnHeldChanged;
        }

        public override void OnNetworkSpawn()
        {
            all.Add(this);
            holder.OnValueChanged += OnHolderChanged;
            if (!IsServer)
            {
                if (holder.Value != NoOne) pickup.HeldElsewhere();
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
                pickup.HeldElsewhere();
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
            gameObject.SetActive(false);
            ConsumeRpc();
        }

        [Rpc(SendTo.Server)]
        void ConsumeRpc()
        {
            if (IsSpawned) NetworkObject.Despawn(true);
        }

        // ---- State ----

        // Whoever moved it last tells everyone how it ended up.
        void OnHeldChanged(bool held)
        {
            if (!held && IsSpawned && IsOwner) Share();
        }

        // Sends this item's state (and whether it can be touched) to everyone else.
        public void Share()
        {
            if (!IsSpawned) return;
            ShareRpc(Capture(), pickup.CollidersOn, Container());
        }

        NetworkObjectReference Container()
        {
            var inside = pickup.InContainer != null ? pickup.InContainer.GetComponent<NetworkObject>() : null;
            return inside != null && inside.IsSpawned ? new NetworkObjectReference(inside) : default;
        }

        string Capture()
        {
            var states = GetComponents<IItemState>();
            var parts = new string[states.Length];
            for (int i = 0; i < states.Length; i++) parts[i] = states[i].Capture();
            return string.Join(Separator.ToString(), parts);
        }

        void Apply(string captured)
        {
            if (string.IsNullOrEmpty(captured)) return;
            var states = GetComponents<IItemState>();
            string[] parts = captured.Split(Separator);
            for (int i = 0; i < states.Length && i < parts.Length; i++) states[i].Restore(parts[i]);
        }

        [Rpc(SendTo.NotMe)]
        void ShareRpc(string captured, bool collidersOn, NetworkObjectReference containerRef) =>
            ShareRpcBody(captured, collidersOn, containerRef);

        // Someone who just joined asks how this item is.
        [Rpc(SendTo.Server)]
        void StateRequestRpc(RpcParams rpc = default)
        {
            ShareToRpc(Capture(), pickup.CollidersOn, Container(), RpcTarget.Single(rpc.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void ShareToRpc(string captured, bool collidersOn, NetworkObjectReference containerRef, RpcParams rpc = default) =>
            ShareRpcBody(captured, collidersOn, containerRef);

        void ShareRpcBody(string captured, bool collidersOn, NetworkObjectReference containerRef)
        {
            Apply(captured);
            if (containerRef.TryGet(out NetworkObject target) && target.TryGetComponent(out Container container))
            {
                if (pickup.InContainer != container) container.Add(pickup, 0f);
            }
            else if (holder.Value == NoOne)
            {
                pickup.RestingElsewhere(collidersOn);
            }
        }
    }
}
