using System.Collections;
using System.Collections.Generic;
using GrandmasHouse.Audio;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Interaction;
using GrandmasHouse.Saving;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // The big water-bath canner. It's kept in the pantry and used right where
    // it sits, with no station: fill it with water at the sink, set it on a
    // burner, put lidded jars in its rack, put the lid on and turn the burner
    // on. It only boils with enough water to cover the jars. It boils for the recipe's
    // time (you can walk away; sleeping finishes it too). Then take the lid
    // off and lift each jar out; it pings as it cools and seals.
    [RequireComponent(typeof(Cookware))]
    public class Canner : MonoBehaviour, IInteractable, IPickupGuard, IItemState, IAfterLoad, IFillable, IEmptyable, GrandmasHouse.Interaction.ISharedUse
    {
        [SerializeField] PlayerSettings settings;
        [SerializeField] RecipeData recipe;
        [Tooltip("Spots in the rack, one jar each.")]
        [SerializeField] Transform[] rackSlots;
        [SerializeField] Transform canLid;
        [Tooltip("Where the canner lid sits when it's on.")]
        [SerializeField] Transform lidOnPose;
        [Tooltip("Where the canner lid is set aside when it's off.")]
        [SerializeField] Transform lidOffPose;
        [SerializeField] ParticleSystem steam;
        [Tooltip("The water's surface; it rises as the canner fills.")]
        [SerializeField] Transform waterSurface;
        [Tooltip("Height of the water's surface when empty and when full, in the canner's space.")]
        [SerializeField] Vector2 waterHeights = new Vector2(0.012f, 0.205f);
        [Tooltip("How full it must be to cover the jars and boil (share of full).")]
        [Range(0f, 1f)] [SerializeField] float waterNeeded = 0.85f;
        [Tooltip("Finished overnight when you sleep.")]
        [SerializeField] DayClock clock;

        [Header("Feel")]
        [Tooltip("Real seconds for each minute of processing time in the recipe.")]
        [SerializeField] float secondsPerRecipeMinute = 3f;
        [SerializeField] float lidMoveSeconds = 0.4f;
        [SerializeField] Vector2 pingDelay = new Vector2(1.5f, 5f);

        readonly Dictionary<Transform, Pickup> rack = new Dictionary<Transform, Pickup>();
        readonly List<Transform> lifted = new List<Transform>();
        bool lidOn;
        bool boiling;
        bool done;
        float boilLeft;
        bool lidMoving;
        Cookware cookware;
        SoundLoop boilSound;

        // 0 is empty, 1 is full.
        float water;

        bool HasJars => rack.Count > 0;
        // Boils only with jars in, enough water over them, the lid on, and the burner lit.
        bool Heating => lidOn && !done && HasJars && water >= waterNeeded && cookware.IsHot;
        public bool IsFull => water >= 1f;
        public Vector3 SurfaceWorld => waterSurface.position;
        public bool HasContents => water > 0f;
        public bool LidOn => lidOn;
        // Not enough water yet to cover the jars, so it won't come to a boil.
        public bool NeedsWater => water < waterNeeded;
        public bool CanMoveLid => !lidMoving;

        void Awake()
        {
            cookware = GetComponent<Cookware>();
            if (clock == null) clock = FindAnyObjectByType<DayClock>();
            boilSound = Sfx.Loop(SoundId.Boil, transform);
            ShowWater();
        }

        public void AddWater(float amount)
        {
            water = Mathf.Min(water + amount, 1f);
            ShowWater();
        }

        public void DoneFilling() { }

        // Poured out at the sink.
        public void Empty()
        {
            water = 0f;
            ShowWater();
        }

        public void SetPouring(bool pouring) { }

        void ShowWater()
        {
            waterSurface.gameObject.SetActive(water > 0f);
            Vector3 at = waterSurface.localPosition;
            waterSurface.localPosition = new Vector3(at.x, Mathf.Lerp(waterHeights.x, waterHeights.y, water), at.z);
        }

        void OnEnable()
        {
            if (clock != null) clock.NewDay += FinishOvernight;
        }

        void OnDisable()
        {
            if (clock != null) clock.NewDay -= FinishOvernight;
        }

        // Lift it whenever the rack is empty, even straight off the stove.
        public bool AllowPickup(PlayerHands player) => !HasJars;

        [System.Serializable]
        class State
        {
            public bool lidOn;
            public bool boiling;
            public bool done;
            public float boilLeft;
            public float water;
        }

        public string Capture() => JsonUtility.ToJson(new State { lidOn = lidOn, boiling = boiling, done = done, boilLeft = boilLeft, water = water });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            lidOn = state.lidOn;
            boiling = state.boiling;
            done = state.done;
            boilLeft = state.boilLeft;
            water = state.water;
            ShowWater();
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
            SetJarsReachable();
        }

        void Start()
        {
            Transform pose = lidOn ? lidOnPose : lidOffPose;
            canLid.SetPositionAndRotation(pose.position, pose.rotation);
            steam.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // Holding a lidded jar: look at the canner on the stove to put it in.
        public bool CanInteract(PlayerHands hands)
        {
            if (!cookware.OnStove || hands.IsEmpty)
            {
                return false;
            }
            var jar = hands.Held.GetComponent<JarContents>();
            return jar != null && jar.HasLid && !jar.IsProcessed && !lidOn && !done && FreeSlot() != null;
        }

        public string PromptVerb(PlayerHands hands) => "Put in canner";

        public void Interact(PlayerHands hands)
        {
            Transform slot = FreeSlot();
            Pickup jar = hands.TakeHeld();
            rack[slot] = jar;
            jar.AttachTo(slot, Vector3.zero, Quaternion.identity, settings.placeGlideSeconds * 1.5f, collidersOn: true);
        }

        public void ToggleLid()
        {
            if (!lidMoving)
            {
                StartCoroutine(MoveLid(!lidOn));
            }
        }

        void Update()
        {
            NoticeLiftedJars();

            boiling = Heating;
            if (boiling && Net.ItemNet.Runs(this))
            {
                boilLeft -= Time.deltaTime;
                if (boilLeft <= 0f)
                {
                    FinishBoiling();
                }
            }
            boilSound.Set(boiling);
            if (boiling && !steam.isEmitting) steam.Play();
            else if (!boiling && steam.isEmitting) steam.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        // A jar picked up out of the rack has left it; a processed one starts cooling.
        void NoticeLiftedJars()
        {
            lifted.Clear();
            foreach (var pair in rack)
            {
                if (pair.Value == null || pair.Value.transform.parent != pair.Key)
                {
                    lifted.Add(pair.Key);
                }
            }
            foreach (var slot in lifted)
            {
                Pickup jar = rack[slot];
                rack.Remove(slot);
                if (done && jar != null && Net.ItemNet.Runs(this))
                {
                    jar.GetComponent<JarContents>().CoolAndSeal(Random.Range(pingDelay.x, pingDelay.y));
                }
            }
            if (lifted.Count > 0 && !HasJars)
            {
                done = false;
            }
        }

        IEnumerator MoveLid(bool putOn)
        {
            lidMoving = true;
            if (putOn)
            {
                lidOn = true;
                SetJarsReachable();
            }
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
            SetJarsReachable();
            Sfx.Play(SoundId.PotLid, canLid.position);
            lidMoving = false;

            if (putOn && !done)
            {
                boilLeft = ProcessMinutes() * secondsPerRecipeMinute;
            }
        }

        // The longest time any jar in the load needs (tomatoes take far longer
        // than pickles).
        float ProcessMinutes()
        {
            float minutes = 0f;
            foreach (var jar in rack.Values)
            {
                var contents = jar != null ? jar.GetComponent<JarContents>() : null;
                RecipeData packed = contents != null && contents.Recipe != null ? contents.Recipe : recipe;
                minutes = Mathf.Max(minutes, packed.processMinutes);
            }
            return minutes > 0f ? minutes : recipe.processMinutes;
        }

        // Jars can only be reached into while the lid is off.
        void SetJarsReachable()
        {
            foreach (var jar in rack.Values)
            {
                if (jar != null)
                {
                    jar.SetCollidersEnabled(!lidOn);
                }
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
    }
}
