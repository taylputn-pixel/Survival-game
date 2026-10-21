using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Food;
using GrandmasHouse.Interaction;
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
    // them with the game. Asleep or collapsed, the figure lies down. Hold
    // food and look at another player to feed them; that's how someone
    // who collapsed from hunger is brought round.
    public class PlayerNet : NetworkBehaviour, IInteractable, ISharedUse
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
        // Collapsed from hunger, waiting to be fed.
        public bool IsDown => down.Value;
        // The host's latest copy of this player's hunger and rest.
        public string NeedsJson { get; private set; }

        readonly NetworkVariable<float> pitch = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> asleep = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        readonly NetworkVariable<bool> down = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        float nextReport;
        Vector3 bodyStanding, pivotStanding;
        Quaternion bodyStandingTurn;

        public static PlayerNet Owning(ulong clientId) => All.Find(p => p.OwnerClientId == clientId);

        public override void OnNetworkSpawn()
        {
            All.Add(this);
            name = IsOwner ? "Player (you)" : "Player " + OwnerClientId;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", colors[(int)(OwnerClientId % (ulong)colors.Length)]);
            foreach (var r in tinted) r.SetPropertyBlock(block);

            foreach (var part in figure) part.SetActive(!IsOwner);
            bodyStanding = figure[0].transform.localPosition;
            bodyStandingTurn = figure[0].transform.localRotation;
            pivotStanding = cameraPivot.localPosition;
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
                down.Value = needs.Collapsed;
                if (Time.time >= nextReport)
                {
                    Report();
                }
            }
            else
            {
                Pose(asleep.Value || down.Value);
            }
        }

        // Standing and looking where they look, or lying on their back.
        void Pose(bool lying)
        {
            // A sleeper in bed is out of everyone's way; someone collapsed
            // can still be reached to feed them.
            var solid = GetComponent<CharacterController>();
            solid.enabled = !lying || down.Value;
            Transform bodyPart = figure[0].transform;
            if (lying)
            {
                bodyPart.SetLocalPositionAndRotation(new Vector3(0f, 0.25f, -0.1f), Quaternion.Euler(90f, 0f, 0f));
                cameraPivot.SetLocalPositionAndRotation(new Vector3(0f, 0.3f, 0.72f), Quaternion.Euler(-90f, 0f, 0f));
            }
            else
            {
                bodyPart.SetLocalPositionAndRotation(bodyStanding, bodyStandingTurn);
                cameraPivot.SetLocalPositionAndRotation(pivotStanding, Quaternion.Euler(pitch.Value, 0f, 0f));
            }
        }

        // ---- Feeding someone else ----

        IEdible FoodIn(PlayerHands feeder) =>
            feeder.Held != null && feeder.Held.TryGetComponent(out IEdible food) && food.CanEat ? food : null;

        public bool CanInteract(PlayerHands feeder) =>
            IsSpawned && feeder != hands && (FoodIn(feeder) != null ? IsDown || !IsAsleep : feeder.IsEmpty && IsDown);

        public string PromptVerb(PlayerHands feeder) => FoodIn(feeder) != null ? "Feed" : "Warm them up";

        public void Interact(PlayerHands feeder)
        {
            if (FoodIn(feeder) == null)
            {
                WarmRpc();
                return;
            }
            IEdible food = FoodIn(feeder);
            var settings = feeder.GetComponent<PlayerNeeds>().Settings;
            FeedRpc(food.Nutrition, food.SickHours(settings));
            food.Eaten(feeder);
        }

        [Rpc(SendTo.Owner)]
        void FeedRpc(float nutrition, float sickFor) => needs.Fed(nutrition, sickFor);

        [Rpc(SendTo.Owner)]
        void WarmRpc() => needs.WarmedUp();

        // Everyone is down: the host tells each player the game is over.
        [Rpc(SendTo.Owner)]
        public void EndGameRpc() => needs.EndGame();

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
