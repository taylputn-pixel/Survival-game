using System;
using UnityEngine;

namespace GrandmasHouse.Saving
{
    // Remembers where you were standing and which way you faced.
    [RequireComponent(typeof(CharacterController))]
    public class PlayerSave : MonoBehaviour, ISaveable
    {
        [Serializable]
        class State
        {
            public Vector3 position;
            public float yaw;
        }

        public string SaveKey => "player";

        public string Capture() => JsonUtility.ToJson(new State { position = transform.position, yaw = transform.eulerAngles.y });

        public void Restore(string json)
        {
            var state = JsonUtility.FromJson<State>(json);
            // The character controller would undo a direct move, so pause it.
            var controller = GetComponent<CharacterController>();
            controller.enabled = false;
            transform.SetPositionAndRotation(state.position, Quaternion.Euler(0f, state.yaw, 0f));
            controller.enabled = true;
        }
    }
}
