using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

namespace Ryan.RollABall.Editor
{
    public static class RollingSkyValidation
    {
        private static int stage, level = 1, boardIndex, hazardIndex, previousBounces;
        private static float deadline;
        private static double timeout;
        private static bool oldEnabled;
        private static EnterPlayModeOptions oldOptions;
        private static InputSettings.BackgroundBehavior oldBackground;
        private static Keyboard keyboard;
        private static Key[] held = Array.Empty<Key>();
        private static RollABallGame game, previousGame;
        private static SpringBoard[] boards;
        private static MovingObstacle[] hazards;
        private static Vector3 destination;
        private static Vector3[] oldPositions;
        private static Quaternion[] oldRotations;
        private static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Separate batch editor only.");
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            oldEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
            oldBackground = InputSystem.settings.backgroundBehavior;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            EditorApplication.update += Tick;
            InputSystem.onBeforeUpdate += QueueInput;
            timeout = EditorApplication.timeSinceStartup + 240;
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout) throw new Exception("Timeout stage " + stage + ", level " + level);
                if (!EditorApplication.isPlaying || Time.time < deadline) return;
                game = UnityEngine.Object.FindFirstObjectByType<RollABallGame>();
                if (game == null || game.Remaining == 0) return;
                if (stage == 0)
                {
                    if (keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>();
                    boards = UnityEngine.Object.FindObjectsByType<SpringBoard>(FindObjectsSortMode.None).OrderBy(b => b.name).ToArray();
                    hazards = Hazards;
                    Check(game.levelNumber == level && boards.Length == level + 1, "Level " + level + " springboard count");
                    Check(hazards.Length == (level == 1 ? 1 : level == 2 ? 3 : 4), "Progressive moving hazard count");
                    Check(hazards.All(h => h.GetComponent<Rigidbody>().isKinematic && h.GetComponent<RestartHazard>() != null),
                        "Moving colliders use kinematic physics and restart on contact");
                    oldPositions = hazards.Select(h => h.GetComponent<Rigidbody>().position).ToArray();
                    oldRotations = hazards.Select(h => h.GetComponent<Rigidbody>().rotation).ToArray();
                    boardIndex = 0;
                    hazardIndex = 0;
                    Press();
                    Next(1, 0.7f);
                }
                else if (stage == 1)
                {
                    for (int i = 0; i < hazards.Length; i++)
                    {
                        var body = hazards[i].GetComponent<Rigidbody>();
                        Check(Vector3.Distance(oldPositions[i], body.position) > 0.15f
                            || Quaternion.Angle(oldRotations[i], body.rotation) > 10,
                            "Level " + level + ": obstacle " + i + " physically moves or rotates");
                    }
                    // A ball underneath the trigger must not be launched upward through the platform.
                    MoveBall(boards[0].transform.position + Vector3.down * 0.7f);
                    Next(2, 0.1f);
                }
                else if (stage == 2)
                {
                    Check(game.player.Bounces == 0, "Springboard cannot activate from underneath");
                    MoveBall(new Vector3(0, 0.7f, -2));
                    Next(10, 0.1f);
                }
                else if (stage == 10)
                {
                    var board = boards[boardIndex];
                    previousBounces = game.player.Bounces;
                    destination = board.destination;
                    MoveBall(board.transform.position + Vector3.up * 0.53f);
                    Press(Key.W); // Holding forward must not overshoot the guided landing.
                    Next(11, 0.14f);
                }
                else if (stage == 11)
                {
                    Check(game.player.Bounces == previousBounces + 1 && game.player.SpringInFlight && Body.linearVelocity.y > 7,
                        "Level " + level + " spring " + (boardIndex + 1) + ": touching top automatically launches ball");
                    if (boardIndex == 0)
                    {
                        Directory.CreateDirectory("output/rollaball");
                        ScreenCapture.CaptureScreenshot("output/rollaball/rolling-sky-level-" + level + "-bounce.png");
                    }
                    Next(12, 0.05f);
                }
                else if (stage == 12)
                {
                    if (game.player.SpringInFlight) return;
                    Press();
                    Next(13, 0.15f);
                }
                else if (stage == 13)
                {
                    var delta = Body.position - destination;
                    Check(new Vector2(delta.x, delta.z).magnitude < 1.1f && Mathf.Abs(delta.y) < 0.25f,
                        "Spring lands on its next platform while W is held; error=" + delta);
                    Check(game.player.Bounces == previousBounces + 1 && game.player.CanMove,
                        "No repeated bounce while touching a board; control returns after landing");
                    boardIndex++;
                    if (boardIndex < boards.Length) Next(10, 0.1f);
                    else Next(20, 0.1f);
                }
                else if (stage == 20)
                {
                    game.cubes[0].Collect();
                    game.player.SetCheckpoint(new Vector3(0, 3, 15), 1);
                    previousGame = game;
                    var hazard = Hazards[hazardIndex];
                    var bounds = hazard.GetComponent<Collider>().bounds;
                    MoveBall(bounds.center + Vector3.up * 0.2f);
                    Next(21, 0.3f);
                }
                else if (stage == 21)
                {
                    Check(!ReferenceEquals(previousGame, game) && game.levelNumber == level,
                        "Level " + level + " moving hazard " + (hazardIndex + 1) + ": physical contact restarts current level");
                    Check(game.Remaining == game.cubes.Length && game.player.CheckpointNumber == 0
                        && game.player.Bounces == 0 && !game.player.SpringInFlight,
                        "Restart resets collection, checkpoint and spring flight state");
                    hazardIndex++;
                    if (hazardIndex < Hazards.Length) Next(20, 0.1f);
                    else if (level < 3)
                    {
                        level++;
                        SceneManager.LoadScene(PrototypeBuilder.LevelPaths[level - 1]);
                        Next(0, 0.4f);
                    }
                    else
                    {
                        SceneManager.LoadScene(PrototypeBuilder.ScenePath);
                        Next(30, 0.8f);
                    }
                }
                else if (stage == 30)
                {
                    Check(game.levelNumber == 1 && game.Remaining == 8, "Campaign is ready from level 1");
                    ScreenCapture.CaptureScreenshot("output/rollaball/rolling-sky-gameplay.png");
                    Next(31, 0.4f);
                }
                else if (stage == 31) Finish(0);
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL: " + exception);
                Debug.LogException(exception);
                Finish(1);
            }
        }

        private static Rigidbody Body => game.player.GetComponent<Rigidbody>();
        private static MovingObstacle[] Hazards => UnityEngine.Object.FindObjectsByType<MovingObstacle>(FindObjectsSortMode.None)
            .OrderBy(h => h.name).ToArray();
        private static void Press(params Key[] keys) => held = keys;
        private static void QueueInput()
        {
            if (keyboard != null && InputState.currentUpdateType == InputUpdateType.Dynamic)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
        }
        private static void MoveBall(Vector3 position)
        {
            Body.position = position;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }
        private static void Next(int next, float delay) { stage = next; deadline = Time.time + delay; }
        private static void Check(bool result, string message)
        {
            if (!result) throw new Exception(message);
            report.AppendLine("PASS: " + message);
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            InputSystem.onBeforeUpdate -= QueueInput;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            EditorSettings.enterPlayModeOptionsEnabled = oldEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
            Directory.CreateDirectory("output/rollaball");
            File.WriteAllText("output/rollaball/rolling-sky-validation.txt", report.ToString());
            Debug.Log("ROLLING_SKY_VALIDATION_EXIT: " + code + "\n" + report);
            EditorApplication.Exit(code);
        }
    }
}
