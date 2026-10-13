using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.Kitchen;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Stations
{
    // The big water-bath canner on the back burner. Set lidded jars in its
    // rack, step up and put the canner lid on, and it boils for the recipe's
    // time (you can walk away; sleeping finishes it too). Then take the lid
    // off and lift each jar out onto the cooling towel, where it pings as it
    // seals.
    public class CannerStation : Station, ISaveable, IAfterLoad
    {
        [SerializeField] RecipeData recipe;
        [Tooltip("Spots in the rack, one jar each.")]
        [SerializeField] Transform[] rackSlots;
        [Tooltip("Spots on the cooling towel.")]
        [SerializeField] Transform[] coolingSpots;
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

        bool HasJars => rack.Count > 0;

        void OnEnable() => clock.NewDay += FinishOvernight;
        void OnDisable() => clock.NewDay -= FinishOvernight;

        [System.Serializable]
        class State
        {
            public bool lidOn;
            public bool boiling;
            public bool done;
            public float boilLeft;
        }

        public string SaveKey => "canner:" + name;
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
            if (boiling)
            {
                steam.Play();
            }
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
            canLid.SetPositionAndRotation(lidOffPose.position, lidOffPose.rotation);
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public override bool CanInteract(PlayerHands hands)
        {
            if (hands.IsEmpty)
            {
                return HasJars;
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
            if (!boiling)
            {
                yield return canLid.position;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (boiling)
            {
                boilLeft -= Time.deltaTime;
                if (boilLeft <= 0f)
                {
                    FinishBoiling();
                }
            }
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
                        reticle.Highlight = FreeCoolingSpot() != null;
                        UseHint = reticle.Highlight == true ? "Lift out" : null;
                        if (pressed && FreeCoolingSpot() != null)
                        {
                            LiftOut(pair.Key);
                        }
                        return;
                    }
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

            if (putOn)
            {
                boiling = true;
                boilLeft = recipe.processMinutes * secondsPerRecipeMinute;
                steam.Play();
            }
        }

        void FinishBoiling()
        {
            boiling = false;
            done = true;
            steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // Left to boil overnight, the jars are simply done by morning.
        void FinishOvernight(int day)
        {
            if (boiling)
            {
                FinishBoiling();
            }
        }

        void LiftOut(Transform slot)
        {
            Pickup jar = rack[slot];
            rack.Remove(slot);
            Transform spot = FreeCoolingSpot();
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

        Transform FreeCoolingSpot()
        {
            foreach (var spot in coolingSpots)
            {
                if (spot.childCount == 0)
                {
                    return spot;
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
