using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Net
{
    // One player in a shared game. On your own computer it's you: your
    // camera, your input, your HUD. Everyone else sees a simple figure that
    // walks where you walk and looks where you look. Hunger and rest are
    // worked out on your own computer and reported to the host, who saves
    // them with the game.
    public class PlayerNet : NetworkBehaviour
    {
        [SerializeField] FirstPersonController controller;
        [SerializeField] PlayerHands hands;
        [SerializeField] PlayerNeeds needs;
        [SerializeField] CursorLock cursorLock;
        [SerializeField] Camera view;
        [SerializeField] AudioListener ears;
        [SerializeField] Transform cameraPivot;
        [Tooltip("The figure other players see (body, head). Hidden on your own screen.")]
        [SerializeField] GameObject[] figure;
        [SerializeField] Renderer[] tinted;
        [SerializeField] Color[] colors =
        {
            new Color(0.36f, 0.48f, 0.66f), new Color(0.66f, 0.36f, 0.32f),
            new Color(0.40f, 0.58f, 0.36f), new Color(0.74f, 0.60f, 0.30f),
        };
        [SerializeField] float reportSeconds = 5f;

        // Every player in the game, on every computer.
        public static readonly List<PlayerNet> All = new List<PlayerNet>();

        public PlayerHands Hands => hands;
        public PlayerNeeds Needs => needs;
        public bool IsAsleep => asleep.Value;
        public bool IsDead => dead.Value;
        // The host's latest copy of this player's hunger and rest.
        public string NeedsJson { get; private set; }

        readonly NetworkVariable<float> pitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> asleep = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> dead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        float nextReport;

        public static PlayerNet Owning(ulong clientId) => All.Find(p => p.OwnerClientId == clientId);

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            name = IsOwner ? "Player (you)" : "Player " + OwnerClientId;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", colors[(int)(OwnerClientId % (ulong)colors.Length)]);
            foreach (var r in tinted) r.SetPropertyBlock(block);

            foreach (var part in figure) part.SetActive(!IsOwner);
            if (IsOwner)
            {
                view.enabled = true;
                ears.enabled = true;
                foreach (var bound in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (bound is IPlayerBound b) b.Bind(hands);
                }
            }
            else
            {
                view.enabled = false;
                ears.enabled = false;
                controller.enabled = false;
                hands.enabled = false;
                needs.enabled = false;
                cursorLock.enabled = false;
            }

            if (IsServer && SaveManager.Instance != null)
            {
                SaveManager.Instance.Arrived(this);
            }
        }

        public override void OnNetworkDespawn() => All.Remove(this);

        public override void OnDestroy()
        {
            All.Remove(this);
            base.OnDestroy();
        }

        // The save's key for this player.
        public string Key => NetSession.Instance != null ? NetSession.Instance.KeyOf(OwnerClientId) : NetSession.LocalHostKey;

        void Update()
        {
            if (!IsSpawned)
            {
                return;
            }
            if (IsOwner)
            {
                float x = cameraPivot.localEulerAngles.x;
                pitch.Value = x > 180f ? x - 360f : x;
                asleep.Value = needs.Asleep;
                dead.Value = needs.IsDead;
                if (Time.time >= nextReport)
                {
                    Report();
                }
            }
            else
            {
                cameraPivot.localRotation = Quaternion.Euler(pitch.Value, 0f, 0f);
            }
        }

        // Sends hunger and rest to the host now (it also happens every few seconds).
        public void Report()
        {
            nextReport = Time.time + reportSeconds;
            if (needs.State != null)
            {
                ReportRpc(needs.Capture());
            }
        }

        [Rpc(SendTo.Server)]
        void ReportRpc(string json) => NeedsJson = json;

        // The host hands back where this player was and how they were doing.
        public void Restore(PlayerRecord record)
        {
            NeedsJson = record.needs;
            RestoreRpc(record.position, record.yaw, record.needs ?? "");
        }

        [Rpc(SendTo.Owner)]
        void RestoreRpc(Vector3 position, float yaw, string needsJson)
        {
            var body = GetComponent<CharacterController>();
            body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            body.enabled = true;
            if (!string.IsNullOrEmpty(needsJson))
            {
                needs.Restore(needsJson);
            }
        }
    }
}
