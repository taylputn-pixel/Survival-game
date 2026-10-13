using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // A dish towel on the counter between the cutting board and the stove.
    // Set an empty jar on it (and a head of dill beside it), step up, then:
    // drop in the dill, scoop slices from the board into the jar a handful
    // at a time, hold Interact on a pot of boiling brine on the stove to pour
    // up to the line, and put a lid on from the stack. Then it's ready for
    // the canner.
    public class JarPackingStation : Station, IAfterLoad
    {
        [SerializeField] RecipeData recipe;
        [Tooltip("Slices are taken from this board's pile.")]
        [SerializeField] CuttingBoardStation board;
        [Tooltip("Where the jar stands on the towel.")]
        [SerializeField] Transform jarSpot;
        [Tooltip("Where a head of dill waits beside the jar.")]
        [SerializeField] Transform herbSpot;
        [Tooltip("The stack of lids and rings on the towel.")]
        [SerializeField] Transform lidStack;

        [Header("Feel")]
        [Tooltip("How close the cursor must be to something to use it, in screen heights.")]
        [SerializeField] float grabRadius = 0.08f;
        [SerializeField] int handful = 4;
        [Tooltip("How fast the brine rises while pouring (share of the jar per second).")]
        [SerializeField] float pourRate = 0.32f;
        [SerializeField] float pourTilt = 70f;
        [Tooltip("How far above the jar's mouth the lip is held while pouring, in meters.")]
        [SerializeField] float pourHeight = 0.07f;
        [SerializeField] float potFollowSharpness = 10f;

        Pickup jarItem;
        JarContents jar;
        Pickup dill;
        int enteredFrame;
        bool pouring;
        float tilt;
        bool loggedQuality;
        bool takeable;
        // The pot being poured from, and where it rested on the stove.
        CookingPotStation pot;
        Vector3 potRestPosition;
        Quaternion potRestRotation;

        public override bool CanInteract(PlayerHands hands)
        {
            if (hands.IsEmpty)
            {
                return jar != null && !jar.HasLid;
            }
            var heldJar = hands.Held.GetComponent<JarContents>();
            if (heldJar != null)
            {
                return jar == null && heldJar.BrineLevel <= 0f;
            }
            return dill == null && IsDill(hands.Held);
        }

        public override void Interact(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                Pickup item = hands.TakeHeld();
                if (item.GetComponent<JarContents>() != null)
                {
                    SetJar(item);
                }
                else
                {
                    dill = item;
                    item.AttachTo(herbSpot, Vector3.zero, Quaternion.Euler(0f, 0f, 90f), settings.placeGlideSeconds, collidersOn: false);
                }
                return;
            }
            enteredFrame = Time.frameCount;
            Enter(hands);
        }

        public override string PromptVerb(PlayerHands hands)
        {
            if (hands.IsEmpty)
            {
                return "Pack jar";
            }
            return hands.Held.GetComponent<JarContents>() != null ? "Set jar down" : "Set dill down";
        }

        protected override IEnumerable<Vector3> SnapTargets()
        {
            if (pouring)
            {
                yield break;
            }
            if (CanTakeSlices())
            {
                yield return board.PilePosition;
            }
            if (dill != null && !jar.HasDill)
            {
                yield return dill.transform.position;
            }
            if (CanPutOnLid())
            {
                yield return lidStack.position;
            }
            CookingPotStation ready = CookingPotStation.ReadyPot();
            if (!jar.HasLid && ready != null)
            {
                yield return ready.transform.position;
            }
        }

        protected override void OnLeave() => StopPouring();

        protected override void WhileWorking()
        {
            if (pouring)
            {
                UseHint = "Hold to pour";
                Pour();
                return;
            }
            UseHint = null;

            bool pressed = Time.frameCount != enteredFrame && UseAction.WasPressedThisFrame();
            if (CanTakeSlices() && CursorNear(board.PilePosition))
            {
                reticle.Highlight = true;
                UseHint = "Scoop chips";
                if (pressed)
                {
                    ScoopSlices();
                }
            }
            else if (dill != null && !jar.HasDill && !jar.HasLid && CursorNear(dill.transform.position))
            {
                reticle.Highlight = true;
                UseHint = "Add dill";
                if (pressed)
                {
                    jar.AddDill(dill);
                    dill = null;
                }
            }
            else if (CanPutOnLid() && CursorNear(lidStack.position))
            {
                reticle.Highlight = true;
                UseHint = "Put on lid";
                if (pressed)
                {
                    jar.PutOnLid();
                }
            }
            else if (!jar.HasLid && CookingPotStation.ReadyPot() is CookingPotStation ready && CursorNear(ready.transform.position))
            {
                reticle.Highlight = true;
                UseHint = "Pour brine";
                if (pressed)
                {
                    pot = ready;
                    potRestPosition = pot.transform.position;
                    potRestRotation = pot.transform.rotation;
                    pouring = true;
                    reticle.Hidden = true;
                }
            }
            else
            {
                reticle.Highlight = false;
            }
        }

        protected override void Update()
        {
            base.Update();
            MovePot();

            // Once the lid is on and you've stepped back, the jar can be
            // picked up and carried to the canner.
            if (jar != null && jar.HasLid && !IsWorking && !takeable)
            {
                takeable = true;
                jarItem.SetCollidersEnabled(true);
            }
        }

        // A jar or dill left on the towel is picked up where it was.
        public void AfterLoad()
        {
            var jarOnTowel = jarSpot.GetComponentInChildren<JarContents>();
            if (jarOnTowel != null)
            {
                SetJar(jarOnTowel.GetComponent<Pickup>());
            }
            var dillWaiting = herbSpot.GetComponentInChildren<Pickup>();
            if (dillWaiting != null)
            {
                dill = dillWaiting;
            }
        }

        void SetJar(Pickup item)
        {
            jarItem = item;
            jar = item.GetComponent<JarContents>();
            loggedQuality = false;
            takeable = false;
            item.AttachTo(jarSpot, Vector3.zero, Quaternion.identity, settings.placeGlideSeconds, collidersOn: false);
            // Picking the finished jar up takes it off the towel.
            item.HeldChanged += OnJarHeld;
        }

        void OnJarHeld(bool held)
        {
            if (!held || jarItem == null)
            {
                return;
            }
            jarItem.HeldChanged -= OnJarHeld;
            jarItem.transform.SetParent(null, worldPositionStays: true);
            jarItem = null;
            jar = null;
        }

        bool CanTakeSlices() => board.SliceCount > 0 && !jar.IsFull && !jar.HasLid;

        bool CanPutOnLid() => !jar.HasLid && !pouring && jar.BrineLevel >= recipe.brineEnough;

        void ScoopSlices()
        {
            int room = jar.Capacity - jar.SliceCount;
            List<Transform> slices = board.TakeSlices(Mathf.Min(handful, room));
            for (int i = 0; i < slices.Count; i++)
            {
                jar.AddSlice(slices[i], i * 0.06f);
            }
        }

        void Pour()
        {
            if (!UseAction.IsPressed() || !pot.IsReady)
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

        // The pot lifts off its burner, swings over the jar and tips; let go
        // and it goes back.
        void MovePot()
        {
            if (pot == null)
            {
                return;
            }
            bool pourPose = pouring && IsWorking && jar != null;
            tilt = Mathf.MoveTowards(tilt, pourPose ? pourTilt : 0f, pourTilt / 0.35f * Time.deltaTime);
            Vector3 target = jar != null ? jar.Mouth : potRestPosition;
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
            if (loggedQuality || jar == null || jar.BrineLevel < recipe.brineEnough)
            {
                return;
            }
            loggedQuality = true;
            float headspaceError = jar.BrineLevel - recipe.brineLine;
            Debug.Log($"Packed {jar.SliceCount}/{recipe.slicesPerJar} slices, dill {(jar.HasDill ? "yes" : "no")}, brine {jar.BrineLevel:P0} (line {recipe.brineLine:P0}, off by {headspaceError:+0%;-0%})");
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
