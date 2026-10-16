using System.Collections;
using GrandmasHouse.Audio;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // Something on a plant you take off by hand: a tomato sucker you pinch
    // off where it meets the stem, or a hornworm you pick off and flick out
    // of the bed. You find them by looking closer at the plant and turning it
    // (PlantStation). While any are on a plant it isn't tended, so it yields
    // less (it never dies in Cozy). Its collider only helps your eye land on
    // the plant.
    public class Pinchable : MonoBehaviour
    {
        [Tooltip("What the prompt says, like \"Pinch off\" or \"Pick off\".")]
        [SerializeField] string verb = "Pinch off";
        [Tooltip("How far it's flicked away when taken off, in meters. Zero drops it at the plant's foot.")]
        [SerializeField] float tossDistance;
        [Tooltip("A slow side-to-side sway, in degrees (a hornworm munching). Zero keeps it still.")]
        [SerializeField] float wiggle;
        [SerializeField] SoundId sound = SoundId.DillRustle;
        [SerializeField] float removeSeconds = 0.4f;

        public string Verb => verb;
        public PlantSpot Owner { get; set; }

        float phase;
        bool removed;

        void Awake() => phase = Random.Range(0f, 10f);

        void Update()
        {
            if (!removed && wiggle > 0f)
            {
                float sway = Mathf.Sin((Time.time + phase) * 2.2f) * wiggle;
                transform.localRotation = Quaternion.Euler(0f, sway, 0f);
            }
        }

        // Taken off: it pops free, then drops or is flicked away and is gone.
        public void Remove(Vector3 awayFrom)
        {
            removed = true;
            foreach (var hit in GetComponentsInChildren<Collider>())
            {
                hit.enabled = false;
            }
            Sfx.Play(sound, transform.position);
            transform.SetParent(null, worldPositionStays: true);
            StartCoroutine(Leave(awayFrom));
        }

        IEnumerator Leave(Vector3 awayFrom)
        {
            Vector3 start = transform.position;
            Vector3 out_ = Vector3.ProjectOnPlane(start - awayFrom, Vector3.up).normalized;
            if (out_ == Vector3.zero) out_ = Vector3.forward;
            Vector3 end = tossDistance > 0f
                ? start + out_ * tossDistance + Vector3.down * start.y * 0.5f
                : new Vector3(start.x, awayFrom.y, start.z) + out_ * 0.03f;
            Vector3 size = transform.localScale;
            Quaternion from = transform.rotation;
            Quaternion to = Quaternion.Euler(Random.Range(60f, 120f), Random.Range(0f, 360f), 0f);
            for (float time = 0f; time < removeSeconds; time += Time.deltaTime)
            {
                float t = time / removeSeconds;
                float arc = Mathf.Sin(t * Mathf.PI) * (tossDistance > 0f ? 0.2f : 0.03f);
                transform.SetPositionAndRotation(Vector3.Lerp(start, end, t) + Vector3.up * arc, Quaternion.Slerp(from, to, t));
                // It shrinks away as it lands, so nothing is left to tidy up.
                transform.localScale = size * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, t)));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
