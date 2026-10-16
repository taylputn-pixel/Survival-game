using System.Collections;
using GrandmasHouse.Carrying;
using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // Emptying something you carry over an opening (the sink drain, the
    // trash bin): it leaves your hands, goes over the opening, tips away
    // from you while it empties, and comes back upright.
    public static class TipOut
    {
        public static IEnumerator Over(PlayerHands hands, Transform item, Vector3 opening, float holdHeight, float tipAngle, float tipSeconds, float pourSeconds, System.Action<bool> tipped)
        {
            Vector3 startPosition = item.position;
            Quaternion startRotation = item.rotation;
            Vector3 away = Vector3.ProjectOnPlane(opening - hands.ViewPoint.position, Vector3.up).normalized;
            Vector3 axis = Vector3.Cross(Vector3.up, away);
            Quaternion upright = Quaternion.LookRotation(away, Vector3.up);
            Quaternion tippedOver = Quaternion.AngleAxis(tipAngle, axis) * upright;
            Vector3 over = opening + Vector3.up * holdHeight - away * 0.05f;

            yield return Move(item, startPosition, startRotation, over, upright, tipSeconds);
            yield return Move(item, over, upright, over, tippedOver, tipSeconds);
            tipped(true);
            yield return new WaitForSeconds(pourSeconds);
            tipped(false);
            yield return Move(item, over, tippedOver, over, upright, tipSeconds);
        }

        public static IEnumerator Move(Transform t, Vector3 fromPosition, Quaternion fromRotation, Vector3 toPosition, Quaternion toRotation, float seconds)
        {
            for (float time = 0f; time < seconds; time += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, time / seconds);
                t.SetPositionAndRotation(Vector3.Lerp(fromPosition, toPosition, k), Quaternion.Slerp(fromRotation, toRotation, k));
                yield return null;
            }
            t.SetPositionAndRotation(toPosition, toRotation);
        }
    }
}
