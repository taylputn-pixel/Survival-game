using UnityEngine;

namespace GrandmasHouse.Data
{
    // Tunable numbers for the first-person controller. Kept in an asset so they
    // can be tweaked in the Inspector without touching code.
    [CreateAssetMenu(fileName = "PlayerSettings", menuName = "Grandma's House/Player Settings")]
    public class PlayerSettings : ScriptableObject
    {
        [Header("Movement (meters per second)")]
        [Tooltip("Normal pace. Unhurried, like walking around a house.")]
        public float walkSpeed = 2.6f;
        [Tooltip("Brisk pace for crossing the yard. Held, never required.")]
        public float briskSpeed = 4.2f;
        [Tooltip("How quickly you reach full speed and come to a stop.")]
        public float acceleration = 12f;
        public float gravity = -20f;

        [Header("Looking")]
        [Tooltip("Degrees turned per pixel of mouse movement.")]
        public float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full right-stick deflection.")]
        public float gamepadLookSpeed = 150f;
        [Tooltip("Above 1, small stick movements turn slowly for fine aiming.")]
        [Range(1f, 3f)] public float gamepadLookCurve = 2f;
        public bool invertY;
        [Range(30f, 89f)] public float maxPitch = 80f;

        [Header("Camera")]
        [Tooltip("Eye height above the feet, in meters. Lower makes the world feel bigger.")]
        [Range(1.2f, 1.8f)] public float eyeHeight = 1.5f;
        [Tooltip("Vertical field of view. Wide views make rooms feel small and far away.")]
        [Range(50f, 80f)] public float fieldOfView = 60f;

        [Header("Hands")]
        [Tooltip("How far away you can reach to pick up or set down, in meters.")]
        public float reach = 2.2f;
        [Tooltip("Where a held item sits, relative to the camera (right, up, forward).")]
        public Vector3 holdOffset = new Vector3(0.22f, -0.2f, 0.5f);
        [Tooltip("How snugly a held item follows your view. Higher is tighter.")]
        public float holdFollowSharpness = 16f;
        [Tooltip("Surfaces steeper than this can't hold an item. 1 = flat only.")]
        [Range(0.3f, 1f)] public float minSurfaceUpness = 0.6f;
        [Tooltip("How long a set-down item takes to glide from your hand to its spot.")]
        [Range(0.05f, 0.5f)] public float placeGlideSeconds = 0.2f;

        [Header("Stations")]
        [Tooltip("How long the camera takes to glide into and out of a station's working view.")]
        [Range(0.15f, 1.2f)] public float stationCameraSeconds = 0.45f;
        [Tooltip("Cursor movement per pixel of mouse movement, as a fraction of the screen height.")]
        public float stationMouseSpeed = 0.0011f;
        [Tooltip("Cursor speed at full stick, in screen heights per second.")]
        public float stationStickSpeed = 0.9f;
        [Tooltip("Gamepad only: how close (in screen heights) the cursor must be before it drifts onto a tool.")]
        public float stationSnapRadius = 0.06f;
        public float stationSnapStrength = 10f;

        [Header("Feel")]
        [Tooltip("Gentle head bob while walking. 0 turns it off.")]
        [Range(0f, 0.08f)] public float headBobAmount = 0.025f;
        public float headBobFrequency = 1.8f;
    }
}
