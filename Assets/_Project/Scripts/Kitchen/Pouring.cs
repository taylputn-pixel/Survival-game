using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // The shared motion for pouring by hand: lift a vessel from where it
    // rests, swing its lip over the target, and tip it. Let go and it goes
    // back. Used for the cooking pot, the vinegar jug, and the water pitcher.
    public static class Pouring
    {
        // Moves `vessel` a step toward its pouring pose for this much tilt (0 = resting).
        public static void Follow(Transform vessel, Transform lip, Vector3 restPosition, Quaternion restRotation,
            Vector3 target, float tilt, float maxTilt, float height, float sharpness)
        {
            Vector3 position = restPosition;
            Quaternion rotation = restRotation;
            if (tilt > 0f)
            {
                // Turn so the lip faces the target, then tip forward about the lip's side.
                Vector3 toTarget = Vector3.ProjectOnPlane(target - restPosition, Vector3.up);
                Vector3 lipLocal = Quaternion.Inverse(vessel.rotation) * (lip.position - vessel.position);
                Vector3 lipFlat = new Vector3(lipLocal.x, 0f, lipLocal.z);
                float lipYaw = Mathf.Atan2(lipFlat.x, lipFlat.z) * Mathf.Rad2Deg;
                float yaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg - lipYaw;
                Vector3 tipAxis = Quaternion.Euler(0f, lipYaw, 0f) * Vector3.right;
                rotation = Quaternion.Euler(0f, yaw, 0f) * Quaternion.AngleAxis(tilt, tipAxis);
                Vector3 lipTarget = target + Vector3.up * height;
                position = Vector3.Lerp(restPosition, lipTarget - rotation * lipLocal, Mathf.Clamp01(tilt / (maxTilt * 0.5f)));
            }
            float follow = 1f - Mathf.Exp(-sharpness * Time.deltaTime);
            vessel.SetPositionAndRotation(
                Vector3.Lerp(vessel.position, position, follow),
                Quaternion.Slerp(vessel.rotation, rotation, follow));
        }
    }
}
