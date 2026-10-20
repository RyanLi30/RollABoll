using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ryan.RollABall.Editor
{
    public static class HazardValidation
    {
        private static int stage, level = 2, index;
        private static RollABallGame beforeGame;
        private static float deadline;
        private static double timeout;
        private static RollABallGame game;
        private static bool previousEnabled;
        private static EnterPlayModeOptions previousOptions;
        private static GameObject testObject;
        private static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Separate batch editor only.");
            EditorSceneManager.OpenScene(PrototypeBuilder.LevelPaths[1]);
            previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
            timeout = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout) throw new Exception("Timeout at stage " + stage);
                if (!EditorApplication.isPlaying || Time.time < deadline) return;
                game = UnityEngine.Object.FindFirstObjectByType<RollABallGame>();
                if (game == null || game.Remaining == 0) return;
                if (stage == 0)
                {
                    Check(game.levelNumber == level, "Correct level loaded");
                    var hazards = Hazards;
                    Check(hazards.Length == (level == 2 ? 3 : 4), "Every orange hurdle has RestartHazard in level " + level);
                    beforeGame = game;
                    MoveBall(new Vector3(-3.4f, 0.65f, -2));
                    Next(1, 0.25f);
                }
                else if (stage == 1)
                {
                    Check(ReferenceEquals(game, beforeGame), "Ordinary start rail contact does not restart");
                    var hazard = Hazards[0].GetComponent<Collider>().bounds;
                    MoveBall(hazard.center + Vector3.up * 2.5f);
                    Next(2, 0.12f);
                }
                else if (stage == 2)
                {
                    Check(ReferenceEquals(game, beforeGame), "Passing above a hurdle without contact is safe");
                    MoveBall(new Vector3(0, 0.7f, -2));
                    testObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    testObject.transform.position = Hazards[0].transform.position;
                    testObject.AddComponent<Rigidbody>();
                    Next(3, 0.2f);
                }
                else if (stage == 3)
                {
                    Check(ReferenceEquals(game, beforeGame), "Non-player collisions do not restart");
                    UnityEngine.Object.Destroy(testObject);
                    Next(10, 0.1f);
                }
                else if (stage == 10)
                {
                    game.cubes[0].Collect();
                    game.player.SetCheckpoint(new Vector3(0, 3, 15), 1);
                    Check(game.Remaining == game.cubes.Length - 1 && game.player.CheckpointNumber == 1,
                        "Set up collected cube and checkpoint before hazard contact");
                    beforeGame = game;
                    var bounds = Hazards[index].GetComponent<Collider>().bounds;
                    MoveBall(new Vector3(bounds.center.x, bounds.min.y + 0.65f, bounds.center.z - 0.7f));
                    Next(11, 0.3f);
                }
                else if (stage == 11)
                {
                    Check(!ReferenceEquals(game, beforeGame) && game.levelNumber == level,
                        "Level " + level + " hurdle " + (index + 1) + ": physical contact reloads the same level");
                    Check(game.Remaining == game.cubes.Length && game.cubes.All(c => c.gameObject.activeSelf)
                        && game.player.CheckpointNumber == 0 && game.player.Falls == 0 && game.Elapsed < 0.8f,
                        "Restart resets cubes, checkpoint, falls and timer");
                    Check(Vector3.Distance(game.player.transform.position, new Vector3(0, 0.65f, -2)) < 0.2f,
                        "Player returns to the level start");
                    beforeGame = game;
                    Next(12, 0.25f);
                }
                else if (stage == 12)
                {
                    Check(ReferenceEquals(game, beforeGame), "One contact causes one restart");
                    index++;
                    if (index < Hazards.Length) Next(10, 0.1f);
                    else if (level == 2)
                    {
                        level = 3;
                        index = 0;
                        SceneManager.LoadScene(PrototypeBuilder.LevelPaths[2]);
                        Next(0, 0.3f);
                    }
                    else Finish(0);
                }
            }
            catch (Exception exception)
            {
                report.AppendLine("FAIL: " + exception);
                Debug.LogException(exception);
                Finish(1);
            }
        }
        private static RestartHazard[] Hazards => UnityEngine.Object.FindObjectsByType<RestartHazard>(FindObjectsSortMode.None)
            .OrderBy(h => h.name).ToArray();
        private static void MoveBall(Vector3 position)
        {
            var body = game.player.GetComponent<Rigidbody>();
            body.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
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
            EditorSettings.enterPlayModeOptionsEnabled = previousEnabled;
            EditorSettings.enterPlayModeOptions = previousOptions;
            Directory.CreateDirectory("output/rollaball");
            File.WriteAllText("output/rollaball/hazard-validation.txt", report.ToString());
            Debug.Log("HAZARD_VALIDATION_EXIT: " + code + "\n" + report);
            EditorApplication.Exit(code);
        }
    }
}
