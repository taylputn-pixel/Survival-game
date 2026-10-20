using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Net;
using GrandmasHouse.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.DayCycle
{
    // A bed. Lie down any time and the screen fades out. One sleeper per bed.
    // The night only passes once everyone in the game is in bed: when the
    // clock stops at night, morning comes; when it runs all night, time
    // hurries until you're rested, and you go on getting hungry. Once the
    // sun is up, or while others are still awake, you can get up early.
    // The host saves the game when the night ends (see ClockNet).
    public class Bed : NetworkBehaviour, IInteractable, ISharedUse
    {
        const ulong NoOne = ulong.MaxValue;

        [SerializeField] DayClock clock;
        [SerializeField] ScreenFade fade;
        [SerializeField] float fadeSeconds = 1.2f;
        [Tooltip("Real seconds of dark before you can get up again.")]
        [SerializeField] float sleepSeconds = 1.5f;

        readonly NetworkVariable<ulong> sleeper = new NetworkVariable<ulong>(NoOne);
        bool sleeping;

        bool Taken => IsSpawned && sleeper.Value != NoOne;

        public bool CanInteract(PlayerHands hands) => hands.IsEmpty && !sleeping && !Taken;

        public string PromptVerb(PlayerHands hands) => "Sleep";

        public void Interact(PlayerHands hands)
        {
            if (IsSpawned)
            {
                ClaimRpc();
            }
            else
            {
                StartCoroutine(Sleep(hands));
            }
        }

        [Rpc(SendTo.Server)]
        void ClaimRpc(RpcParams rpc = default)
        {
            ulong who = rpc.Receive.SenderClientId;
            if (sleeper.Value != NoOne)
            {
                return;
            }
            sleeper.Value = who;
            GrantRpc(RpcTarget.Single(who, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        void GrantRpc(RpcParams rpc = default)
        {
            PlayerNet me = PlayerNet.Owning(NetworkManager.LocalClientId);
            if (me != null)
            {
                StartCoroutine(Sleep(me.Hands));
            }
        }

        [Rpc(SendTo.Server)]
        void LeaveRpc(RpcParams rpc = default)
        {
            if (sleeper.Value == rpc.Receive.SenderClientId)
            {
                sleeper.Value = NoOne;
            }
        }

        IEnumerator Sleep(PlayerHands hands)
        {
            sleeping = true;
            hands.Controller.InputEnabled = false;
            var needs = hands.GetComponent<PlayerNeeds>();
            var interact = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);

            yield return fade.FadeTo(1f, fadeSeconds);
            if (needs != null)
            {
                needs.Asleep = true;
            }
            float earliestWake = Time.time + sleepSeconds;
            bool restUntilRested = needs != null && clock.Mode.hungerHasPenalties && !clock.Mode.clockStopsAtNight;
            int startDay = clock.Day;

            while (needs == null || !needs.IsDead)
            {
                bool morning = clock.Day != startDay;
                if (restUntilRested ? needs.State.rest >= 100f : morning)
                {
                    break;
                }
                // Up once the sun's up, or whenever someone else is still
                // awake (the night isn't hurrying by for you alone).
                bool canGetUp = Time.time >= earliestWake && (!clock.IsNight || clock.FastForward <= 0f);
                if (needs != null) needs.CanGetUp = canGetUp;
                if (canGetUp && interact.WasPressedThisFrame())
                {
                    break;
                }
                yield return null;
            }

            if (needs != null)
            {
                needs.CanGetUp = false;
                needs.Asleep = false;
            }
            if (IsSpawned)
            {
                LeaveRpc();
            }
            yield return fade.FadeTo(0f, fadeSeconds);

            if (needs == null || !needs.IsDead)
            {
                hands.Controller.InputEnabled = true;
            }
            sleeping = false;
        }
    }
}
