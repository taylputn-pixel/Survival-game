using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // The big water-bath canner. It's kept in the pantry; set it on a burner,
    // put lidded jars in its rack, step up, put the canner lid on and turn
    // the burner on. It boils for the recipe's time (you can walk away;
    // sleeping finishes it too). Then take the lid off and lift each jar out
    // onto the cooling towel, where it pings as it seals.
    [RequireComponent(typeof(Cookware))]
    public class CannerStation : Station, IPickupGuard, IItemState, IAfterLoad
    {
        [SerializeField] RecipeData recipe;
        [Tooltip("Spots in the rack, one jar each.")]
        [SerializeField] Transform[] rackSlots;
        [SerializeField] Transform canLid;
        [Tooltip("Where the canner lid sits when it's on.")]
        [SerializeField] Transform lidOnPose;
        [Tooltip("Where the canner lid is set aside when it's off.")]
        [SerializeField] Transform lidOffPose;
        [SerializeField] ParticleSystem steam;

        [Header("Feel")]
        [Tooltip("Real seconds for each minute of processing time in the recipe.")]
        [SerializeField] float secondsPerRecipeMinute = 3f;
        [SerializeField] float grabRadius = 0.08f;
        [SerializeField] float lidMoveSeconds = 0.4f;
        [SerializeField] Vector2 pingDelay = new Vector2(1.5f, 5f);

        readonly Dictionary<Transform, Pickup> rack = new Dictionary<Transform, Pickup>();
        bool lidOn;
        bool boiling;
        bool done;
        float boilLeft;
        bool lidMoving;
        int enteredFrame;
        Cookware cookware;
        CoolingTowel towel;
        PlayerHands hands;

        bool HasJars => rack.Count > 0;
        // Boils only with jars in, the lid on, and the burner lit.
        bool Heating => lidOn && !done && HasJars && cookware.IsHot;

        protected override void Awake()
        {
            base.Awake();
            cookware = GetComponent<Cookware>();
            towel = FindFirstObjectByType<CoolingTowel>();
        }

        void OnEnable()
        {
            if (clock != null) clock.NewDay += FinishOvernight;
        }

        void OnDisable()
        {
            if (clock != null) clock.NewDay -= FinishOvernight;
        }

        // Lift it off the stove from its station once it's empty.
        public bool AllowPickup(PlayerHands player) => !cookware.OnStove;

        [System.Serializable]
        class State
        {
            public bool lidOn;
            public bool boiling;
            public bool done;
            public float boilLeft;
        }

        public string Capture() => JsonUtility.ToJson(new State { lidOn = lidOn, boiling = boiling, done = done, boilLeft = boilLeft });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            lidOn = state.lidOn;
            boiling = state.boiling;
            done = state.done;
            boilLeft = state.boilLeft;
            Transform pose = lidOn ? lidOnPose : lidOffPose;
            canLid.SetPositionAndRotation(pose.position, pose.rotation);
        }

        // Jars left in the rack go back in the rack.
        public void AfterLoad()
        {
            rack.Clear();
            foreach (var slot in rackSlots)
            {
                var jar = slot.GetComponentInChildren<Pickup>();
                if (jar != null)
                {
                    rack[slot] = jar;
                }
            }
        }

        void Start()
        {
            Transform pose = lidOn ? lidOnPose : lidOffPose;
            canLid.SetPositionAndRotation(pose.position, pose.rotation);
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public override bool CanInteract(PlayerHands hands)
        {
            if (!cookware.OnStove)
            {
                return false;
            }
            if (hands.IsEmpty)
            {
                return true;
            }
            var jar = hands.Held.GetComponent<JarContents>();
            return jar != null && jar.HasLid && !jar.IsProcessed && !lidOn && !done && FreeSlot() != null;
        }

        public override void Interact(PlayerHands hands)
        {
            if (!hands.IsEmpty)
            {
                Transform slot = FreeSlot();
                Pickup jar = hands.TakeHeld();
                rack[slot] = jar;
                jar.AttachTo(slot, Vector3.zero, Quaternion.identity, settings.placeGlideSeconds * 1.5f, collidersOn: false);
                return;
            }
            this.hands = hands;
            enteredFrame = Time.frameCount;
            Enter(hands);
        }

        public override string PromptVerb(PlayerHands hands) => hands.IsEmpty ? "Tend canner" : "Put in canner";

        protected override IEnumerable<Vector3> SnapTargets()
        {
            if (!lidOn && done)
            {
                foreach (var jar in rack.Values)
                {
                    yield return jar.transform.position;
                }
            }
            if (!lidMoving)
            {
                yield return canLid.position;
            }
            yield return cookware.Burner.Knob.position;
            yield return cookware.Handle.position;
        }

        protected override void Update()
        {
            base.Update();
            boiling = Heating;
            if (boiling)
            {
                boilLeft -= Time.deltaTime;
                if (boilLeft <= 0f)
                {
                    FinishBoiling();
                }
            }
            if (boiling && !steam.isEmitting) steam.Play();
            else if (!boiling && steam.isEmitting) steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        protected override void WhileWorking()
        {
            bool pressed = Time.frameCount != enteredFrame && UseAction.WasPressedThisFrame() && !lidMoving;
            reticle.Highlight = false;
            UseHint = null;

            // Lid on to start; lid off once it's done.
            bool lidReady = (!lidOn && HasJars && !done) || (lidOn && done);
            if (lidReady && CursorNear(canLid.position))
            {
                reticle.Highlight = true;
                UseHint = lidOn ? "Take lid off" : "Put lid on";
                if (pressed)
                {
                    StartCoroutine(MoveLid(!lidOn));
                }
                return;
            }

            if (!lidOn && done)
            {
                foreach (var pair in rack)
                {
                    if (CursorNear(pair.Value.transform.position))
                    {
                        reticle.Highlight = towel.FreeSpot() != null;
                        UseHint = reticle.Highlight == true ? "Lift out" : null;
                        if (pressed && towel.FreeSpot() != null)
                        {
                            LiftOut(pair.Key);
                        }
                        return;
                    }
                }
            }

            if (CursorNear(cookware.Burner.Knob.position))
            {
                reticle.Highlight = true;
                UseHint = cookware.Burner.On ? "Turn the burner off" : "Turn the burner on";
                if (pressed)
                {
                    cookware.Burner.Toggle();
                }
                return;
            }

            if (!HasJars && !lidOn && CursorNear(cookware.Handle.position))
            {
                reticle.Highlight = true;
                UseHint = "Take off the stove";
                if (pressed)
                {
                    Leave();
                    cookware.LiftOff(hands);
                }
            }
        }

        IEnumerator MoveLid(bool putOn)
        {
            lidMoving = true;
            Transform from = putOn ? lidOffPose : lidOnPose;
            Transform to = putOn ? lidOnPose : lidOffPose;
            for (float time = 0f; time < lidMoveSeconds; time += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, time / lidMoveSeconds);
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.12f;
                canLid.SetPositionAndRotation(Vector3.Lerp(from.position, to.position, t) + arc, Quaternion.Slerp(from.rotation, to.rotation, t));
                yield return null;
            }
            canLid.SetPositionAndRotation(to.position, to.rotation);
            lidOn = putOn;
            lidMoving = false;

            if (putOn && !done)
            {
                boilLeft = recipe.processMinutes * secondsPerRecipeMinute;
            }
        }

        void FinishBoiling()
        {
            boiling = false;
            done = true;
        }

        // Left with the lid on overnight, the jars are simply done by morning.
        void FinishOvernight(int day)
        {
            if (lidOn && HasJars && !done)
            {
                FinishBoiling();
            }
        }

        void LiftOut(Transform slot)
        {
            Pickup jar = rack[slot];
            rack.Remove(slot);
            Transform spot = towel.FreeSpot();
            jar.AttachTo(spot, Vector3.zero, Quaternion.identity, 0.5f, collidersOn: true);
            jar.GetComponent<JarContents>().CoolAndSeal(Random.Range(pingDelay.x, pingDelay.y));
            if (!HasJars)
            {
                done = false;
            }
        }

        Transform FreeSlot()
        {
            foreach (var slot in rackSlots)
            {
                if (!rack.ContainsKey(slot))
                {
                    return slot;
                }
            }
            return null;
        }

        bool CursorNear(Vector3 worldPoint)
        {
            Vector3 screen = View.WorldToViewportPoint(worldPoint);
            float aspect = (float)Screen.width / Screen.height;
            Vector2 offset = (Vector2)screen - CursorPoint;
            return screen.z > 0f && new Vector2(offset.x * aspect, offset.y).magnitude < grabRadius;
        }
    }
}
