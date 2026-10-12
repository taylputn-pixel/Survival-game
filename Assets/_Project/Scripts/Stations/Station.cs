using System.Collections.Generic;
using GrandmasHouse.Carrying;
using GrandmasHouse.Data;
using GrandmasHouse.DayCycle;
using GrandmasHouse.Interaction;
using GrandmasHouse.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GrandmasHouse.Stations
{
    // A hands-on work spot (cutting board, canner, chopping block). Entering
    // one glides the camera into a fixed working view, pauses walking, and
    // gives you a cursor driven by the mouse or right stick. Back leaves.
    public abstract class Station : MonoBehaviour, IInteractable
    {
        [SerializeField] protected PlayerSettings settings;
        [Tooltip("Where the camera sits while working here.")]
        [SerializeField] Transform viewPoint;
        [SerializeField] protected Reticle reticle;
        [Tooltip("Paused while working here, if the mode says so.")]
        [SerializeField] DayClock clock;

        protected bool IsWorking => isActive && !leaving;
        protected Camera View { get; private set; }
        // The station cursor, in viewport coordinates (0..1).
        protected Vector2 CursorPoint { get; private set; }
        protected InputAction UseAction { get; private set; }

        InputAction lookAction;
        InputAction backAction;
        FirstPersonController controller;
        CursorLock cursorLock;
        Transform cameraHome;
        bool isActive;
        bool leaving;
        float blend;

        protected virtual void Awake()
        {
            UseAction = InputSystem.actions.FindAction("Player/Interact", throwIfNotFound: true);
            lookAction = InputSystem.actions.FindAction("Player/Look", throwIfNotFound: true);
            backAction = InputSystem.actions.FindAction("Player/Back", throwIfNotFound: true);
        }

        public abstract bool CanInteract(PlayerHands hands);
        public abstract void Interact(PlayerHands hands);

        // Things the gamepad cursor gently drifts onto when close (tools, handles).
        protected virtual IEnumerable<Vector3> SnapTargets() { yield break; }
        protected virtual void OnEnter() { }
        protected virtual void OnLeave() { }
        protected virtual void WhileWorking() { }

        protected void Enter(PlayerHands hands)
        {
            controller = hands.Controller;
            cursorLock = hands.GetComponent<CursorLock>();
            View = hands.ViewPoint.GetComponent<Camera>();
            cameraHome = View.transform.parent;

            controller.InputEnabled = false;
            cursorLock.BlockUnlock = true;
            CursorPoint = new Vector2(0.5f, 0.5f);
            clock.PausedForWork = true;
            isActive = true;
            leaving = false;
            OnEnter();
        }

        protected void Leave()
        {
            leaving = true;
            reticle.ScreenPoint = null;
            reticle.Highlight = null;
            reticle.Hidden = false;
            OnLeave();
        }

        protected Ray CursorRay() => View.ViewportPointToRay(CursorPoint);

        protected virtual void Update()
        {
            if (!IsWorking)
            {
                return;
            }

            if (backAction.WasPressedThisFrame())
            {
                Leave();
                return;
            }

            MoveCursor();
            reticle.ScreenPoint = CursorPoint;
            WhileWorking();
        }

        void LateUpdate()
        {
            if (!isActive)
            {
                return;
            }

            // Glide between the player's eyes and the working view.
            float step = Time.deltaTime / settings.stationCameraSeconds;
            blend = Mathf.Clamp01(blend + (leaving ? -step : step));
            float t = blend * blend * (3f - 2f * blend);
            View.transform.SetPositionAndRotation(
                Vector3.Lerp(cameraHome.position, viewPoint.position, t),
                Quaternion.Slerp(cameraHome.rotation, viewPoint.rotation, t));

            if (leaving && blend <= 0f)
            {
                View.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                isActive = false;
                clock.PausedForWork = false;
                controller.InputEnabled = true;
                cursorLock.BlockUnlock = false;
            }
        }

        void MoveCursor()
        {
            Vector2 input = lookAction.ReadValue<Vector2>();
            bool gamepad = lookAction.activeControl?.device is Gamepad;
            Vector2 delta = gamepad
                ? input * settings.stationStickSpeed * Time.deltaTime
                : input * settings.stationMouseSpeed;
            // Viewport x spans the screen width, so scale it to match y in on-screen distance.
            float aspect = (float)Screen.width / Screen.height;
            delta.x /= aspect;
            Vector2 point = CursorPoint + delta;

            if (gamepad)
            {
                point = DriftTowardTargets(point, aspect);
            }

            point.x = Mathf.Clamp01(point.x);
            point.y = Mathf.Clamp01(point.y);
            CursorPoint = point;
        }

        Vector2 DriftTowardTargets(Vector2 point, float aspect)
        {
            foreach (Vector3 target in SnapTargets())
            {
                Vector3 screen = View.WorldToViewportPoint(target);
                Vector2 offset = (Vector2)screen - point;
                float distance = new Vector2(offset.x * aspect, offset.y).magnitude;
                if (screen.z > 0f && distance < settings.stationSnapRadius)
                {
                    float pull = 1f - Mathf.Exp(-settings.stationSnapStrength * Time.deltaTime);
                    return point + offset * pull;
                }
            }
            return point;
        }
    }
}
