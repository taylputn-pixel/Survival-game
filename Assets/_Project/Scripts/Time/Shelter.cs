using System.Collections.Generic;
using GrandmasHouse.Data;
using GrandmasHouse.Kitchen;
using UnityEngine;

namespace GrandmasHouse.DayCycle
{
    // Somewhere out of the weather: the house (warmer than outside, warmer
    // still with the stove going), or the root cellar (the earth keeps it
    // about the same all year).
    public class Shelter : MonoBehaviour
    {
        [Tooltip("The inside, as boxes.")]
        [SerializeField] BoxCollider[] rooms;
        [Tooltip("Holds the house's share of warmth over outside (Weather settings).")]
        [SerializeField] bool isHouse = true;
        [Tooltip("Not a house: it stays at this temperature whatever the weather.")]
        [SerializeField] float steadyTemperature = 45f;
        [SerializeField] WoodStove stove;

        public static readonly List<Shelter> All = new List<Shelter>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool Contains(Vector3 position)
        {
            foreach (var room in rooms) if (room != null && room.bounds.Contains(position)) return true;
            return false;
        }

        public float Temperature(float outside, WeatherSettings settings)
        {
            if (!isHouse) return steadyTemperature;
            return outside + settings.houseHolds + (stove != null ? settings.stoveHeat * stove.Heat : 0f);
        }
    }
}
