using System.Collections;
using GrandmasHouse.Carrying;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // What's inside a glass jar: slices packed in, a head of dill, and brine.
    // Everything is visible through the glass.
    [RequireComponent(typeof(Pickup))]
    public class JarContents : MonoBehaviour
    {
        [Tooltip("The bottom of the inside of the jar.")]
        [SerializeField] Transform inside;
        [SerializeField] float innerHeight = 0.12f;
        [SerializeField] float innerRadius = 0.036f;
        [Tooltip("A see-through cylinder that rises with the brine.")]
        [SerializeField] Transform brine;
        [SerializeField] int capacity = 22;
        [SerializeField] float sliceStep = 0.0052f;

        [Tooltip("The lid and ring, hidden until the jar is closed up.")]
        [SerializeField] Transform lid;
        [SerializeField] float lidSeconds = 0.25f;

        public int SliceCount { get; private set; }
        public bool HasLid { get; private set; }
        // Through the water bath and out to cool.
        public bool IsProcessed { get; private set; }
        public bool IsSealed { get; private set; }
        public bool HasDill { get; private set; }
        // 0 is empty, 1 is right to the rim.
        public float BrineLevel { get; private set; }
        public bool IsFull => SliceCount >= capacity;
        public int Capacity => capacity;
        public Vector3 Mouth => inside.position + inside.up * innerHeight;

        void Awake()
        {
            ShowBrine();
            lid.gameObject.SetActive(false);
        }

        // The lid drops on in one motion.
        public void PutOnLid()
        {
            if (HasLid)
            {
                return;
            }
            HasLid = true;
            StartCoroutine(LidDrop());
        }

        // Out of the canner: as it cools, the lid seals with a ping.
        public void CoolAndSeal(float seconds)
        {
            IsProcessed = true;
            StartCoroutine(Cooling(seconds));
        }

        IEnumerator LidDrop()
        {
            Vector3 seated = lid.localPosition;
            lid.gameObject.SetActive(true);
            for (float time = 0f; time < lidSeconds; time += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / lidSeconds);
                lid.localPosition = seated + Vector3.up * (1f - t) * 0.04f;
                yield return null;
            }
            lid.localPosition = seated;
        }

        IEnumerator Cooling(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            // Ping: the center of the lid pulls down as it seals.
            Vector3 size = lid.localScale;
            for (float time = 0f; time < 0.15f; time += Time.deltaTime)
            {
                lid.localScale = Vector3.Scale(size, new Vector3(1f, 1f - Mathf.Sin(time / 0.15f * Mathf.PI) * 0.4f, 1f));
                yield return null;
            }
            lid.localScale = size;
            IsSealed = true;
        }

        // A slice drops in and settles on the pile inside, a little askew.
        public void AddSlice(Transform slice, float delay)
        {
            int index = SliceCount++;
            float y = 0.004f + index * sliceStep;
            Vector2 jitter = Random.insideUnitCircle * (innerRadius - 0.026f);
            var end = new Vector3(jitter.x, y, jitter.y);
            var tilt = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
            slice.SetParent(inside, worldPositionStays: true);
            StartCoroutine(Drop(slice, end, tilt, delay));
        }

        // The dill goes in standing up along the side of the jar.
        public void AddDill(Pickup dill)
        {
            HasDill = true;
            dill.AttachTo(inside, new Vector3(innerRadius * 0.4f, 0.01f, 0f), Quaternion.Euler(0f, 0f, 8f), 0.35f, collidersOn: false);
            dill.transform.localScale = Vector3.Scale(dill.transform.localScale, new Vector3(0.6f, 0.9f, 0.6f));
        }

        public void AddBrine(float amount)
        {
            BrineLevel = Mathf.Clamp01(BrineLevel + amount);
            ShowBrine();
        }

        void ShowBrine()
        {
            brine.gameObject.SetActive(BrineLevel > 0f);
            float height = BrineLevel * innerHeight;
            brine.localScale = new Vector3(innerRadius * 2f, height / 2f, innerRadius * 2f);
            brine.localPosition = Vector3.up * height / 2f;
        }

        IEnumerator Drop(Transform slice, Vector3 end, Quaternion endRotation, float delay)
        {
            yield return new WaitForSeconds(delay);
            Vector3 start = slice.localPosition;
            Quaternion startRotation = slice.localRotation;
            // Up over the rim, then down inside.
            Vector3 overRim = new Vector3(end.x, innerHeight + 0.04f, end.z);
            const float seconds = 0.4f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float t = time / seconds;
                Vector3 a = Vector3.Lerp(start, overRim, t);
                Vector3 b = Vector3.Lerp(overRim, end, t);
                slice.SetLocalPositionAndRotation(Vector3.Lerp(a, b, t), Quaternion.Slerp(startRotation, endRotation, t));
                yield return null;
            }
            slice.SetLocalPositionAndRotation(end, endRotation);
        }
    }
}
