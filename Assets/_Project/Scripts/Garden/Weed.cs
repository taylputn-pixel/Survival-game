using System.Collections;
using GrandmasHouse.Audio;
using UnityEngine;

namespace GrandmasHouse.Garden
{
    // A weed in the garden bed. It grows a little each night. Grab it and
    // pull back to work it loose; it comes out roots and all.
    public class Weed : MonoBehaviour
    {
        [Tooltip("The leafy part above the soil.")]
        [SerializeField] Transform top;
        [Tooltip("The root, hidden in the soil until the weed is pulled.")]
        [SerializeField] Transform root;
        [Tooltip("How far the weed rises out of the soil as it comes loose, in meters.")]
        [SerializeField] float pullRise = 0.05f;
        [SerializeField] float tossSeconds = 0.45f;

        public int Age { get; private set; }
        public float Size => Mathf.Lerp(0.9f, 1.8f, Age / (float)Mathf.Max(maxAge, 1));

        int maxAge = 3;
        float pull;
        float shownPull;
        bool held;
        bool pulledOut;

        public void Sprout(int startAge, int oldest)
        {
            maxAge = oldest;
            Age = Mathf.Clamp(startAge, 0, maxAge);
            transform.localScale = Vector3.one * Size;
        }

        public void GrowOvernight()
        {
            Age = Mathf.Min(Age + 1, maxAge);
            transform.localScale = Vector3.one * Size;
        }

        // How loose the weed is, from 0 (in the ground) to 1 (out).
        public void SetPull(float amount, bool holding)
        {
            pull = Mathf.Clamp01(amount);
            held = holding;
        }

        public void PullOut(Vector3 tossTo)
        {
            pulledOut = true;
            Sfx.Play(SoundId.WeedPull, transform.position);
            StartCoroutine(Toss(tossTo));
        }

        void Update()
        {
            if (pulledOut)
            {
                return;
            }
            // Let go and it settles back down; held, it follows your pull with a little give.
            float target = held ? pull : 0f;
            shownPull = Mathf.Lerp(shownPull, target, 1f - Mathf.Exp(-(held ? 20f : 8f) * Time.deltaTime));
            top.localPosition = Vector3.up * shownPull * pullRise;
            top.localScale = new Vector3(1f, 1f + shownPull * 0.4f, 1f);
            // A little shiver while you work it loose.
            float shake = held ? Mathf.Sin(Time.time * 40f) * shownPull * 4f : 0f;
            top.localRotation = Quaternion.Euler(shake, 0f, shake * 0.5f);
        }

        IEnumerator Toss(Vector3 tossTo)
        {
            // Pops free with its root showing, then is tossed onto the pile.
            root.gameObject.SetActive(true);
            Vector3 start = transform.position + Vector3.up * pullRise * Size;
            top.localPosition = Vector3.zero;
            top.localScale = Vector3.one;
            top.localRotation = Quaternion.identity;
            Quaternion startRotation = transform.rotation;
            Quaternion endRotation = Quaternion.Euler(80f, Random.Range(0f, 360f), 0f);

            for (float time = 0f; time < tossSeconds; time += Time.deltaTime)
            {
                float t = time / tossSeconds;
                Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.25f;
                transform.SetPositionAndRotation(Vector3.Lerp(start, tossTo, t) + arc, Quaternion.Slerp(startRotation, endRotation, t));
                yield return null;
            }
            transform.SetPositionAndRotation(tossTo, endRotation);
        }
    }
}
