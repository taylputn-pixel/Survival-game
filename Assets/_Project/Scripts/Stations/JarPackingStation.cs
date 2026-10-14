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
    // them, step up, then work one jar at a time: drop in the dill, scoop
    // slices from the board a handful at a time, hold Interact on the pot of
    // boiling brine (on the hot pad or the stove) to pour up to the line, and
    // put a lid on from the stack. Each step goes to the jar you're working
    // on while it still needs it, and otherwise on to the next jar that does,
    // so you can pour brine down the row, then lid them all. Click a jar to
    // work on it. Lidded jars can be picked straight up off the towel.
    public class JarPackingStation : Station, IAfterLoad
    {
        [SerializeField] RecipeData recipe;
        [Tooltip("Slices are taken from this board's pile.")]
        [SerializeField] CuttingBoardStation board;
        [Tooltip("Where jars stand on the towel, in the order they're packed.")]
        [SerializeField] Transform[] jarSpots;
        [Tooltip("Where heads of dill wait beside the jars.")]
        [SerializeField] Transform herbSpot;
        [SerializeField] int maxDill = 4;
        [SerializeField] float dillSpacing = 0.035f;
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

        PlayerHands player;
        // The jar being worked on.
        JarContents jar;
        readonly List<Pickup> dills = new List<Pickup>();
        readonly HashSet<JarContents> logged = new HashSet<JarContents>();
        int enteredFrame;
        bool pouring;
        float tilt;
        // The pot being poured from, and where it rested.
        CookingPotStation pot;
        Vector3 potRestPosition;
        Quaternion potRestRotation;

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
            return dills.Count < maxDill && IsDill(hands.Held);
        }

        public override void Interact(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                Pickup item = hands.TakeHeld();
                if (item.GetComponent<JarContents>() != null)
                {
                    item.AttachTo(FreeSpot(), Vector3.zero, Quaternion.identity, settings.placeGlideSeconds, collidersOn: true);
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
            return hands.Held.GetComponent<JarContents>() != null ? "Set jar down" : "Set dill down";
        }

        protected override IEnumerable<Vector3> SnapTargets()
        {
            if (pouring)
            {
                yield break;
            }
            if (jar != null)
            {
                if (SliceTarget() != null)
                {
                    yield return board.PilePosition;
                }
                if (DillTarget() != null)
                {
                    yield return dills[0].transform.position;
                }
                if (LidTarget() != null)
                {
                    yield return lidStack.position;
                }
                CookingPotStation ready = CookingPotStation.ReadyPot();
                if (ready != null && BrineTarget() != null)
                {
                    yield return ready.transform.position;
                }
            }
            foreach (var other in JarsOnTowel())
            {
                if (other != jar)
                {
                    yield return other.Mouth;
                }
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
            reticle.Highlight = false;

            // A jar that was finished or taken away hands over to the next one.
            if (!IsOpen(jar) || !OnTowel(jar))
            {
                jar = FirstOpenJar();
            }

            bool pressed = Time.frameCount != enteredFrame && UseAction.WasPressedThisFrame();
            if (jar != null && UseTools(pressed))
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

        // The pile, the dill, the lids and the pot each go to the jar that needs them.
        bool UseTools(bool pressed)
        {
            if (SliceTarget() is JarContents forSlices && CursorNear(board.PilePosition))
            {
                Hint("Scoop chips");
                if (pressed)
                {
                    jar = forSlices;
                    ScoopSlices();
                }
                return true;
            }
            if (DillTarget() is JarContents forDill && CursorNear(dills[0].transform.position))
            {
                Hint("Add dill");
                if (pressed)
                {
                    jar = forDill;
                    jar.AddDill(dills[0]);
                    dills.RemoveAt(0);
                    LineUpDill();
                }
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
            if (CookingPotStation.ReadyPot() is CookingPotStation ready && BrineTarget() is JarContents forBrine && CursorNear(ready.transform.position))
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

        JarContents SliceTarget() => board.SliceCount > 0 ? NextNeeding(j => !j.IsFull) : null;

        JarContents DillTarget() => dills.Count > 0 ? NextNeeding(j => !j.HasDill) : null;

        JarContents LidTarget() => pouring ? null : NextNeeding(j => j.BrineLevel >= recipe.brineEnough);

        // Brine goes to the next jar short of the line; once every jar is up
        // to it, the jar you're on can still be topped up.
        JarContents BrineTarget() => NextNeeding(j => j.BrineLevel < recipe.brineEnough) ?? (IsOpen(jar) && jar.BrineLevel < 1f ? jar : null);

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
