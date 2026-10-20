using UnityEngine;

namespace GrandmasHouse.Data
{
    // The rules for one way of playing. The game ships with Cozy only; this
    // keeps the rules in data so a harder mode would just be another asset.
    // Rules marked "not used yet" have no system reading them so far.
    [CreateAssetMenu(fileName = "ModeProfile", menuName = "Grandma's House/Mode Profile")]
    public class ModeProfile : ScriptableObject
    {
        public string displayName = "Cozy";

        [Header("Time")]
        [Tooltip("The clock stops while you work at a station, so tasks never cost daylight.")]
        public bool pauseClockAtStations = true;
        [Tooltip("The clock stops at nightfall and the next day starts when you sleep. Off: it runs all night and a new day starts at midnight.")]
        public bool clockStopsAtNight = true;

        [Header("Hardship (Cozy keeps all of these off)")]
        [Tooltip("You get hungry and tired, and it slows you down.")]
        public bool hungerHasPenalties;
        [Tooltip("Not used yet. A cold house in winter has downsides.")]
        public bool coldHasPenalties;
        [Tooltip("Fresh food and open jars spoil over time.")]
        public bool foodSpoils;
        [Tooltip("Not used yet. Neglected crops can die.")]
        public bool cropsCanDie;
        [Tooltip("Not used yet. Sloppy jars can fail to seal and spoil later.")]
        public bool jarsCanFailToSeal;
        [Tooltip("Starving for too long ends the game.")]
        public bool gameOver;
    }
}
