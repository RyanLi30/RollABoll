using System;
using System.IO;
using System.Linq;
using Ryan.RollABall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Ryan.RollABall.Editor
{
    public static class PrototypeValidation
    {
        private static int stage, padIndex, level = 1;
        private static float deadline, risingVelocity;
        private static double timeout;
        private static Vector3 initialPosition, landing;
        private static RollABallGame game;
        private static Transform[] pads;
        private static Keyboard keyboard;
        private static Key[] heldKeys = Array.Empty<Key>();
        private static bool previousOptionsEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static InputSettings.BackgroundBehavior previousBackground;
        private static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Run validation in a separate batch editor.");
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            previousBackground = InputSystem.settings.backgroundBehavior;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            stage = 0;
            timeout = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Tick;
            InputSystem.onBeforeUpdate += QueueInput;
            var gameView = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            gameView.Show();
            gameView.Focus();
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout) throw new Exception("Timed out at stage " + stage + ", level " + level);
                if (!EditorApplication.isPlaying || Time.time < deadline) return;
                if (stage == 0)
                {
                    game = UnityEngine.Object.FindFirstObjectByType<RollABallGame>();
                    if (game == null || game.Remaining != 8) return;
                    keyboard = InputSystem.AddDevice<Keyboard>();
                    Check(!Camera.main.orthographic, "Camera uses 3D perspective");
                    initialPosition = game.player.transform.position;
                    Press(Key.W);
                    Next(1, 0.5f);
                }
                else if (stage == 1)
                {
                    Check(game.player.transform.position.z > initialPosition.z + 0.4f, "W moves the ball on the starting platform");
                    Press();
                    Teleport(new Vector3(0, 0.7f, -2));
                    Next(2, 0.3f);
                }
                else if (stage == 2) { Press(Key.Space); Next(3, 0.12f); }
                else if (stage == 3)
                {
                    Check(Body.linearVelocity.y > 4 && Body.position.y > 1, "Space jumps from a grounded platform");
                    risingVelocity = Body.linearVelocity.y;
                    Press();
                    Next(4, 0.08f);
                }
                else if (stage == 4) { Press(Key.Space); Next(5, 0.12f); }
                else if (stage == 5)
                {
                    Check(Body.linearVelocity.y < risingVelocity - 0.5f, "Airborne Space press cannot trigger a double jump");
                    Press();
                    var checkpoint = UnityEngine.Object.FindObjectsByType<ParkourCheckpoint>(FindObjectsSortMode.None)
                        .OrderBy(c => c.number).First();
                    Teleport(checkpoint.respawnPosition);
                    Next(6, 0.3f);
                }
                else if (stage == 6)
                {
                    Check(game.player.CheckpointNumber == 1, "Green checkpoint activates through physics contact");
                    int remaining = game.Remaining;
                    report.AppendLine("INFO: Remaining before falling = " + remaining);
                    Teleport(new Vector3(0, -9, 0));
                    Next(7, 0.3f);
                }
                else if (stage == 7)
                {
                    Check(game.player.Falls == 1 && Vector3.Distance(Body.position, game.player.RespawnPosition) < 0.3f,
                        "Falling restores the ball to the latest checkpoint");
                    Check(game.Remaining == game.cubes.Count(c => c.gameObject.activeSelf), "Falling preserves collected cubes");
                    Press(Key.R);
                    Next(8, 0.4f);
                }
                else if (stage == 8)
                {
                    Press();
                    game = UnityEngine.Object.FindFirstObjectByType<RollABallGame>();
                    Check(game.Remaining == 8 && game.player.Falls == 0 && game.player.CheckpointNumber == 0,
                        "R resets the current level, collectibles and checkpoints");
                    SetupCourse();
                    Next(20, 0.1f);
                }
                else if (stage == 20)
                {
                    var from = pads[padIndex - 1];
                    var to = pads[padIndex];
                    float launchX = Mathf.Clamp(to.position.x, from.position.x - from.localScale.x / 2 + 0.85f,
                        from.position.x + from.localScale.x / 2 - 0.85f);
                    Teleport(new Vector3(launchX, from.position.y + from.localScale.y / 2 + 0.67f,
                        from.position.z + from.localScale.z / 2 - 0.85f));
                    landing = new Vector3(to.position.x, to.position.y + to.localScale.y / 2 + 0.65f, to.position.z + 0.5f);
                    Press();
                    Next(21, 0.3f);
                }
                else if (stage == 21)
                {
                    Check(game.player.IsGrounded, "Level " + level + " platform " + padIndex + ": valid takeoff surface");
                    Press(Key.Space);
                    Next(22, 0.1f);
                }
                else if (stage == 22)
                {
                    Check(Body.linearVelocity.y > 4, "Level " + level + " platform " + padIndex + ": jump input works");
                    Press();
                    float gravity = -Physics.gravity.y;
                    float velocityY = Body.linearVelocity.y;
                    float discriminant = velocityY * velocityY - 2 * gravity * (landing.y - Body.position.y);
                    Check(discriminant > 0, "Next platform is below jump apex");
                    float flight = (velocityY + Mathf.Sqrt(discriminant)) / gravity;
                    var displacement = landing - Body.position;
                    displacement.y = 0;
                    var planarVelocity = displacement * (0.35f / (1 - Mathf.Exp(-0.35f * flight)));
                    Check(planarVelocity.magnitude <= 8, "Gap is reachable within player speed: " + planarVelocity.magnitude.ToString("0.00"));
                    // Launch at the tested speed; allow real gravity, drag, colliders and triggers to determine landing.
                    Body.linearVelocity = new Vector3(planarVelocity.x, velocityY, planarVelocity.z);
                    Next(23, flight + 0.08f);
                }
                else if (stage == 23)
                {
                    Check(!game.cubes[padIndex - 1].gameObject.activeSelf,
                        "Level " + level + " platform " + padIndex + ": physical jump reaches collectible");
                    Check(game.Remaining == game.cubes.Length - padIndex, "Collection count is correct");
                    if (!game.HasWon)
                    {
                        Check(Mathf.Abs(Body.position.y - landing.y) < 0.4f, "Ball lands on the destination platform");
                        Body.linearVelocity = Vector3.zero;
                        padIndex++;
                        Next(20, 0.1f);
                    }
                    else
                    {
                        Check(!game.player.CanMove && Body.isKinematic, "Level completion stops the ball");
                        Directory.CreateDirectory("output/rollaball");
                        ScreenCapture.CaptureScreenshot("output/rollaball/parkour-level-" + level + "-complete.png");
                        Press(Key.Enter);
                        Next(30, 0.4f);
                    }
                }
                else if (stage == 30)
                {
                    Press();
                    game = UnityEngine.Object.FindFirstObjectByType<RollABallGame>();
                    if (level == 3)
                    {
                        Check(game.levelNumber == 1 && game.Remaining == 8, "Final victory replays from level 1");
                        Next(40, 1);
                    }
                    else
                    {
                        level++;
                        Check(game.levelNumber == level && game.Remaining == (level == 2 ? 10 : 12), "Enter advances to level " + level);
                        SetupCourse();
                        Next(20, 0.1f);
                    }
                }
                else if (stage == 40)
                {
                    ScreenCapture.CaptureScreenshot("output/rollaball/parkour-gameplay.png");
                    Next(41, 0.5f);
                }
                else if (stage == 41) Finish(0);
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL: " + exception);
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static Rigidbody Body => game.player.GetComponent<Rigidbody>();
        private static void SetupCourse()
        {
            pads = GameObject.Find("Landing platforms").GetComponentsInChildren<Transform>()
                .Where(t => t.name.StartsWith("Landing ") && t.GetComponent<BoxCollider>() != null).OrderBy(t => t.name).ToArray();
            Check(pads.Length == game.cubes.Length + 1, "Course has a start and one landing platform per cube");
            Check(pads.Last().position.y > pads.First().position.y + 2,
                "Level " + level + " has real elevation changes");
            Check(!Camera.main.orthographic, "Level " + level + " uses perspective camera");
            if (level > 1)
                Check(GameObject.Find("Narrow bridges").transform.childCount > 0, "Later level contains balance bridges");
            padIndex = 1;
        }
        private static void Press(params Key[] keys) => heldKeys = keys;
        private static void QueueInput()
        {
            if (keyboard != null && InputState.currentUpdateType == InputUpdateType.Dynamic)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(heldKeys));
        }
        private static void Next(int next, float delay) { stage = next; deadline = Time.time + delay; }
        private static void Teleport(Vector3 position)
        {
            Body.position = position;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message + " at " + Body.position);
            report.AppendLine("PASS: " + message);
        }
        private static void Finish(int exitCode)
        {
            EditorApplication.update -= Tick;
            InputSystem.onBeforeUpdate -= QueueInput;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = previousBackground;
            EditorSettings.enterPlayModeOptionsEnabled = previousOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            Directory.CreateDirectory("output/rollaball");
            File.WriteAllText("output/rollaball/parkour-validation.txt", report.ToString());
            Debug.Log("PARKOUR_VALIDATION_EXIT: " + exitCode + "\n" + report);
            EditorApplication.Exit(exitCode);
        }
    }
}
