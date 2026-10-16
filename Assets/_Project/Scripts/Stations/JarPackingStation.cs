using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // A dish towel on the counter between the cutting board and the stove.
    // Set empty jars on it (up to a canner load) and heads of dill beside
    // them, then step up and pack by hand: pick up a handful of slices or
    // garlic cloves from the board, a head of dill, or a pinch of peppercorns
    // from the tin, carry it over a jar and drop it in. A jar of slices is
    // dill pickles; a jar of cloves is pickled garlic. A jar of tomato wedges
    // is canned tomatoes: pressing them in fills it with their own juice, and
    // the lemon juice bottle tips a spoonful into each before its lid. Hold
    // Interact on the pot of boiling brine (on the hot pad
    // or the stove) to pour up to the line, and put a lid on from the stack.
    // Brine and lids go to the jar you're working on while it still needs
    // them, otherwise on to the next jar that does, so you can pour down the
    // row and then lid them all. Lidded jars pick straight up off the towel.
    public class JarPackingStation : Station, IAfterLoad
    {
        enum Carrying { Nothing, Slices, Cloves, Dill, Peppercorns, Wedges }

        [SerializeField] RecipeData recipe;
        [Tooltip("Slices and peeled garlic are taken from this board's piles.")]
        [SerializeField] CuttingBoardStation board;
        [Tooltip("Where jars stand on the towel, in the order they're packed.")]
        [SerializeField] Transform[] jarSpots;
        [Tooltip("Where heads of dill wait beside the jars.")]
        [SerializeField] Transform herbSpot;
        [SerializeField] int maxDill = 4;
        [SerializeField] float dillSpacing = 0.035f;
        [Tooltip("The stack of lids and rings on the towel.")]
        [SerializeField] Transform lidStack;
        [Tooltip("Where the peppercorn tin stands on the towel.")]
        [SerializeField] Transform spiceSpot;
        [Tooltip("Where the lemon juice bottle stands on the towel.")]
        [SerializeField] Transform lemonSpot;

        [Header("Feel")]
        [Tooltip("How close the cursor must be to something to use it, in screen heights.")]
        [SerializeField] float grabRadius = 0.08f;
        [Tooltip("Slices picked up with each grab, and the most you can carry at once.")]
        [SerializeField] int handful = 4;
        [SerializeField] int maxSlicesCarried = 12;
        [SerializeField] int clovesPerGrab = 3;
        [SerializeField] int maxClovesCarried = 12;
        [SerializeField] int wedgesPerGrab = 2;
        [SerializeField] int maxWedgesCarried = 6;
        [Tooltip("How high above the towel things are carried, in meters.")]
        [SerializeField] float carryHeight = 0.2f;
        [SerializeField] float carrySharpness = 18f;
        [Tooltip("How fast the brine rises while pouring (share of the jar per second).")]
        [SerializeField] float pourRate = 0.32f;
        [SerializeField] float pourTilt = 70f;
        [Tooltip("How far above the jar's mouth the lip is held while pouring, in meters.")]
        [SerializeField] float pourHeight = 0.07f;
        [SerializeField] float potFollowSharpness = 10f;

        PlayerHands player;
        // The jar being worked on.
        JarContents jar;
        readonly List<Pickup> dills = new List<Pickup>();
        readonly HashSet<JarContents> logged = new HashSet<JarContents>();
        int enteredFrame;
        bool pouring;
        float tilt;
        // The pot being poured from, and where it rested.
        CookingPot pot;
        Vector3 potRestPosition;
        Quaternion potRestRotation;
        // What's in your hand under the cursor.
        Carrying carrying;
        readonly List<Transform> carried = new List<Transform>();
        Pickup carriedDill;

        public override bool CanInteract(PlayerHands hands)
        {
            if (hands.IsEmpty)
            {
                return FirstOpenJar() != null;
            }
            var heldJar = hands.Held.GetComponent<JarContents>();
            if (heldJar != null)
            {
                return !heldJar.HasLid && FreeSpot() != null;
            }
            if (hands.Held.GetComponent<PeppercornTin>() != null)
            {
                return Tin == null;
            }
            if (hands.Held.GetComponent<LemonJuice>() != null)
            {
                return Lemon == null;
            }
            return dills.Count < maxDill && IsDill(hands.Held);
        }

        PeppercornTin Tin => spiceSpot.GetComponentInChildren<PeppercornTin>();
        LemonJuice Lemon => lemonSpot.GetComponentInChildren<LemonJuice>();

        public override void Interact(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                Pickup item = hands.TakeHeld();
                if (item.GetComponent<JarContents>() != null)
                {
                    item.AttachTo(FreeSpot(), Vector3.zero, Quaternion.identity, settings.placeGlideSeconds, collidersOn: true);
                }
                else if (item.GetComponent<PeppercornTin>() != null)
                {
                    item.AttachTo(spiceSpot, Vector3.zero, Quaternion.identity, settings.placeGlideSeconds, collidersOn: true);
                }
                else if (item.GetComponent<LemonJuice>() != null)
                {
                    item.AttachTo(lemonSpot, Vector3.zero, Quaternion.identity, settings.placeGlideSeconds, collidersOn: true);
                }
                else
                {
                    AddDillHead(item, settings.placeGlideSeconds);
                }
                return;
            }
            player = hands;
            enteredFrame = Time.frameCount;
            if (!IsOpen(jar) || !OnTowel(jar))
            {
                jar = FirstOpenJar();
            }
            Enter(hands);
        }

        public override string PromptVerb(PlayerHands hands)
        {
            if (hands.IsEmpty)
            {
                return "Pack jars";
            }
            if (hands.Held.GetComponent<JarContents>() != null) return "Set jar down";
            if (hands.Held.GetComponent<LemonJuice>() != null) return "Set lemon juice down";
            return hands.Held.GetComponent<PeppercornTin>() != null ? "Set peppercorns down" : "Set dill down";
        }

        protected override IEnumerable<Vector3> SnapTargets()
        {
            if (pouring)
            {
                yield break;
            }
            if (carrying == Carrying.Nothing)
            {
                if (board.SliceCount > 0)
                {
                    yield return board.PilePosition;
                }
                if (board.CloveCount > 0)
                {
                    yield return board.ClovePilePosition;
                }
                if (board.WedgeCount > 0)
                {
                    yield return board.WedgePilePosition;
                }
                if (Lemon != null && LemonTarget() != null)
                {
                    yield return Lemon.transform.position;
                }
                if (dills.Count > 0)
                {
                    yield return dills[0].transform.position;
                }
                if (Tin != null)
                {
                    yield return Tin.transform.position;
                }
                if (LidTarget() != null)
                {
                    yield return lidStack.position;
                }
                CookingPot ready = CookingPot.ReadyPot();
                if (ready != null && BrineTarget() != null)
                {
                    yield return ready.transform.position;
                }
            }
            foreach (var onTowel in JarsOnTowel())
            {
                yield return onTowel.Mouth;
            }
        }

        protected override void OnLeave()
        {
            StopPouring();
            PutBackCarried();
        }

        protected override void WhileWorking()
        {
            if (pouring)
            {
                UseHint = "Hold to pour";
                Pour();
                return;
            }
            UseHint = null;
            reticle.Highlight = false;

            // A jar that was finished or taken away hands over to the next one.
            if (!IsOpen(jar) || !OnTowel(jar))
            {
                jar = FirstOpenJar();
            }

            bool pressed = Time.frameCount != enteredFrame && UseAction.WasPressedThisFrame();
            if (carrying != Carrying.Nothing)
            {
                WhileCarrying(pressed);
                return;
            }
            if (PickUpIngredient(pressed) || UseTools(pressed))
            {
                return;
            }

            foreach (var other in JarsOnTowel())
            {
                if (!CursorNear(other.Mouth))
                {
                    continue;
                }
                if (other.HasLid)
                {
                    Hint("Pick up jar");
                    if (pressed)
                    {
                        TakeJar(other);
                    }
                }
                else if (other != jar)
                {
                    Hint("Work on this jar");
                    if (pressed)
                    {
                        jar = other;
                    }
                }
                return;
            }
        }

        // Slices, a garlic clove, or a head of dill come up into your hand.
        bool PickUpIngredient(bool pressed)
        {
            if (board.SliceCount > 0 && CursorNear(board.PilePosition))
            {
                Hint("Pick up slices");
                if (pressed)
                {
                    Grab(Carrying.Slices, board.TakeSlices(handful));
                }
                return true;
            }
            if (board.CloveCount > 0 && CursorNear(board.ClovePilePosition))
            {
                Hint("Pick up cloves");
                if (pressed)
                {
                    Grab(Carrying.Cloves, board.TakeCloves(clovesPerGrab));
                }
                return true;
            }
            if (board.WedgeCount > 0 && CursorNear(board.WedgePilePosition))
            {
                Hint("Pick up tomatoes");
                if (pressed)
                {
                    Grab(Carrying.Wedges, board.TakeWedges(wedgesPerGrab));
                }
                return true;
            }
            if (Tin is PeppercornTin tin && CursorNear(tin.transform.position))
            {
                Hint("Pick up a pinch of peppercorns");
                if (pressed)
                {
                    Grab(Carrying.Peppercorns, tin.TakePinch());
                }
                return true;
            }
            if (dills.Count > 0 && CursorNear(dills[0].transform.position))
            {
                Hint("Pick up dill");
                if (pressed)
                {
                    carriedDill = dills[0];
                    dills.RemoveAt(0);
                    carriedDill.OnPickedUp();
                    carriedDill.transform.SetParent(null, worldPositionStays: true);
                    carrying = Carrying.Dill;
                    LineUpDill();
                }
                return true;
            }
            return false;
        }

        void Grab(Carrying kind, List<Transform> pieces)
        {
            foreach (var piece in pieces)
            {
                piece.SetParent(null, worldPositionStays: true);
                carried.Add(piece);
            }
            if (carried.Count > 0)
            {
                carrying = kind;
            }
        }

        // Over a jar, drop what you're holding in; over its pile, pick up more.
        void WhileCarrying(bool pressed)
        {
            foreach (var onTowel in JarsOnTowel())
            {
                if (!IsOpen(onTowel) || !CursorNear(onTowel.Mouth))
                {
                    continue;
                }
                bool noRoom = (carrying == Carrying.Slices && onTowel.RoomForSlices == 0)
                    || (carrying == Carrying.Cloves && onTowel.RoomForCloves == 0)
                    || (carrying == Carrying.Wedges && onTowel.RoomForWedges == 0);
                if (noRoom)
                {
                    UseHint = "This jar is full";
                    return;
                }
                if (carrying == Carrying.Dill && onTowel.HasDill)
                {
                    UseHint = "This jar has dill";
                    return;
                }
                Hint("Drop in");
                if (pressed)
                {
                    DropInto(onTowel);
                }
                return;
            }

            if (carrying == Carrying.Slices && carried.Count < maxSlicesCarried && board.SliceCount > 0 && CursorNear(board.PilePosition))
            {
                Hint("Pick up more");
                if (pressed)
                {
                    Grab(Carrying.Slices, board.TakeSlices(Mathf.Min(handful, maxSlicesCarried - carried.Count)));
                }
            }
            else if (carrying == Carrying.Wedges && carried.Count < maxWedgesCarried && board.WedgeCount > 0 && CursorNear(board.WedgePilePosition))
            {
                Hint("Pick up more");
                if (pressed)
                {
                    Grab(Carrying.Wedges, board.TakeWedges(Mathf.Min(wedgesPerGrab, maxWedgesCarried - carried.Count)));
                }
            }
            else if (carrying == Carrying.Cloves && carried.Count < maxClovesCarried && board.CloveCount > 0 && CursorNear(board.ClovePilePosition))
            {
                Hint("Pick up more");
                if (pressed)
                {
                    Grab(Carrying.Cloves, board.TakeCloves(Mathf.Min(clovesPerGrab, maxClovesCarried - carried.Count)));
                }
            }
        }

        void DropInto(JarContents target)
        {
            jar = target;
            if (carrying == Carrying.Dill)
            {
                target.AddDill(carriedDill);
                carriedDill = null;
                carrying = Carrying.Nothing;
                return;
            }
            if (carrying == Carrying.Peppercorns)
            {
                target.AddPeppercorns(new List<Transform>(carried));
                carried.Clear();
                carrying = Carrying.Nothing;
                return;
            }
            int room = carrying == Carrying.Slices ? target.RoomForSlices : carrying == Carrying.Wedges ? target.RoomForWedges : target.RoomForCloves;
            int count = Mathf.Min(carried.Count, room);
            for (int i = 0; i < count; i++)
            {
                Transform piece = carried[0];
                carried.RemoveAt(0);
                if (carrying == Carrying.Slices)
                {
                    target.AddSlice(piece, i * 0.06f);
                }
                else if (carrying == Carrying.Wedges)
                {
                    target.AddWedge(piece, i * 0.1f);
                }
                else
                {
                    target.AddClove(piece, i * 0.08f);
                }
            }
            if (carried.Count == 0)
            {
                carrying = Carrying.Nothing;
            }
        }

        // Stepping back with something in hand puts it back where it came from.
        void PutBackCarried()
        {
            if (carrying == Carrying.Slices)
            {
                board.ReturnSlices(new List<Transform>(carried));
            }
            else if (carrying == Carrying.Cloves)
            {
                board.ReturnCloves(new List<Transform>(carried));
            }
            else if (carrying == Carrying.Wedges)
            {
                board.ReturnWedges(new List<Transform>(carried));
            }
            else if (carrying == Carrying.Dill && carriedDill != null)
            {
                AddDillHead(carriedDill, 0.3f);
            }
            else if (carrying == Carrying.Peppercorns)
            {
                // Back into the tin.
                foreach (var corn in carried) Destroy(corn.gameObject);
            }
            carried.Clear();
            carriedDill = null;
            carrying = Carrying.Nothing;
        }

        // What's in hand floats under the cursor, a little above the jars.
        void MoveCarried()
        {
            if (carrying == Carrying.Nothing || View == null)
            {
                return;
            }
            var plane = new Plane(Vector3.up, jarSpots[0].position + Vector3.up * carryHeight);
            Ray ray = CursorRay();
            if (!plane.Raycast(ray, out float distance))
            {
                return;
            }
            Vector3 hand = ray.GetPoint(distance);
            float t = 1f - Mathf.Exp(-carrySharpness * Time.deltaTime);
            if (carrying == Carrying.Dill)
            {
                Transform dill = carriedDill.transform;
                dill.SetPositionAndRotation(Vector3.Lerp(dill.position, hand + Vector3.down * 0.06f, t),
                    Quaternion.Slerp(dill.rotation, Quaternion.Euler(0f, 0f, 8f), t));
                return;
            }
            for (int i = 0; i < carried.Count; i++)
            {
                // A loose little stack, each piece a bit higher and askew.
                Vector3 offset = new Vector3((i % 2) * 0.012f - 0.006f, i * 0.007f, ((i / 2) % 2) * 0.01f - 0.005f);
                Quaternion lean = Quaternion.Euler(10f + i * 7f, i * 53f, -8f);
                carried[i].SetPositionAndRotation(Vector3.Lerp(carried[i].position, hand + offset, t),
                    Quaternion.Slerp(carried[i].rotation, lean, t));
            }
        }

        // The lemon juice, lids and the pot go to the jar that needs them.
        bool UseTools(bool pressed)
        {
            if (Lemon is LemonJuice lemon && !lemon.Busy && CursorNear(lemon.transform.position))
            {
                JarContents forLemon = LemonTarget();
                if (forLemon == null)
                {
                    UseHint = "No jar needs lemon juice";
                    return true;
                }
                Hint("Add lemon juice");
                if (pressed)
                {
                    jar = forLemon;
                    StartCoroutine(lemon.SpoonInto(forLemon));
                }
                return true;
            }
            if (LidTarget() == null && CursorNear(lidStack.position) && NextNeeding(j => j.IsFull && j.NeedsLemon) != null)
            {
                UseHint = "Add lemon juice first";
                return true;
            }
            if (LidTarget() is JarContents forLid && CursorNear(lidStack.position))
            {
                Hint("Put on lid");
                if (pressed)
                {
                    jar = forLid;
                    jar.PutOnLid();
                }
                return true;
            }
            if (CookingPot.ReadyPot() is CookingPot ready && BrineTarget() is JarContents forBrine && CursorNear(ready.transform.position))
            {
                Hint("Pour brine");
                if (pressed)
                {
                    jar = forBrine;
                    pot = ready;
                    potRestPosition = pot.transform.position;
                    potRestRotation = pot.transform.rotation;
                    pouring = true;
                    reticle.Hidden = true;
                }
                return true;
            }
            return false;
        }

        void Hint(string hint)
        {
            UseHint = hint;
            reticle.Highlight = true;
        }

        protected override void Update()
        {
            base.Update();
            MoveCarried();
            MovePot();
        }

        // Dill left on the towel is picked up where it was.
        public void AfterLoad()
        {
            dills.Clear();
            foreach (var dill in herbSpot.GetComponentsInChildren<Pickup>())
            {
                dills.Add(dill);
            }
        }

        // Step back from the towel with the lidded jar already in hand.
        void TakeJar(JarContents finished)
        {
            Leave();
            var item = finished.GetComponent<Pickup>();
            item.SetCollidersEnabled(true);
            player.Give(item);
        }

        void AddDillHead(Pickup dill, float glideSeconds)
        {
            dills.Add(dill);
            dill.AttachTo(herbSpot, DillPosition(dills.Count - 1), Quaternion.Euler(0f, 0f, 90f), glideSeconds, collidersOn: false);
        }

        // The heads of dill lie side by side, the next one to use in front.
        void LineUpDill()
        {
            for (int i = 0; i < dills.Count; i++)
            {
                dills[i].AttachTo(herbSpot, DillPosition(i), Quaternion.Euler(0f, 0f, 90f), 0.2f, collidersOn: false);
            }
        }

        Vector3 DillPosition(int index) => new Vector3(0f, 0f, -index * dillSpacing);

        IEnumerable<JarContents> JarsOnTowel()
        {
            foreach (var spot in jarSpots)
            {
                var onSpot = spot.GetComponentInChildren<JarContents>();
                if (onSpot != null)
                {
                    yield return onSpot;
                }
            }
        }

        JarContents FirstOpenJar()
        {
            foreach (var onTowel in JarsOnTowel())
            {
                if (IsOpen(onTowel))
                {
                    return onTowel;
                }
            }
            return null;
        }

        Transform FreeSpot()
        {
            foreach (var spot in jarSpots)
            {
                if (spot.GetComponentInChildren<JarContents>() == null)
                {
                    return spot;
                }
            }
            return null;
        }

        static bool IsOpen(JarContents j) => j != null && !j.HasLid;

        bool OnTowel(JarContents j) => j != null && j.transform.parent != null && System.Array.IndexOf(jarSpots, j.transform.parent) >= 0;

        // The jar being worked on if this step still applies to it, otherwise
        // the next jar on the towel it applies to.
        JarContents NextNeeding(System.Func<JarContents, bool> needs)
        {
            if (IsOpen(jar) && needs(jar))
            {
                return jar;
            }
            foreach (var onTowel in JarsOnTowel())
            {
                if (IsOpen(onTowel) && needs(onTowel))
                {
                    return onTowel;
                }
            }
            return null;
        }

        JarContents LidTarget() => pouring ? null : NextNeeding(j => j.ReadyForLid(recipe));

        // Lemon juice goes to the next jar of tomatoes without it.
        JarContents LemonTarget() => NextNeeding(j => j.NeedsLemon);

        // Brine goes to the next jar short of the line; once every jar is up
        // to it, the jar you're on can still be topped up. Tomatoes take none.
        JarContents BrineTarget() => NextNeeding(j => j.TakesBrine && j.BrineLevel < recipe.brineEnough)
            ?? (IsOpen(jar) && jar.TakesBrine && jar.BrineLevel < 1f ? jar : null);

        void Pour()
        {
            if (!UseAction.IsPressed() || !pot.IsReady || jar == null)
            {
                StopPouring();
                return;
            }
            bool tipped = tilt >= pourTilt * 0.8f;
            pot.SetPouring(tipped);
            if (tipped)
            {
                // The pot only loses what the jar actually takes.
                float before = jar.BrineLevel;
                jar.AddBrine(pourRate * Time.deltaTime);
                pot.Use((jar.BrineLevel - before) * recipe.potPerJar);
            }
        }

        void StopPouring()
        {
            if (!pouring)
            {
                return;
            }
            pouring = false;
            if (pot != null)
            {
                pot.SetPouring(false);
            }
            reticle.Hidden = false;
            LogQuality();
        }

        // The pot lifts off where it rests, swings over the jar and tips; let
        // go and it goes back.
        void MovePot()
        {
            if (pot == null)
            {
                return;
            }
            bool pourPose = pouring && IsWorking && jar != null;
            tilt = Mathf.MoveTowards(tilt, pourPose ? pourTilt : 0f, pourTilt / 0.35f * Time.deltaTime);
            Vector3 target = pourPose ? jar.Mouth : potRestPosition;
            Pouring.Follow(pot.transform, pot.Lip, potRestPosition, potRestRotation, target, tilt, pourTilt, pourHeight, potFollowSharpness);
            if (!pouring && tilt <= 0f && (pot.transform.position - potRestPosition).sqrMagnitude < 0.000001f)
            {
                pot.transform.SetPositionAndRotation(potRestPosition, potRestRotation);
                pot = null;
            }
        }

        void LogQuality()
        {
            // Not shown to the player yet; recorded for tuning jar quality later.
            if (jar == null || logged.Contains(jar) || jar.BrineLevel < recipe.brineEnough)
            {
                return;
            }
            logged.Add(jar);
            RecipeData packed = jar.Recipe != null ? jar.Recipe : recipe;
            float headspaceError = jar.BrineLevel - packed.brineLine;
            Debug.Log($"{packed.displayName}: {jar.SliceCount} slices, {jar.CloveCount} cloves (recipe {packed.garlicPerJar}), {jar.PeppercornCount} peppercorns, dill {(jar.HasDill ? "yes" : "no")}, brine {jar.BrineLevel:P0} (line {packed.brineLine:P0}, off by {headspaceError:+0%;-0%})");
        }

        bool CursorNear(Vector3 worldPoint)
        {
            Vector3 screen = View.WorldToViewportPoint(worldPoint);
            float aspect = (float)Screen.width / Screen.height;
            Vector2 offset = (Vector2)screen - CursorPoint;
            return screen.z > 0f && new Vector2(offset.x * aspect, offset.y).magnitude < grabRadius;
        }

        static bool IsDill(Pickup item) => item.DisplayName == "Dill";
    }
}
