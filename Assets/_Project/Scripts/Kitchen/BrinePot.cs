using UnityEngine;

namespace GrandmasHouse.Kitchen
{
    // A pot of hot pickling brine kept warm on the stove. A station lifts it
    // and tips it to pour from the lip.
    public class BrinePot : MonoBehaviour
    {
        [Tooltip("The pouring lip. Its forward axis points the way brine comes out.")]
        [SerializeField] Transform lip;
        [SerializeField] ParticleSystem stream;

        public Transform Lip => lip;
        public Vector3 RestPosition { get; private set; }
        public Quaternion RestRotation { get; private set; }

        void Awake()
        {
            RestPosition = transform.position;
            RestRotation = transform.rotation;
            SetPouring(false);
        }

        public void SetPouring(bool pouring)
        {
            if (pouring && !stream.isEmitting)
            {
                stream.Play();
            }
            else if (!pouring && stream.isEmitting)
            {
                stream.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }
    }
}
