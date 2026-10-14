using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // Lay produce on the board, step up, pick up the knife, and chop it into
    // slices. Each press is one chop where the knife is; slices tumble to a
    // pile at the side of the board.
    public class CuttingBoardStation : Station, ISaveable, IAfterLoad
    {
        [Tooltip("Center of the board's top. Its X axis is the direction of cutting.")]
        [SerializeField] Transform itemSlot;
        [SerializeField] Transform knife;
        [Tooltip("Where the knife lies when not in use.")]
        [SerializeField] Transform knifeRest;
        [Tooltip("Where cut slices gather, on the board top.")]
        [SerializeField] Transform slicePile;

        [Header("Cutting")]
        [SerializeField] float minSliceThickness = 0.003f;
        [Tooltip("When this little is left, the end piece falls off on its own.")]
        [SerializeField] float heelLength = 0.012f;
        [SerializeField] float knifeHoverHeight = 0.035f;
        [SerializeField] float chopSeconds = 0.2f;
        [Tooltip("How close the cursor must be to the knife to pick it up, in screen heights.")]
        [SerializeField] float knifeGrabRadius = 0.07f;

        [Header("Rebuilding saved chips")]
        [SerializeField] Material chipMaterial;
        [Tooltip("Diameter and thickness of a saved chip, in meters.")]
        [SerializeField] Vector2 chipSize = new Vector2(0.05f, 0.006f);

        Pickup item;
        Sliceable sliceable;
        Transform remaining;
        float leftEnd;
        float cutEnd;
        bool knifeHeld;
        float knifeX;
        float chopTime = -1f;
        bool chopLanded;
        int enteredFrame;
        Transform pile;
        int pileCount;
        readonly List<Transform> pileSlices = new List<Transform>();
        readonly HashSet<Transform> tumbling = new HashSet<Transform>();

        // Cut slices resting on the board, ready to be packed.
        public int SliceCount => pileSlices.Count - tumbling.Count;
        public Vector3 PilePosition => slicePile.position;
        readonly List<float> thicknesses = new List<float>();

        bool HasSomethingToCut => item != null;

        public override bool CanInteract(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                return item == null && hands.Held.GetComponent<Sliceable>() != null;
            }
            return HasSomethingToCut;
        }

        public override void Interact(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                LayOnBoard(hands.TakeHeld());
                return;
            }
            enteredFrame = Time.frameCount;
            Enter(hands);
        }

        public override string PromptVerb(PlayerHands hands) => hands.IsEmpty ? "Slice" : "Lay on board";

        protected override IEnumerable<Vector3> SnapTargets()
        {
            if (!knifeHeld)
            {
                yield return knife.position;
            }
        }

        protected override void OnLeave()
        {
            knifeHeld = false;
            chopTime = -1f;
        }

        // Chips waiting on the board are saved as a count and laid out again.
        public string SaveKey => "board:" + name;
        public string Capture() => pileSlices.Count.ToString();

        public void Restore(string json)
        {
            foreach (var slice in pileSlices)
            {
                Destroy(slice.gameObject);
            }
            pileSlices.Clear();
            tumbling.Clear();
            pileCount = 0;
            int count = int.Parse(json);
            if (count == 0)
            {
                return;
            }
            pile = new GameObject("Slices").transform;
            pile.SetParent(slicePile, false);
            for (int i = 0; i < count; i++)
            {
                Transform slice = MakeRound("Slice", chipMaterial).transform;
                slice.SetParent(pile, false);
                slice.localScale = new Vector3(chipSize.x, chipSize.y / 2f, chipSize.x);
                int n = pileCount++;
                float d = chipSize.x;
                slice.SetLocalPositionAndRotation(
                    new Vector3((n % 4) * d * 0.8f, chipSize.y / 2f + (n / 12) * 0.004f, ((n / 4) % 3) * d * 0.8f),
                    Quaternion.Euler(0f, n * 47f, 0f));
                pileSlices.Add(slice);
            }
        }

        // A cucumber left lying on the board is picked up where it was.
        public void AfterLoad()
        {
            var lying = itemSlot.GetComponentInChildren<Pickup>();
            if (lying != null && lying.GetComponent<Sliceable>() != null)
            {
                LayOnBoard(lying);
            }
        }

        void LayOnBoard(Pickup produce)
        {
            item = produce;
            sliceable = produce.GetComponent<Sliceable>();
            remaining = null;
            leftEnd = -sliceable.Length / 2f;
            cutEnd = sliceable.Length / 2f;
            thicknesses.Clear();
            produce.AttachTo(itemSlot, Vector3.up * produce.RestHalfHeight, produce.RestTilt, settings.placeGlideSeconds, collidersOn: false);
        }

        protected override void WhileWorking()
        {
            // The press that opened the station shouldn't also grab the knife.
            if (Time.frameCount == enteredFrame)
            {
                return;
            }

            if (!knifeHeld)
            {
                bool nearKnife = CursorNear(knife.position, knifeGrabRadius);
                reticle.Highlight = nearKnife;
                UseHint = nearKnife ? "Pick up knife" : null;
                if (nearKnife && UseAction.WasPressedThisFrame())
                {
                    knifeHeld = true;
                    Sfx.Play(SoundId.PickUp, knife.position);
                    reticle.Hidden = true;
                    knifeX = cutEnd;
                }
                return;
            }

            AimKnife();
            UseHint = HasSomethingToCut ? "Chop" : null;

            if (chopTime < 0f && UseAction.WasPressedThisFrame())
            {
                chopTime = 0f;
                chopLanded = false;
            }
        }

        protected override void Update()
        {
            base.Update();
            MoveKnife();
        }

        void AimKnife()
        {
            // The knife follows the cursor along the cutting line only, so a
            // mouse or a single stick axis is all it takes.
            var board = new Plane(itemSlot.up, itemSlot.position);
            Ray ray = CursorRay();
            if (chopTime < 0f && board.Raycast(ray, out float distance))
            {
                float x = itemSlot.InverseTransformPoint(ray.GetPoint(distance)).x;
                knifeX = HasSomethingToCut ? Mathf.Clamp(x, leftEnd, cutEnd) : Mathf.Clamp(x, -0.15f, 0.15f);
            }
        }

        void MoveKnife()
        {
            Vector3 position;
            Quaternion rotation;

            if (knifeHeld)
            {
                float hover = (sliceable != null ? sliceable.Diameter : 0.03f) + knifeHoverHeight;
                float height = hover;

                if (chopTime >= 0f)
                {
                    chopTime += Time.deltaTime;
                    float phase = chopTime / chopSeconds;
                    // Quick drop, slower lift.
                    height = phase < 0.35f ? hover * (1f - phase / 0.35f) : hover * Mathf.Clamp01((phase - 0.35f) / 0.65f);
                    if (!chopLanded && phase >= 0.35f)
                    {
                        chopLanded = true;
                        Sfx.Play(SoundId.KnifeChop, knife.position);
                        Chop(knifeX);
                    }
                    if (phase >= 1f)
                    {
                        chopTime = -1f;
                    }
                }

                position = itemSlot.TransformPoint(new Vector3(knifeX, height, 0f));
                rotation = itemSlot.rotation;
            }
            else
            {
                position = knifeRest.position;
                rotation = knifeRest.rotation;
            }

            float t = knifeHeld ? 1f - Mathf.Exp(-30f * Time.deltaTime) : 1f - Mathf.Exp(-10f * Time.deltaTime);
            knife.SetPositionAndRotation(Vector3.Lerp(knife.position, position, t), Quaternion.Slerp(knife.rotation, rotation, t));
        }

        void Chop(float x)
        {
            if (!HasSomethingToCut)
            {
                return;
            }

            if (remaining == null)
            {
                StartCutting();
            }

            if (x > cutEnd - minSliceThickness)
            {
                return; // Just a tap on the very end.
            }

            if (x - leftEnd < minSliceThickness)
            {
                // A chop at the far end takes what's left as one last piece,
                // rather than leaving a heel too thin to be a slice.
                CutSlice(leftEnd, cutEnd);
                FinishItem();
                return;
            }

            CutSlice(x, cutEnd);
            cutEnd = x;

            if (cutEnd - leftEnd <= heelLength)
            {
                CutSlice(leftEnd, cutEnd);
                FinishItem();
            }
            else
            {
                ShapeRemaining();
            }
        }

        // Swap the whole cucumber for a simple log that can be shortened as you cut.
        void StartCutting()
        {
            remaining = MakeRound("Remaining", item.GetComponent<Renderer>().sharedMaterial).transform;
            remaining.SetParent(itemSlot, false);
            item.GetComponent<Renderer>().enabled = false;
            ShapeRemaining();

            if (pile == null)
            {
                pile = new GameObject(item.DisplayName + " Slices").transform;
                pile.SetParent(slicePile, false);
                pileCount = 0;
            }
        }

        void ShapeRemaining()
        {
            float length = cutEnd - leftEnd;
            float d = sliceable.Diameter;
            remaining.localPosition = new Vector3((leftEnd + cutEnd) / 2f, d / 2f, 0f);
            remaining.localRotation = Quaternion.Euler(0f, 0f, 90f);
            remaining.localScale = new Vector3(d, length / 2f, d);
        }

        void CutSlice(float from, float to)
        {
            float thickness = to - from;
            thicknesses.Add(thickness);

            var slice = MakeRound("Slice", sliceable.FleshMaterial).transform;
            slice.SetParent(itemSlot, false);
            float d = sliceable.Diameter;
            slice.localPosition = new Vector3((from + to) / 2f, d / 2f, 0f);
            slice.localRotation = Quaternion.Euler(0f, 0f, 90f);
            slice.localScale = new Vector3(d, thickness / 2f, d);

            // Slices fan out in loose rows on the pile spot, lying flat.
            int i = pileCount++;
            Vector3 spot = new Vector3((i % 4) * d * 0.8f, thickness / 2f + (i / 12) * 0.004f, ((i / 4) % 3) * d * 0.8f);
            slice.SetParent(pile, true);
            pileSlices.Add(slice);
            StartCoroutine(Tumble(slice, spot, Quaternion.Euler(0f, i * 47f, 0f)));
        }

        // Hands over up to this many settled slices, newest first (another
        // station, like jar packing, takes them from here).
        public List<Transform> TakeSlices(int count)
        {
            var taken = new List<Transform>();
            for (int i = pileSlices.Count - 1; i >= 0 && taken.Count < count; i--)
            {
                Transform slice = pileSlices[i];
                if (!tumbling.Contains(slice))
                {
                    pileSlices.RemoveAt(i);
                    taken.Add(slice);
                }
            }
            if (pileSlices.Count == 0)
            {
                pileCount = 0;
            }
            return taken;
        }

        IEnumerator Tumble(Transform slice, Vector3 localEnd, Quaternion localEndRotation)
        {
            tumbling.Add(slice);
            Vector3 start = slice.localPosition;
            Quaternion startRotation = slice.localRotation;
            const float seconds = 0.25f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float t = 1f - Mathf.Pow(1f - time / seconds, 2f);
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.02f;
                slice.SetLocalPositionAndRotation(Vector3.Lerp(start, localEnd, t) + arc, Quaternion.Slerp(startRotation, localEndRotation, t));
                yield return null;
            }
            slice.SetLocalPositionAndRotation(localEnd, localEndRotation);
            tumbling.Remove(slice);
        }

        void FinishItem()
        {
            Destroy(remaining.gameObject);
            Destroy(item.gameObject);
            remaining = null;
            item = null;
            sliceable = null;
            LogEvenness();
        }

        void LogEvenness()
        {
            // Not shown to the player yet; recorded for tuning slice quality later.
            float sum = 0f;
            foreach (float t in thicknesses) sum += t;
            float mean = sum / thicknesses.Count;
            float variance = 0f;
            foreach (float t in thicknesses) variance += (t - mean) * (t - mean);
            float spread = Mathf.Sqrt(variance / thicknesses.Count) / mean;
            Debug.Log($"Cut {thicknesses.Count} slices, average {mean * 1000f:F1} mm, unevenness {spread:P0}");
        }

        bool CursorNear(Vector3 worldPoint, float radius)
        {
            Vector3 screen = View.WorldToViewportPoint(worldPoint);
            float aspect = (float)Screen.width / Screen.height;
            Vector2 offset = (Vector2)screen - CursorPoint;
            return screen.z > 0f && new Vector2(offset.x * aspect, offset.y).magnitude < radius;
        }

        static GameObject MakeRound(string name, Material material)
        {
            var round = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            round.name = name;
            Destroy(round.GetComponent<Collider>());
            round.GetComponent<Renderer>().sharedMaterial = material;
            return round;
        }
    }
}
