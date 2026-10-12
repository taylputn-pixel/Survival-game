using GrandmasHouse.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Player
{
    // Walk and look in first person with keyboard/mouse or a gamepad.
    // Reads the project-wide input actions (Assets/InputSystem_Actions).
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonController : MonoBehaviour
    {
        [SerializeField] PlayerSettings settings;
        [Tooltip("The child that holds the camera. Pitch and head bob are applied here.")]
        [SerializeField] Transform cameraPivot;

        // Stations turn this off while the camera is in a working view.
        public bool InputEnabled { get; set; } = true;

        CharacterController body;
        InputAction moveAction;
        InputAction lookAction;
        InputAction briskAction;

        Vector3 horizontalVelocity;
        float verticalVelocity;
        float pitch;
        float bobTime;
        Vector3 pivotRestPosition;
        Camera view;

        void Awake()
        {
            body = GetComponent<CharacterController>();
            view = cameraPivot.GetComponentInChildren<Camera>();
            moveAction = InputSystem.actions.FindAction("Player/Move", throwIfNotFound: true);
            lookAction = InputSystem.actions.FindAction("Player/Look", throwIfNotFound: true);
            briskAction = InputSystem.actions.FindAction("Player/Sprint", throwIfNotFound: true);
        }

        void OnEnable() => InputSystem.actions.FindActionMap("Player").Enable();

        void Update()
        {
            // Read every frame so eye height and field of view can be tuned live in Play mode.
            pivotRestPosition = new Vector3(0f, settings.eyeHeight, 0f);
            view.fieldOfView = settings.fieldOfView;

            if (InputEnabled)
            {
                Look();
            }
            Move(InputEnabled ? moveAction.ReadValue<Vector2>() : Vector2.zero);
            HeadBob();
        }

        void Look()
        {
            Vector2 input = lookAction.ReadValue<Vector2>();
            Vector2 degrees;

            if (lookAction.activeControl?.device is Gamepad)
            {
                // Stick values are -1..1, so scale by time and ease small movements.
                float magnitude = Mathf.Pow(input.magnitude, settings.gamepadLookCurve);
                degrees = input.normalized * magnitude * settings.gamepadLookSpeed * Time.deltaTime;
            }
            else
            {
                // Mouse delta is already per frame, so it must not be scaled by time.
                degrees = input * settings.mouseSensitivity;
            }

            if (settings.invertY)
            {
                degrees.y = -degrees.y;
            }

            transform.Rotate(0f, degrees.x, 0f);
            pitch = Mathf.Clamp(pitch - degrees.y, -settings.maxPitch, settings.maxPitch);
            cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void Move(Vector2 input)
        {
            float speed = briskAction.IsPressed() ? settings.briskSpeed : settings.walkSpeed;
            Vector3 target = (transform.right * input.x + transform.forward * input.y) * speed;
            horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, target, settings.acceleration * Time.deltaTime);

            if (body.isGrounded && verticalVelocity < 0f)
            {
                // A small downward push keeps the controller snapped to the floor on slopes.
                verticalVelocity = -2f;
            }
            verticalVelocity += settings.gravity * Time.deltaTime;

            body.Move((horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime);
        }

        void HeadBob()
        {
            float speed = horizontalVelocity.magnitude;
            if (settings.headBobAmount <= 0f || speed < 0.1f || !body.isGrounded)
            {
                cameraPivot.localPosition = Vector3.Lerp(cameraPivot.localPosition, pivotRestPosition, 8f * Time.deltaTime);
                return;
            }

            bobTime += Time.deltaTime * settings.headBobFrequency * (speed / settings.walkSpeed);
            float offset = Mathf.Sin(bobTime * Mathf.PI * 2f) * settings.headBobAmount;
            cameraPivot.localPosition = pivotRestPosition + Vector3.up * offset;
        }
    }
}
