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
    // A bed. Lie down any time and the screen fades out; everyone else sees
    // you lying in it. One sleeper per bed.
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
            LieDown(hands);
            if (needs != null)
            {
                needs.Asleep = true;
            }
            float earliestWake = Time.time + sleepSeconds;
            bool restUntilRested = needs != null && clock.Mode.hungerHasPenalties && !clock.Mode.clockStopsAtNight;
            int startDay = clock.Day;

            while (needs == null || !needs.Collapsed)
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
            GetUp(hands, stillDown: needs != null && needs.Collapsed);
            if (IsSpawned)
            {
                LeaveRpc();
            }
            yield return fade.FadeTo(0f, fadeSeconds);

            if (needs == null || !needs.Collapsed)
            {
                hands.Controller.InputEnabled = true;
            }
            sleeping = false;
        }

        // Into the bed, head on the pillow.
        void LieDown(PlayerHands hands)
        {
            hands.GetComponent<CharacterController>().enabled = false;
            hands.transform.SetPositionAndRotation(transform.position + Vector3.up * 0.3f, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            hands.Controller.LyingDown = true;
        }

        // Out of bed on whichever side is clear, facing away from it.
        void GetUp(PlayerHands hands, bool stillDown)
        {
            Vector3[] sides = { new Vector3(0.95f, 0f, 0f), new Vector3(-0.95f, 0f, 0f), new Vector3(0f, 0f, -1.5f) };
            Vector3 spot = transform.TransformPoint(sides[0]);
            foreach (var side in sides)
            {
                Vector3 p = transform.TransformPoint(side);
                if (!Physics.CheckCapsule(p + Vector3.up * 0.35f, p + Vector3.up * 1.4f, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    spot = p;
                    break;
                }
            }
            Vector3 away = spot - transform.position;
            away.y = 0f;
            hands.transform.SetPositionAndRotation(spot + Vector3.up * 0.02f, Quaternion.LookRotation(away.normalized, Vector3.up));
            hands.GetComponent<CharacterController>().enabled = true;
            hands.Controller.LyingDown = stillDown;
        }
    }
}
