using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // Lay produce on the board, step up, pick up the knife, and chop it into
    // slices. Each press is one chop where the knife is; slices tumble to a
    // pile at the side of the board. A head of garlic needs no knife: press
    // it to break it into cloves, then peel each one; peeled cloves gather
    // in a little pile of their own. A tomato takes two chops with the knife,
    // one to halve it and one to quarter it, and the wedges gather in a pile
    // of their own.
    //
    // More produce can wait in a row at the back of the board (set it down
    // one at a time, or empty a basket onto it); when one is finished the
    // next rolls forward. For salsa, carry the cooking pot over and scrape
    // everything chopped into it.
    public class CuttingBoardStation : Station, ISaveable, IAfterLoad
    {
        [System.Serializable]
        struct SliceLook
        {
            [Tooltip("The produce's name, as its slices are named (Onion).")]
            public string produce;
            public Material material;
            [Tooltip("Diameter and thickness of a saved slice, in meters.")]
            public Vector2 size;
        }

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

        [Header("Garlic")]
        [Tooltip("Where peeled cloves gather, on the board top.")]
        [SerializeField] Transform clovePile;
        [SerializeField] Material garlicSkinMaterial;
        [SerializeField] Material cloveMaterial;
        [Tooltip("Width, height and length of a clove, in meters.")]
        [SerializeField] Vector3 cloveSize = new Vector3(0.014f, 0.012f, 0.024f);
        [Tooltip("How far from the middle of the board the broken cloves land.")]
        [SerializeField] float cloveSpread = 0.06f;

        [Header("Tomatoes")]
        [Tooltip("Where tomato wedges gather, on the board top.")]
        [SerializeField] Transform wedgePile;
        [Tooltip("Width, height and length of a wedge, in meters.")]
        [SerializeField] Vector3 wedgeSize = new Vector3(0.034f, 0.026f, 0.036f);
        [Tooltip("For rebuilding saved wedges.")]
        [SerializeField] Material wedgeMaterial;

        [Header("Waiting their turn")]
        [Tooltip("Where produce waits in a row at the back of the board. Its X axis runs along the row.")]
        [SerializeField] Transform waitingRow;
        [SerializeField] int maxWaiting = 6;
        [SerializeField] float waitingSpacing = 0.075f;

        [Header("Rebuilding saved chips")]
        [SerializeField] Material chipMaterial;
        [Tooltip("Diameter and thickness of a saved chip, in meters.")]
        [SerializeField] Vector2 chipSize = new Vector2(0.05f, 0.006f);
        [Tooltip("Slices of other produce (onion, jalapeño), by name. Anything else comes back as a pickle chip.")]
        [SerializeField] SliceLook[] sliceLooks;

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
        GarlicBulb garlic;
        // Cloves broken off the head, still in their skins.
        readonly List<Transform> unpeeled = new List<Transform>();
        readonly List<Transform> pileCloves = new List<Transform>();
        int clovePileCount;
        Quarterable tomato;
        // The two halves after the first chop.
        readonly List<Transform> halves = new List<Transform>();
        readonly List<Transform> pileWedges = new List<Transform>();
        int wedgePileCount;
        // Produce waiting at the back of the board, next one first.
        readonly List<Pickup> waiting = new List<Pickup>();

        // Cut slices resting on the board, ready to be packed.
        public int SliceCount => Settled(pileSlices);
        public Vector3 PilePosition => slicePile.position;
        // Peeled cloves resting on the board.
        public int CloveCount => Settled(pileCloves);
        public Vector3 ClovePilePosition => clovePile.position;
        // Tomato wedges resting on the board.
        public int WedgeCount => Settled(pileWedges);
        public Vector3 WedgePilePosition => wedgePile.position;
        readonly List<float> thicknesses = new List<float>();

        bool HasSomethingToCut => sliceable != null || tomato != null;
        bool HasGarlicWork => garlic != null || unpeeled.Count > 0;

        static bool IsChoppable(Pickup produce) => produce != null
            && (produce.GetComponent<Sliceable>() != null || produce.GetComponent<GarlicBulb>() != null || produce.GetComponent<Quarterable>() != null);

        bool SlotFree => item == null && unpeeled.Count == 0;
        bool HasRoom => SlotFree || Waiting.Count < maxWaiting;
        int ChoppedCount => SliceCount + WedgeCount + CloveCount;

        public override bool CanInteract(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                Pickup held = hands.Held;
                if (held.GetComponent<CookingPot>() is CookingPot pot)
                {
                    return ChoppedCount > 0 && pot.RoomForPieces > 0;
                }
                if (held.OwnContainer != null)
                {
                    return HasRoom && FirstChoppable(held.OwnContainer) != null;
                }
                return HasRoom && IsChoppable(held);
            }
            return HasSomethingToCut || HasGarlicWork || Waiting.Count > 0;
        }

        public override void Interact(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                Pickup held = hands.Held;
                if (held.GetComponent<CookingPot>() is CookingPot pot)
                {
                    ScrapeInto(pot);
                }
                else if (held.OwnContainer != null)
                {
                    EmptyOnto(held.OwnContainer);
                }
                else
                {
                    Accept(hands.TakeHeld());
                }
                return;
            }
            NextUp();
            enteredFrame = Time.frameCount;
            Enter(hands);
        }

        public override string PromptVerb(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                Pickup held = hands.Held;
                if (held.GetComponent<CookingPot>() != null) return "Scrape into pot";
                if (held.OwnContainer != null) return "Empty onto board";
                return SlotFree ? "Lay on board" : "Set by board";
            }
            return HasGarlicWork ? "Peel garlic" : "Slice";
        }

        // Produce still in the row (anything picked back up has left it).
        List<Pickup> Waiting
        {
            get
            {
                waiting.RemoveAll(p => p == null || p.transform.parent != waitingRow);
                return waiting;
            }
        }

        static Pickup FirstChoppable(Container basket)
        {
            foreach (var inside in basket.Contents)
            {
                if (IsChoppable(inside)) return inside;
            }
            return null;
        }

        // On the board if it's free, otherwise at the end of the row.
        void Accept(Pickup produce)
        {
            if (SlotFree)
            {
                LayOnBoard(produce);
                return;
            }
            Waiting.Add(produce);
            produce.AttachTo(waitingRow, WaitingSpot(waiting.Count - 1, produce), WaitingTilt(produce), settings.placeGlideSeconds, collidersOn: true);
        }

        // Everything choppable in the basket comes out, as far as there's room.
        void EmptyOnto(Container basket)
        {
            var produce = new List<Pickup>();
            foreach (var inside in basket.Contents)
            {
                if (IsChoppable(inside)) produce.Add(inside);
            }
            foreach (var p in produce)
            {
                if (!HasRoom)
                {
                    break;
                }
                basket.Remove(p);
                Accept(p);
            }
        }

        // The next one in the row rolls forward onto the board.
        void NextUp()
        {
            if (!SlotFree || Waiting.Count == 0)
            {
                return;
            }
            Pickup next = waiting[0];
            waiting.RemoveAt(0);
            LayOnBoard(next);
            for (int i = 0; i < waiting.Count; i++)
            {
                waiting[i].AttachTo(waitingRow, WaitingSpot(i, waiting[i]), WaitingTilt(waiting[i]), 0.25f, collidersOn: true);
            }
        }

        // Long produce (cucumbers, peppers) lies on its side, pointing across the row.
        Vector3 WaitingSpot(int index, Pickup produce)
        {
            var slicer = produce.GetComponent<Sliceable>();
            float height = slicer != null ? slicer.Diameter / 2f : produce.RestHalfHeight;
            return new Vector3(index * waitingSpacing, height, 0f);
        }

        static Quaternion WaitingTilt(Pickup produce) => produce.GetComponent<Sliceable>() != null ? Quaternion.Euler(90f, 0f, 0f) : produce.RestTilt;

        // Everything chopped slides off the board into the pot.
        void ScrapeInto(CookingPot pot)
        {
            int room = pot.RoomForPieces;
            var scraped = new List<Transform>();
            scraped.AddRange(TakeWedges(room));
            scraped.AddRange(TakeSlices(room - scraped.Count));
            scraped.AddRange(TakeCloves(room - scraped.Count));
            if (scraped.Count > 0)
            {
                pot.AddPieces(scraped);
            }
        }

        protected override IEnumerable<Vector3> SnapTargets()
        {
            if (garlic != null)
            {
                yield return garlic.transform.position;
            }
            foreach (var clove in unpeeled)
            {
                yield return clove.position;
            }
            if (!knifeHeld && !HasGarlicWork)
            {
                yield return knife.position;
            }
        }

        protected override void OnLeave()
        {
            knifeHeld = false;
            chopTime = -1f;
        }

        [System.Serializable]
        class State
        {
            public int slices;
            public int cloves;
            public int unpeeled;
            public int wedges;
            public List<string> sliceNames = new List<string>();
        }

        // Chips, cloves and unpeeled cloves waiting on the board are saved as
        // counts and laid out again.
        public string SaveKey => "board:" + name;
        public string Capture()
        {
            var state = new State { slices = pileSlices.Count, cloves = pileCloves.Count, unpeeled = unpeeled.Count, wedges = pileWedges.Count };
            foreach (var slice in pileSlices) state.sliceNames.Add(slice.name);
            return JsonUtility.ToJson(state);
        }

        public void Restore(string json)
        {
            foreach (var piece in pileSlices) Destroy(piece.gameObject);
            foreach (var piece in pileCloves) Destroy(piece.gameObject);
            foreach (var piece in unpeeled) Destroy(piece.gameObject);
            foreach (var piece in pileWedges) Destroy(piece.gameObject);
            pileWedges.Clear();
            wedgePileCount = 0;
            pileSlices.Clear();
            pileCloves.Clear();
            unpeeled.Clear();
            tumbling.Clear();
            pileCount = 0;
            clovePileCount = 0;
            // Older saves stored just the number of chips.
            var state = json.StartsWith("{") ? JsonUtility.FromJson<State>(json) : new State { slices = int.Parse(json) };

            for (int i = 0; i < state.cloves; i++)
            {
                Transform clove = MakeClove(cloveMaterial);
                clove.SetParent(clovePile, false);
                clove.SetLocalPositionAndRotation(ClovePileSpot(clovePileCount++), ClovePileRotation(i));
                pileCloves.Add(clove);
            }
            for (int i = 0; i < state.wedges; i++)
            {
                Transform wedge = JarContents.MakeWedge(wedgeMaterial, wedgeSize);
                wedge.SetParent(wedgePile, false);
                wedge.SetLocalPositionAndRotation(WedgePileSpot(wedgePileCount), WedgePileRotation(wedgePileCount));
                wedgePileCount++;
                pileWedges.Add(wedge);
            }
            for (int i = 0; i < state.unpeeled; i++)
            {
                Transform clove = MakeClove(garlicSkinMaterial);
                clove.SetParent(itemSlot, false);
                clove.SetLocalPositionAndRotation(BrokenSpot(i, state.unpeeled), BrokenRotation(i, state.unpeeled));
                unpeeled.Add(clove);
            }

            int count = state.slices;
            if (count == 0)
            {
                return;
            }
            pile = new GameObject("Slices").transform;
            pile.SetParent(slicePile, false);
            for (int i = 0; i < count; i++)
            {
                string sliceName = state.sliceNames != null && i < state.sliceNames.Count ? state.sliceNames[i] : "Cucumber Slice";
                Material look = chipMaterial;
                Vector2 size = chipSize;
                foreach (var known in sliceLooks)
                {
                    if (sliceName.StartsWith(known.produce))
                    {
                        look = known.material;
                        size = known.size;
                    }
                }
                Transform slice = MakeRound(sliceName, look).transform;
                slice.SetParent(pile, false);
                slice.localScale = new Vector3(size.x, size.y / 2f, size.x);
                int n = pileCount++;
                float d = size.x;
                slice.SetLocalPositionAndRotation(
                    new Vector3((n % 4) * d * 0.8f, size.y / 2f + (n / 12) * 0.004f, ((n / 4) % 3) * d * 0.8f),
                    Quaternion.Euler(0f, n * 47f, 0f));
                pileSlices.Add(slice);
            }
        }

        // Produce left lying on the board, or waiting behind it, is picked up where it was.
        public void AfterLoad()
        {
            var lying = itemSlot.GetComponentInChildren<Pickup>();
            if (IsChoppable(lying))
            {
                LayOnBoard(lying);
            }
            waiting.Clear();
            if (waitingRow != null)
            {
                foreach (Transform child in waitingRow)
                {
                    var produce = child.GetComponent<Pickup>();
                    if (IsChoppable(produce)) waiting.Add(produce);
                }
            }
            NextUp();
        }

        void LayOnBoard(Pickup produce)
        {
            item = produce;
            garlic = produce.GetComponent<GarlicBulb>();
            if (garlic != null)
            {
                sliceable = null;
                produce.AttachTo(itemSlot, Vector3.up * produce.RestHalfHeight, produce.RestTilt, settings.placeGlideSeconds, collidersOn: false);
                return;
            }
            tomato = produce.GetComponent<Quarterable>();
            if (tomato != null)
            {
                sliceable = null;
                halves.Clear();
                produce.AttachTo(itemSlot, Vector3.up * produce.RestHalfHeight, produce.RestTilt, settings.placeGlideSeconds, collidersOn: false);
                return;
            }
            sliceable = produce.GetComponent<Sliceable>();
            remaining = null;
            leftEnd = -sliceable.Length / 2f;
            cutEnd = sliceable.Length / 2f;
            thicknesses.Clear();
            // It lies along the cutting line, ready for the knife.
            produce.AttachTo(itemSlot, Vector3.up * sliceable.Diameter / 2f, Quaternion.Euler(0f, 0f, 90f), settings.placeGlideSeconds, collidersOn: false);
        }

        protected override void WhileWorking()
        {
            // The press that opened the station shouldn't also grab the knife.
            if (Time.frameCount == enteredFrame)
            {
                return;
            }

            if (HasGarlicWork)
            {
                WorkGarlic();
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
                float half = tomato != null ? tomato.Diameter * 0.3f : 0f;
                knifeX = sliceable != null ? Mathf.Clamp(x, leftEnd, cutEnd)
                    : tomato != null ? Mathf.Clamp(x, -half, half)
                    : Mathf.Clamp(x, -0.15f, 0.15f);
            }
        }

        void MoveKnife()
        {
            Vector3 position;
            Quaternion rotation;

            if (knifeHeld)
            {
                float hover = (sliceable != null ? sliceable.Diameter : tomato != null ? tomato.Diameter : 0.03f) + knifeHoverHeight;
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
            if (tomato != null)
            {
                ChopTomato();
                return;
            }

            if (remaining == null)
            {
                StartCutting();
            }

            // Slices come off whichever end the knife is nearer, so you can
            // work from either side.
            bool fromLeft = x - leftEnd < cutEnd - x;
            if ((fromLeft ? x - leftEnd : cutEnd - x) < minSliceThickness)
            {
                return; // Just a tap on the very end.
            }

            if (fromLeft)
            {
                CutSlice(leftEnd, x);
                leftEnd = x;
            }
            else
            {
                CutSlice(x, cutEnd);
                cutEnd = x;
            }

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

            var slice = MakeRound(item.DisplayName + " Slice", sliceable.FleshMaterial).transform;
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
            var taken = Take(pileSlices, count);
            if (pileSlices.Count == 0)
            {
                pileCount = 0;
            }
            return taken;
        }

        // First chop: two halves fall open. Second: they're cut again, and the
        // four wedges hop onto their pile.
        void ChopTomato()
        {
            float d = tomato.Diameter;
            Material skin = item.GetComponent<Renderer>().sharedMaterial;
            if (halves.Count == 0)
            {
                item.GetComponent<Renderer>().enabled = false;
                foreach (var part in item.GetComponentsInChildren<Renderer>()) part.enabled = false;
                for (int side = -1; side <= 1; side += 2)
                {
                    var half = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                    half.name = "Tomato Half";
                    Destroy(half.GetComponent<Collider>());
                    half.GetComponent<Renderer>().sharedMaterial = skin;
                    half.SetParent(itemSlot, false);
                    half.localScale = new Vector3(d * 0.5f, d * 0.9f, d);
                    half.localPosition = new Vector3(0f, d * 0.45f, 0f);
                    halves.Add(half);
                    // The cut face shows red flesh.
                    var face = MakeRound("Cut Face", tomato.FleshMaterial).transform;
                    face.SetParent(half, false);
                    face.localPosition = new Vector3(-side * 0.02f, 0f, 0f);
                    face.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    face.localScale = new Vector3(0.95f, 0.01f, 0.95f);
                    StartCoroutine(Tumble(half, new Vector3(side * d * 0.4f, d * 0.25f, 0f), Quaternion.Euler(0f, 0f, side * 90f)));
                }
                return;
            }

            foreach (var half in halves)
            {
                Vector3 from = half.localPosition;
                tumbling.Remove(half);
                Destroy(half.gameObject);
                for (int k = 0; k < 2; k++)
                {
                    Transform wedge = JarContents.MakeWedge(tomato.FleshMaterial, wedgeSize);
                    wedge.SetParent(itemSlot, false);
                    wedge.localPosition = from;
                    int i = wedgePileCount++;
                    wedge.SetParent(wedgePile, true);
                    pileWedges.Add(wedge);
                    StartCoroutine(Tumble(wedge, WedgePileSpot(i), WedgePileRotation(i)));
                }
            }
            halves.Clear();
            Destroy(item.gameObject);
            item = null;
            tomato = null;
            NextUp();
        }

        Vector3 WedgePileSpot(int i) => new Vector3((i % 3) * 0.032f, wedgeSize.y / 2f + (i / 9) * 0.02f, ((i / 3) % 3) * 0.036f);

        Quaternion WedgePileRotation(int i) => Quaternion.Euler(0f, i * 37f, 0f);

        public List<Transform> TakeWedges(int count)
        {
            var taken = Take(pileWedges, count);
            if (pileWedges.Count == 0)
            {
                wedgePileCount = 0;
            }
            return taken;
        }

        public void ReturnWedges(List<Transform> wedges)
        {
            foreach (var wedge in wedges)
            {
                int i = wedgePileCount++;
                wedge.SetParent(wedgePile, true);
                pileWedges.Add(wedge);
                StartCoroutine(Tumble(wedge, WedgePileSpot(i), WedgePileRotation(i)));
            }
        }

        public List<Transform> TakeCloves(int count)
        {
            var taken = Take(pileCloves, count);
            if (pileCloves.Count == 0)
            {
                clovePileCount = 0;
            }
            return taken;
        }

        // Slices carried off but not used go back on the pile.
        public void ReturnSlices(List<Transform> slices)
        {
            if (pile == null)
            {
                pile = new GameObject("Slices").transform;
                pile.SetParent(slicePile, false);
            }
            foreach (var slice in slices)
            {
                int i = pileCount++;
                float d = slice.localScale.x;
                Vector3 spot = new Vector3((i % 4) * d * 0.8f, slice.localScale.y + (i / 12) * 0.004f, ((i / 4) % 3) * d * 0.8f);
                slice.SetParent(pile, true);
                pileSlices.Add(slice);
                StartCoroutine(Tumble(slice, spot, Quaternion.Euler(0f, i * 47f, 0f)));
            }
        }

        public void ReturnCloves(List<Transform> cloves)
        {
            foreach (var clove in cloves)
            {
                int i = clovePileCount++;
                clove.SetParent(clovePile, true);
                pileCloves.Add(clove);
                StartCoroutine(Tumble(clove, ClovePileSpot(i), ClovePileRotation(i)));
            }
        }

        List<Transform> Take(List<Transform> pieces, int count)
        {
            var taken = new List<Transform>();
            for (int i = pieces.Count - 1; i >= 0 && taken.Count < count; i--)
            {
                Transform piece = pieces[i];
                if (!tumbling.Contains(piece))
                {
                    pieces.RemoveAt(i);
                    taken.Add(piece);
                }
            }
            return taken;
        }

        int Settled(List<Transform> pieces)
        {
            int settled = 0;
            foreach (var piece in pieces)
            {
                if (!tumbling.Contains(piece)) settled++;
            }
            return settled;
        }

        // Garlic needs no knife: press the head to break it, then peel each clove.
        void WorkGarlic()
        {
            if (knifeHeld)
            {
                knifeHeld = false;
                reticle.Hidden = false;
            }
            bool pressed = UseAction.WasPressedThisFrame();
            reticle.Highlight = false;
            UseHint = null;

            if (garlic != null)
            {
                if (CursorNear(garlic.transform.position, knifeGrabRadius))
                {
                    reticle.Highlight = true;
                    UseHint = "Break into cloves";
                    if (pressed)
                    {
                        BreakBulb();
                    }
                }
                return;
            }

            foreach (var clove in unpeeled)
            {
                if (tumbling.Contains(clove) || !CursorNear(clove.position, knifeGrabRadius * 0.6f))
                {
                    continue;
                }
                reticle.Highlight = true;
                UseHint = "Peel";
                if (pressed)
                {
                    Peel(clove);
                }
                return;
            }
        }

        void BreakBulb()
        {
            Vector3 from = itemSlot.InverseTransformPoint(garlic.transform.position);
            int count = garlic.Cloves;
            for (int i = 0; i < count; i++)
            {
                Transform clove = MakeClove(garlicSkinMaterial);
                clove.SetParent(itemSlot, false);
                clove.localPosition = from;
                unpeeled.Add(clove);
                StartCoroutine(Tumble(clove, BrokenSpot(i, count), BrokenRotation(i, count)));
            }
            Sfx.Play(SoundId.SetDown, garlic.transform.position, 1f, 0.1f);
            Destroy(garlic.gameObject);
            garlic = null;
            item = null;
        }

        // The papery skin flies off and the bare clove hops onto its pile.
        void Peel(Transform clove)
        {
            unpeeled.Remove(clove);
            Sfx.Play(SoundId.Paper, clove.position, 0.8f, 0.15f);
            StartCoroutine(SkinFlake(clove.position, clove.rotation));
            clove.GetComponent<Renderer>().sharedMaterial = cloveMaterial;
            int i = clovePileCount++;
            clove.SetParent(clovePile, true);
            pileCloves.Add(clove);
            StartCoroutine(Tumble(clove, ClovePileSpot(i), ClovePileRotation(i)));
            NextUp();
        }

        IEnumerator SkinFlake(Vector3 position, Quaternion rotation)
        {
            Transform flake = MakeClove(garlicSkinMaterial);
            flake.SetPositionAndRotation(position, rotation);
            flake.localScale = new Vector3(cloveSize.x * 1.1f, cloveSize.y * 0.15f, cloveSize.z);
            Vector3 drift = new Vector3(Random.Range(-0.04f, 0.04f), 0f, Random.Range(-0.04f, 0.04f));
            Vector3 size = flake.localScale;
            const float seconds = 0.45f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float t = time / seconds;
                flake.position = position + drift * t + Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.04f;
                flake.rotation = rotation * Quaternion.Euler(t * 220f, t * 90f, 0f);
                flake.localScale = size * (1f - t);
                yield return null;
            }
            Destroy(flake.gameObject);
        }

        // Broken cloves land in a loose ring on the board, lying on their sides.
        Vector3 BrokenSpot(int i, int count)
        {
            float angle = i * Mathf.PI * 2f / Mathf.Max(count, 1);
            return new Vector3(Mathf.Cos(angle) * cloveSpread, cloveSize.y / 2f, Mathf.Sin(angle) * cloveSpread * 0.7f);
        }

        Quaternion BrokenRotation(int i, int count) => Quaternion.Euler(0f, -i * 360f / Mathf.Max(count, 1) + 90f, 0f);

        Vector3 ClovePileSpot(int i) => new Vector3((i % 4) * 0.018f, cloveSize.y / 2f + (i / 8) * 0.01f, ((i / 4) % 2) * 0.03f);

        Quaternion ClovePileRotation(int i) => Quaternion.Euler(0f, i * 61f, 0f);

        Transform MakeClove(Material material)
        {
            var clove = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            clove.name = "Clove";
            Destroy(clove.GetComponent<Collider>());
            var look = clove.GetComponent<Renderer>();
            look.sharedMaterial = material;
            look.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            clove.transform.localScale = cloveSize;
            return clove.transform;
        }

        IEnumerator Tumble(Transform slice, Vector3 localEnd, Quaternion localEndRotation)
        {
            tumbling.Add(slice);
            Vector3 start = slice.localPosition;
            Quaternion startRotation = slice.localRotation;
            const float seconds = 0.25f;
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                // Cut again before it landed (a quick second chop on a tomato).
                if (slice == null)
                {
                    yield break;
                }
                float t = 1f - Mathf.Pow(1f - time / seconds, 2f);
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.02f;
                slice.SetLocalPositionAndRotation(Vector3.Lerp(start, localEnd, t) + arc, Quaternion.Slerp(startRotation, localEndRotation, t));
                yield return null;
            }
            tumbling.Remove(slice);
            if (slice != null)
            {
                slice.SetLocalPositionAndRotation(localEnd, localEndRotation);
            }
        }

        void FinishItem()
        {
            Destroy(remaining.gameObject);
            Destroy(item.gameObject);
            remaining = null;
            item = null;
            sliceable = null;
            LogEvenness();
            NextUp();
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
