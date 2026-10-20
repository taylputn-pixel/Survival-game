using System.Collections;
using GrandmasHouse.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace GrandmasHouse.Tests
{
    // Real button presses through the Input System, on temporary fake
    // devices, so the bindings themselves are tested. (InputTestFixture
    // resets the Input System, which drops the game's project-wide actions
    // and can't put them back in play mode, so it isn't used.)
    public class InputTests
    {
        Keyboard keyboard;
        Gamepad gamepad;

        [UnitySetUp]
        public IEnumerator LoadHomestead()
        {
            keyboard = InputSystem.AddDevice<Keyboard>("Test Keyboard");
            gamepad = InputSystem.AddDevice<Gamepad>("Test Gamepad");
            Homestead.UseMemorySaves();
            yield return Homestead.Load();
            // The first test after a recompile can lose its first key press
            // while the actions take in the new devices; settle them first.
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F12));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            yield return null;
        }

        [TearDown]
        public void RemoveDevices()
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(gamepad);
            Homestead.RestoreRealSaves();
        }

        static IEnumerator HoldFor(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        static float WalkedForward(Transform player, Vector3 start, Vector3 ahead) => Vector3.Dot(player.position - start, ahead);

        // The player starts squeezed between a kitchen chair and the wall,
        // so move them to the nearest open floor, facing the open way.
        static Transform PlayerFacingOpenFloor()
        {
            var player = Homestead.Find<FirstPersonController>().transform;
            var body = player.GetComponent<CharacterController>();
            Vector3 home = player.position;
            for (float reach = 0f; reach <= 4f; reach += 0.5f)
            {
                for (float around = 0f; around < 360f; around += reach > 0f ? 45f : 360f)
                {
                    Vector3 spot = home + Quaternion.Euler(0f, around, 0f) * Vector3.forward * reach;
                    if (!Physics.Raycast(spot + Vector3.up * 1.5f, Vector3.down, out RaycastHit floor, 2f, ~0, QueryTriggerInteraction.Ignore)
                        || floor.collider.transform.IsChildOf(player)
                        || Mathf.Abs(floor.point.y - home.y) > 0.1f)
                    {
                        continue;
                    }
                    for (float yaw = 0f; yaw < 360f; yaw += 45f)
                    {
                        if (Room(player, body, floor.point, yaw) >= 1.5f)
                        {
                            body.enabled = false;
                            player.SetPositionAndRotation(floor.point + Vector3.up * body.skinWidth, Quaternion.Euler(0f, yaw, 0f));
                            body.enabled = true;
                            return player;
                        }
                    }
                }
            }
            Assert.Fail("No open floor near the player to walk on.");
            return player;
        }

        // How far the player could walk from feet at `at`, facing `yaw`, before
        // bumping into something too tall to step over.
        static float Room(Transform player, CharacterController body, Vector3 at, float yaw)
        {
            Vector3 center = at + body.center + Vector3.up * (body.skinWidth + body.stepOffset / 2f);
            Vector3 up = Vector3.up * (body.height / 2f - body.radius - body.stepOffset / 2f);
            Vector3 way = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            if (Physics.CheckCapsule(center + up, center - up, body.radius, ~0, QueryTriggerInteraction.Ignore)
                && OverlapsOtherThan(player, center + up, center - up, body.radius))
            {
                return 0f;
            }
            float room = 2f;
            foreach (var hit in Physics.CapsuleCastAll(center + up, center - up, body.radius, way, 2f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!hit.collider.transform.IsChildOf(player)) room = Mathf.Min(room, hit.distance);
            }
            return room;
        }

        static bool OverlapsOtherThan(Transform player, Vector3 a, Vector3 b, float radius)
        {
            foreach (var other in Physics.OverlapCapsule(a, b, radius, ~0, QueryTriggerInteraction.Ignore))
            {
                if (!other.transform.IsChildOf(player)) return true;
            }
            return false;
        }

        [UnityTest]
        public IEnumerator HoldingWWalksForward()
        {
            var player = PlayerFacingOpenFloor();
            Vector3 start = player.position;
            Vector3 ahead = player.forward;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return HoldFor(0.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;

            var fpc = player.GetComponent<FirstPersonController>();
            var move = InputSystem.actions.FindAction("Player/Move");
            Assert.Greater(WalkedForward(player, start, ahead), 0.2f, "W didn't walk the player forward. "
                + "input " + fpc.InputEnabled + ", enabled " + fpc.enabled + ", body " + player.GetComponent<CharacterController>().enabled
                + ", lying " + fpc.LyingDown + ", speed x" + fpc.SpeedMultiplier + ", move action " + move.enabled + " map " + move.actionMap.enabled
                + ", moved " + (player.position - start) + ", time " + Time.time + ", timeScale " + Time.timeScale);
        }

        [UnityTest]
        public IEnumerator TheLeftStickWalksForward()
        {
            var player = PlayerFacingOpenFloor();
            Vector3 start = player.position;
            Vector3 ahead = player.forward;

            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
            yield return HoldFor(0.5f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;

            Assert.Greater(WalkedForward(player, start, ahead), 0.2f, "The left stick didn't walk the player forward.");
        }

        [UnityTest]
        public IEnumerator LettingGoStops()
        {
            var player = PlayerFacingOpenFloor();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return HoldFor(0.3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            // Let the walk ease to a stop.
            yield return HoldFor(0.5f);

            Vector3 stopped = player.position;
            yield return HoldFor(0.3f);
            Assert.Less(Vector3.Distance(stopped, player.position), 0.01f, "The player kept walking after W was let go.");
        }
    }
}
