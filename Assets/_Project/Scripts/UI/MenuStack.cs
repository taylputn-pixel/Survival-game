using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GrandmasHouse.UI
{
    // A stack of menu screens on one canvas. Opening a screen hides the one
    // below; Back (or Pause) returns to it. The first button is selected so
    // a controller can drive the menu straight away.
    public class MenuStack : MonoBehaviour
    {
        readonly Stack<GameObject> screens = new Stack<GameObject>();
        readonly Dictionary<GameObject, Selectable> remembered = new Dictionary<GameObject, Selectable>();
        InputAction backAction;
        InputAction pauseAction;
        int openedFrame;

        public bool IsOpen => screens.Count > 0;
        // While true (waiting for a key to rebind), Back doesn't close screens.
        public bool Busy { get; set; }
        // Set to stop Back from closing the last screen (the title menu).
        public bool KeepRoot { get; set; }
        // Softly darken the game behind the menu (the pause menu).
        public bool DimBehind { get; set; }

        GameObject backdrop;
        public event Action Emptied;

        void Awake()
        {
            backAction = InputSystem.actions.FindAction("Player/Back", throwIfNotFound: true);
            pauseAction = InputSystem.actions.FindAction("Player/Pause", throwIfNotFound: true);
            EnsureEventSystem();
        }

        public void Push(GameObject screen, Selectable first)
        {
            if (screens.Count == 0 && DimBehind && backdrop == null)
            {
                RectTransform dim = UIKit.Rect("Backdrop", transform);
                UIKit.Stretch(dim);
                dim.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.07f, 0.04f, 0.45f);
                // Behind the menu, or it would catch every mouse click.
                dim.SetSiblingIndex(screen.transform.GetSiblingIndex());
                backdrop = dim.gameObject;
            }
            if (screens.Count > 0)
            {
                GameObject below = screens.Peek();
                var current = EventSystem.current.currentSelectedGameObject;
                remembered[below] = current != null ? current.GetComponent<Selectable>() : null;
                below.SetActive(false);
            }
            screens.Push(screen);
            screen.SetActive(true);
            openedFrame = Time.frameCount;
            Select(first);
        }

        public void Pop()
        {
            if (screens.Count == 0)
            {
                return;
            }
            Destroy(screens.Pop());
            if (screens.Count > 0)
            {
                GameObject below = screens.Peek();
                below.SetActive(true);
                remembered.TryGetValue(below, out Selectable last);
                Select(last != null ? last : below.GetComponentInChildren<Selectable>());
            }
            else
            {
                Destroy(backdrop);
                EventSystem.current.SetSelectedGameObject(null);
                Emptied?.Invoke();
            }
        }

        public void CloseAll()
        {
            while (screens.Count > 0)
            {
                Destroy(screens.Pop());
            }
            remembered.Clear();
            Destroy(backdrop);
            EventSystem.current.SetSelectedGameObject(null);
        }

        void Update()
        {
            if (!IsOpen || Busy || Time.frameCount == openedFrame)
            {
                return;
            }
            if (backAction.WasPressedThisFrame() || pauseAction.WasPressedThisFrame())
            {
                if (screens.Count > 1 || !KeepRoot)
                {
                    Pop();
                }
            }
            // Keep something selected so the controller never loses its place.
            if (IsOpen && EventSystem.current.currentSelectedGameObject == null && Gamepad.current != null)
            {
                Select(screens.Peek().GetComponentInChildren<Selectable>());
            }
        }

        static void Select(Selectable selectable)
        {
            if (selectable != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
    }
}
