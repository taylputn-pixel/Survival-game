using System.Collections;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Interaction;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // The outdoor faucet. Bring the watering can and it's set under the
    // spout, the handle turns, and it fills up.
    public class WaterSpigot : MonoBehaviour, IInteractable
    {
        [SerializeField] PlayerSettings playerSettings;
        [SerializeField] GardenSettings settings;
        [Tooltip("Where the can sits while it fills.")]
        [SerializeField] Transform fillPoint;
        [SerializeField] Transform handle;
        [SerializeField] ParticleSystem stream;
        [SerializeField] float handleTurnSeconds = 0.25f;

        bool busy;

        public bool CanInteract(PlayerHands hands)
        {
            if (busy || hands.IsEmpty)
            {
                return false;
            }
            var can = hands.Held.GetComponent<WateringCan>();
            return can != null && !can.IsFull;
        }

        public void Interact(PlayerHands hands) => StartCoroutine(Filling(hands));

        IEnumerator Filling(PlayerHands hands)
        {
            busy = true;
            hands.Controller.InputEnabled = false;
            Pickup item = hands.TakeHeld();
            var can = item.GetComponent<WateringCan>();

            float glide = playerSettings.placeGlideSeconds * 1.5f;
            item.AttachTo(fillPoint, Vector3.zero, Quaternion.identity, glide, collidersOn: false);
            yield return new WaitForSeconds(glide);

            yield return TurnHandle(0f, 90f);
            stream.Play();
            while (!can.IsFull)
            {
                can.Fill(Time.deltaTime / settings.canFillSeconds);
                yield return null;
            }
            stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            yield return TurnHandle(90f, 0f);

            hands.Give(item);
            hands.Controller.InputEnabled = true;
            busy = false;
        }

        IEnumerator TurnHandle(float from, float to)
        {
            for (float time = 0f; time < handleTurnSeconds; time += Time.deltaTime)
            {
                handle.localRotation = Quaternion.Euler(0f, Mathf.SmoothStep(from, to, time / handleTurnSeconds), 0f);
                yield return null;
            }
            handle.localRotation = Quaternion.Euler(0f, to, 0f);
        }
    }
}
