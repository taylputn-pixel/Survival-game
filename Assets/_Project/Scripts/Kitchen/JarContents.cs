using System.Collections;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Saving;
using GrandmasHouse.Stations;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // What's inside a glass jar: slices packed in, a head of dill, garlic
    // cloves, and brine.
    // Everything is visible through the glass.
    [RequireComponent(typeof(Pickup))]
    public class JarContents : MonoBehaviour, IItemState, IEmptyable, IPickupGuard
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
        [Tooltip("Grandma's label, shown once the jar has sealed.")]
        [SerializeField] GameObject label;
        [SerializeField] float lidSeconds = 0.25f;
        [Header("Rebuilding a saved jar")]
        [SerializeField] Material chipMaterial;
        [Tooltip("Diameter and thickness of a pickle chip, in meters.")]
        [SerializeField] Vector2 chipSize = new Vector2(0.05f, 0.006f);
        [SerializeField] Material cloveMaterial;
        [Tooltip("Width, height and length of a clove, in meters.")]
        [SerializeField] Vector3 cloveSize = new Vector3(0.0126f, 0.0108f, 0.0216f);

        public int SliceCount { get; private set; }
        public int CloveCount { get; private set; }
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
            label.SetActive(false);
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
            Sfx.Play(SoundId.LidClink, lid.position);
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
            label.SetActive(true);
            Sfx.Play(SoundId.LidPing, lid.position, 1f, 0.03f);
        }

        [System.Serializable]
        class State
        {
            public int slices;
            public int cloves;
            public bool dill;
            public float brine;
            public bool lid;
            public bool processed;
            public bool sealedUp;
        }

        public string Capture() => JsonUtility.ToJson(new State
        {
            slices = SliceCount, cloves = CloveCount, dill = HasDill, brine = BrineLevel, lid = HasLid, processed = IsProcessed, sealedUp = IsSealed,
        });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            for (int i = 0; i < state.slices; i++)
            {
                var chip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                chip.name = "Slice";
                Destroy(chip.GetComponent<Collider>());
                chip.GetComponent<Renderer>().sharedMaterial = chipMaterial;
                chip.transform.localScale = new Vector3(chipSize.x, chipSize.y / 2f, chipSize.x);
                chip.transform.SetParent(inside, false);
                NoShadows(chip.transform);
                Vector2 jitter = Random.insideUnitCircle * (innerRadius - 0.026f);
                chip.transform.SetLocalPositionAndRotation(
                    new Vector3(jitter.x, 0.004f + i * sliceStep, jitter.y),
                    Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f)));
            }
            for (int i = 0; i < state.cloves; i++)
            {
                var clove = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                clove.name = "Clove";
                Destroy(clove.GetComponent<Collider>());
                clove.GetComponent<Renderer>().sharedMaterial = cloveMaterial;
                clove.transform.localScale = cloveSize;
                clove.transform.SetParent(inside, false);
                NoShadows(clove.transform);
                clove.transform.SetLocalPositionAndRotation(ClovePlace(), Random.rotation);
            }
            SliceCount = state.slices;
            CloveCount = state.cloves;
            HasDill = state.dill;
            BrineLevel = state.brine;
            HasLid = state.lid;
            IsProcessed = state.processed;
            IsSealed = state.sealedUp;
            lid.gameObject.SetActive(HasLid);
            label.SetActive(IsSealed);
            ShowBrine();
        }

        // Small things packed inside the glass would shimmer with their own
        // shadows, so they don't cast any.
        static void NoShadows(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        // An open jar on the packing towel is being packed there; once its lid
        // is on it can be picked straight up.
        //
        // In the canner, a jar lifts out only into empty hands, so looking into
        // the canner with a jar in hand still puts it in.
        public bool AllowPickup(PlayerHands hands)
        {
            if (GetComponentInParent<Canner>() != null)
            {
                return hands.IsEmpty;
            }
            return HasLid || GetComponentInParent<JarPackingStation>() == null;
        }

        // An open jar can be poured out at the sink and packed again.
        public bool HasContents => !HasLid && (SliceCount > 0 || CloveCount > 0 || HasDill || BrineLevel > 0f);

        public void Empty()
        {
            for (int i = inside.childCount - 1; i >= 0; i--)
            {
                Transform child = inside.GetChild(i);
                if (child != brine)
                {
                    Destroy(child.gameObject);
                }
            }
            SliceCount = 0;
            CloveCount = 0;
            HasDill = false;
            BrineLevel = 0f;
            ShowBrine();
        }

        // A jar has no spout to stream from; it just empties.
        public void SetPouring(bool pouring) { }

        // A slice drops in and settles on the pile inside, a little askew.
        public void AddSlice(Transform slice, float delay)
        {
            int index = SliceCount++;
            float y = 0.004f + index * sliceStep;
            Vector2 jitter = Random.insideUnitCircle * (innerRadius - 0.026f);
            var end = new Vector3(jitter.x, y, jitter.y);
            var tilt = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(0f, 360f), Random.Range(-25f, 25f));
            slice.SetParent(inside, worldPositionStays: true);
            NoShadows(slice);
            StartCoroutine(Drop(slice, end, tilt, delay));
        }

        // A garlic clove drops in and tucks in among the slices. There's no
        // limit; past the recipe's count it's just extra garlic.
        public void AddClove(Transform clove, float delay)
        {
            CloveCount++;
            clove.SetParent(inside, worldPositionStays: true);
            NoShadows(clove);
            StartCoroutine(Drop(clove, ClovePlace(), Random.rotation, delay));
        }

        // Somewhere low in the jar, among whatever is already packed.
        Vector3 ClovePlace()
        {
            float top = Mathf.Max(0.01f, SliceCount * sliceStep);
            Vector2 jitter = Random.insideUnitCircle * (innerRadius - 0.012f);
            return new Vector3(jitter.x, Random.Range(0.006f, Mathf.Min(top, innerHeight - 0.01f)), jitter.y);
        }

        // The dill goes in standing up along the side of the jar.
        public void AddDill(Pickup dill)
        {
            HasDill = true;
            Sfx.Play(SoundId.DillRustle, Mouth);
            dill.AttachTo(inside, new Vector3(innerRadius * 0.4f, 0.01f, 0f), Quaternion.Euler(0f, 0f, 8f), 0.35f, collidersOn: false);
            dill.transform.localScale = Vector3.Scale(dill.transform.localScale, new Vector3(0.6f, 0.9f, 0.6f));
            NoShadows(dill.transform);
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
            Sfx.Play(SoundId.SliceDrop, slice.position, 1f, 0.15f);
        }
    }
}
