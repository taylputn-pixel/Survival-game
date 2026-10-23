using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Interaction;
using GrandmasHouse.Player;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A stump for splitting wood. Stand a log on it, take up the axe and
    // swing: a few good swings split it into firewood that tumbles off the
    // block. Every swing is work, and tires you a little. The host makes the
    // firewood; a friend's last swing asks the host to. A hen carried over
    // and laid on the block takes one swing: a chicken for the pot.
    public class ChoppingBlock : MonoBehaviour, IInteractable, ISharedUse
    {
        [SerializeField] Transform top;
        [SerializeField] Pickup firewoodPrefab;
        [SerializeField] int swingsToSplit = 3;
        [Tooltip("Rest each swing costs.")]
        [SerializeField] float restPerSwing = 1.5f;
        [SerializeField] float swingSeconds = 0.45f;

        public int Swings { get; private set; }
        public Log OnBlock => top.GetComponentInChildren<Log>();
        public Animals.LiveHen HenOnBlock => top.GetComponentInChildren<Animals.LiveHen>();
        bool Empty => OnBlock == null && HenOnBlock == null;

        bool swinging;

        static bool Holding<T>(PlayerHands hands) where T : Component => !hands.IsEmpty && hands.Held.GetComponent<T>() != null;

        // A grown hen, not a chick: a chick isn't for the pot.
        static bool HoldingHen(PlayerHands hands) => Holding<Animals.LiveHen>(hands) && !hands.Held.GetComponent<Animals.LiveHen>().Hen.IsChick;

        public bool CanInteract(PlayerHands hands)
        {
            if (swinging) return false;
            if (Holding<Animals.LiveHen>(hands)) return HoldingHen(hands) && Empty;
            if (Holding<Log>(hands)) return Empty;
            return Holding<Axe>(hands) && !Empty;
        }

        public string PromptVerb(PlayerHands hands) =>
            Holding<Log>(hands) ? "Stand it on the block" : Holding<Animals.LiveHen>(hands) ? "Lay her on the block" : "Swing the axe";

        public void Interact(PlayerHands hands)
        {
            if (Holding<Animals.LiveHen>(hands))
            {
                Pickup hen = hands.TakeHeld();
                hen.AttachTo(top, Vector3.up * 0.05f, Quaternion.Euler(0f, 0f, 90f), 0.25f, collidersOn: true);
                return;
            }
            if (Holding<Log>(hands))
            {
                Pickup log = hands.TakeHeld();
                log.AttachTo(top, Vector3.up * log.RestHalfHeight, Quaternion.identity, 0.25f, collidersOn: true);
                Swings = 0;
                return;
            }
            StartCoroutine(Swing(hands));
        }

        IEnumerator Swing(PlayerHands hands)
        {
            swinging = true;
            hands.Controller.InputEnabled = false;
            // The axe comes up over your head and down onto the log.
            Transform axe = hands.Held.transform;
            Vector3 rest = axe.localPosition;
            Quaternion restTurn = axe.localRotation;
            for (float t = 0f; t < swingSeconds; t += Time.deltaTime)
            {
                float phase = t / swingSeconds;
                float lift = phase < 0.6f ? Mathf.Sin(phase / 0.6f * Mathf.PI * 0.5f) : 1f - (phase - 0.6f) / 0.4f;
                axe.localPosition = rest + new Vector3(0f, 0.25f * lift, -0.1f * lift);
                axe.localRotation = restTurn * Quaternion.Euler(-70f * lift, 0f, 0f);
                yield return null;
            }
            axe.localPosition = rest;
            axe.localRotation = restTurn;
            Sfx.Play(SoundId.KnifeChop, top.position, 1.2f, 0.15f);
            if (hands.TryGetComponent(out PlayerNeeds needs) && needs.State != null)
            {
                needs.State.rest = Mathf.Max(0f, needs.State.rest - restPerSwing);
            }
            Swings++;
            var hen = HenOnBlock;
            if (hen != null)
            {
                Swings = 0;
                hen.Cull();
            }
            else if (Swings >= swingsToSplit) Split();
            hands.Controller.InputEnabled = true;
            swinging = false;
        }

        static bool Hosting => NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening || NetworkManager.Singleton.IsServer;

        // The log falls apart into firewood.
        public void Split()
        {
            Swings = 0;
            if (!Hosting && TryGetComponent(out ChoppingBlockNet net))
            {
                net.Split();
                return;
            }
            SplitHere();
        }

        public void SplitHere()
        {
            Log log = OnBlock;
            if (log == null) return;
            int pieces = log.Pieces;
            Net.ItemNet.Remove(log.gameObject);
            DayCycle.YearTally.Add(DayCycle.YearTally.Count.Firewood, pieces);
            for (int i = 0; i < pieces; i++)
            {
                float angle = i * 360f / pieces;
                Vector3 at = top.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 0.35f + Vector3.up * 0.1f;
                Pickup piece = Instantiate(firewoodPrefab, at, Quaternion.Euler(0f, angle, 80f));
                piece.name = firewoodPrefab.name;
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer && piece.TryGetComponent(out NetworkObject shared))
                {
                    shared.Spawn();
                }
            }
        }
    }
}
