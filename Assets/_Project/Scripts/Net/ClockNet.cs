using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Saving;
using Unity.Netcode;
using UnityEngine;

namespace GrandmasHouse.Net
{
    // The host keeps the time for everyone, and decides when the night
    // passes: only once every player is in bed. Then time hurries (or, when
    // the clock stops at night, morning comes), and the game is saved when
    // the first sleeper gets up.
    [RequireComponent(typeof(DayClock))]
    public class ClockNet : NetworkBehaviour
    {
        [SerializeField] SurvivalSettings survival;
        [Tooltip("In-game hours the host's clock may drift before guests are corrected.")]
        [SerializeField] float sendEveryHours = 0.02f;

        readonly NetworkVariable<int> day = new NetworkVariable<int>(1);
        readonly NetworkVariable<float> hour = new NetworkVariable<float>(7f);
        readonly NetworkVariable<float> fastForward = new NetworkVariable<float>(0f);

        DayClock clock;
        bool everyoneWasAsleep;
        int nightOf = -1;

        // Everyone in the game is asleep right now.
        public bool EveryoneAsleep { get; private set; }

        void Awake()
        {
            clock = GetComponent<DayClock>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                clock.Following = true;
                day.OnValueChanged += (_, __) => Follow();
                hour.OnValueChanged += (_, __) => Follow();
                fastForward.OnValueChanged += (_, __) => Follow();
                Follow();
            }
        }

        public override void OnNetworkDespawn() => clock.Following = false;

        void Follow() => clock.Follow(day.Value, hour.Value, fastForward.Value);

        void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }
            Coordinate();
            if (day.Value != clock.Day || Mathf.Abs(hour.Value - clock.Hour) >= sendEveryHours || !Mathf.Approximately(fastForward.Value, clock.FastForward))
            {
                day.Value = clock.Day;
                hour.Value = clock.Hour;
                fastForward.Value = clock.FastForward;
            }
        }

        // Whether everyone's in bed, and what that does to the night.
        void Coordinate()
        {
            int players = 0, sleeping = 0;
            foreach (var player in PlayerNet.All)
            {
                if (!player.IsSpawned || player.IsDead) continue;
                players++;
                if (player.IsAsleep) sleeping++;
            }
            EveryoneAsleep = players > 0 && sleeping == players;

            if (clock.Mode.clockStopsAtNight)
            {
                // The gentle clock: when everyone's in bed, morning comes.
                if (EveryoneAsleep && nightOf != clock.Day)
                {
                    nightOf = clock.Day + 1;
                    clock.SleepUntilMorning();
                    Save();
                }
                return;
            }

            clock.FastForward = EveryoneAsleep && survival != null ? survival.sleepHoursPerSecond : 0f;
            if (everyoneWasAsleep && !EveryoneAsleep)
            {
                Save();
            }
            everyoneWasAsleep = EveryoneAsleep;
        }

        static void Save()
        {
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.Save();
            }
        }
    }
}
