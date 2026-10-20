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
    public static class ThreeLaneValidation
    {
        private static int stage, level = 1, springIndex, bounceBefore, hazardIndex;
        private static float deadline;
        private static double timeout;
        private static bool oldEnabled;
        private static EnterPlayModeOptions oldOptions;
        private static InputSettings.BackgroundBehavior oldBackground;
        private static Keyboard keyboard;
        private static Key[] held = Array.Empty<Key>();
        private static RollABallGame game, previousGame;
        private static CourseSection[] sections;
        private static SpringBoard[] springs;
        private static RestartHazard[] contactHazards;
        private static Vector3 landing;
        private static MovingObstacle motion;
        private static Vector3 motionStart;
        private static float observedTravel;
        private static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
        public static void RunLevel3()
        {
            level = 3;
            if (File.Exists("output/rollaball/three-lane-validation.txt"))
                foreach (var line in File.ReadAllLines("output/rollaball/three-lane-validation.txt").Where(l => l.StartsWith("PASS:")))
                    report.AppendLine(line);
            report.AppendLine("INFO: Retained completed checks from levels 1 and 2; validating level 3.");
            Run();
        }
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch editor only.");
            EditorSceneManager.OpenScene(PrototypeBuilder.LevelPaths[level - 1]);
            oldEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
            oldBackground = InputSystem.settings.backgroundBehavior;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var view = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.position = new Rect(0, 0, 1280, 760);
            view.Show();
            view.Focus();
            EditorApplication.update += Tick;
            InputSystem.onBeforeUpdate += Queue;
            timeout = EditorApplication.timeSinceStartup + 360;
            EditorApplication.EnterPlaymode();
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout) throw new Exception("Timeout stage " + stage + " level " + level);
                if (stage == 1 && motion != null)
                    observedTravel = Mathf.Max(observedTravel, Vector3.Distance(motionStart, motion.GetComponent<Rigidbody>().position));
                if (!EditorApplication.isPlaying || Time.time < deadline) return;
                game = UnityEngine.Object.FindFirstObjectByType<RollABallGame>();
                if (game == null || game.Remaining == 0 && !game.HasWon) return;
                if (stage == 0)
                {
                    if (keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>();
                    sections = UnityEngine.Object.FindObjectsByType<CourseSection>(FindObjectsSortMode.None).OrderBy(s => s.number).ToArray();
                    springs = UnityEngine.Object.FindObjectsByType<SpringBoard>(FindObjectsSortMode.None).OrderBy(s => s.name).ToArray();
                    Check(game.levelNumber == level && game.cubes.Length == 6 + 2 * level, "Level " + level + " initializes with correct cubes");
                    Check(sections.Length == 5 + level && sections.All(s => s.GetComponent<BoxCollider>().bounds.size.x > 11.9f), "Full three-lane road sections");
                    Check(Mathf.Approximately(game.player.MaximumSpeed, 12 + level * 2), "Forward speed = " + game.player.MaximumSpeed);
                    Check(springs.Length == sections.Count(s => s.requiresSpring) * 3, "Every long gap has a spring in all three lanes");
                    foreach (var section in sections.Where(s => s.requiresSpring))
                        Check(section.gapAfter > game.player.MaximumSpeed * (2 * game.player.JumpSpeed / -Physics.gravity.y + 0.1f) + 2,
                            "Long gap " + section.gapAfter + "m exceeds best normal jump including coyote allowance");
                    var hazards = UnityEngine.Object.FindObjectsByType<RestartHazard>(FindObjectsSortMode.None);
                    Check(hazards.All(h => h.transform.Find("White warning panel") != null && h.transform.Find("Red chevron") != null),
                        "Every hazard has red-white chevrons and warning panel");
                    foreach (var section in sections)
                    {
                        int safe = (section.number + level) % 3 - 1;
                        var point = new Vector3(safe * 4, section.transform.position.y + 1.5f, section.transform.position.z + 1);
                        Check(!Physics.OverlapSphere(point, 0.7f, ~0, QueryTriggerInteraction.Ignore).Any(c => c.GetComponent<RestartHazard>() != null),
                            "Barrier row " + section.number + " leaves an open ball-width lane");
                    }
                    motion = UnityEngine.Object.FindFirstObjectByType<MovingObstacle>();
                    motionStart = motion.GetComponent<Rigidbody>().position;
                    observedTravel = 0;
                    Press(Key.W);
                    Next(1, 0.65f);
                }
                else if (stage == 1)
                {
                    Check(Body.linearVelocity.z >= game.player.MaximumSpeed - 0.2f, "Actual W speed reaches the new maximum");
                    Check(observedTravel > 0.2f, "Warning barriers actually move");
                    ReturnStart();
                    Press(Key.D);
                    Next(2, 0.8f);
                }
                else if (stage == 2)
                {
                    Check(game.player.CurrentLane == 1 && Body.position.x > 3.7f, "D changes to right lane");
                    Press(); Next(3, 0.08f);
                }
                else if (stage == 3) { Press(Key.A); Next(4, 0.8f); }
                else if (stage == 4)
                {
                    Check(game.player.CurrentLane == 0 && Mathf.Abs(Body.position.x) < 0.3f, "A returns to center lane");
                    Press(); Next(5, 0.08f);
                }
                else if (stage == 5) { Press(Key.A); Next(6, 0.8f); }
                else if (stage == 6)
                {
                    Check(game.player.CurrentLane == -1 && Body.position.x < -3.7f, "A changes to left lane");
                    Press();
                    foreach (var spring in springs) { spring.enabled = false; spring.GetComponent<Collider>().enabled = false; }
                    var from = sections[0];
                    game.player.ReturnToCourse(new Vector3(0, from.entry.y, from.transform.position.z + 15.3f), 0);
                    Body.linearVelocity = Vector3.forward * game.player.MaximumSpeed;
                    Press(Key.W, Key.Space);
                    Next(7, 0.15f);
                }
                else if (stage == 7)
                {
                    Check(Body.linearVelocity.y > 3, "Normal jump launches at full forward speed");
                    Press(Key.W);
                    Next(8, 1.15f);
                }
                else if (stage == 8)
                {
                    float nextEdge = sections[1].transform.position.z - 16;
                    Check(Body.position.z < nextEdge - 0.65f && Body.position.y < sections[1].entry.y - 0.5f,
                        "Without springboard, full-speed jump falls short of the long gap");
                    foreach (var spring in springs) { spring.enabled = true; spring.GetComponent<Collider>().enabled = true; }
                    var shortFrom = sections[1];
                    int safeLane = (2 + level) % 3 - 1;
                    game.player.ReturnToCourse(new Vector3(safeLane * 4, shortFrom.entry.y, shortFrom.transform.position.z + 15.3f), 0);
                    Body.linearVelocity = Vector3.forward * game.player.MaximumSpeed;
                    Press(Key.W, Key.Space);
                    Next(9, 0.15f);
                }
                else if (stage == 9)
                {
                    Check(Body.linearVelocity.y > 3, "Ordinary jump launches over the short gap");
                    Press(Key.W);
                    Next(91, 0.9f);
                }
                else if (stage == 91)
                {
                    Check(Body.position.z > sections[2].transform.position.z - 15.3f, "Ordinary jump crosses the short gap");
                    Press(); Next(92, 0.25f);
                }
                else if (stage == 92)
                {
                    Check(Mathf.Abs(Body.position.y - sections[2].entry.y) < 0.3f, "Ordinary jump lands safely on the next three-lane road");
                    ReturnStart();
                    springIndex = 0;
                    Press();
                    Directory.CreateDirectory("output/rollaball");
                    ScreenCapture.CaptureScreenshot("output/rollaball/three-lane-level-" + level + ".png");
                    Next(10, 0.2f);
                }
                else if (stage == 10)
                {
                    var spring = springs[springIndex];
                    bounceBefore = game.player.Bounces;
                    landing = spring.destination;
                    game.player.ReturnToCourse(spring.transform.position + Vector3.up * 0.53f, 0);
                    Press(Key.W);
                    Next(11, 0.15f);
                }
                else if (stage == 11)
                {
                    Check(game.player.Bounces == bounceBefore + 1 && game.player.SpringInFlight,
                        "Level " + level + " spring " + (springIndex + 1) + " launches across the required gap");
                    Check(Body.linearVelocity.z > game.player.MaximumSpeed, "Spring supplies a real forward speed boost");
                    Next(12, 0.1f);
                }
                else if (stage == 12)
                {
                    if (game.player.SpringInFlight) return;
                    Press(); Next(13, 0.12f);
                }
                else if (stage == 13)
                {
                    var delta = Body.position - landing;
                    Check(new Vector2(delta.x, delta.z).magnitude < 1 && Mathf.Abs(delta.y) < 0.3f,
                        "Spring lands safely in the correct lane, error=" + delta);
                    springIndex++;
                    if (springIndex < springs.Length) Next(10, 0.1f);
                    else
                    {
                        var all = UnityEngine.Object.FindObjectsByType<RestartHazard>(FindObjectsSortMode.None);
                        contactHazards = new[] { all.First(h => h.GetComponent<MovingObstacle>() != null), all.First(h => h.GetComponent<MovingObstacle>() == null) };
                        hazardIndex = 0;
                        Next(20, 0.1f);
                    }
                }
                else if (stage == 20)
                {
                    // Reacquire after scene restart.
                    var all = UnityEngine.Object.FindObjectsByType<RestartHazard>(FindObjectsSortMode.None);
                    var hazard = hazardIndex == 0 ? all.First(h => h.GetComponent<MovingObstacle>() != null) : all.First(h => h.GetComponent<MovingObstacle>() == null);
                    previousGame = game;
                    game.player.ReturnToCourse(hazard.GetComponent<Collider>().bounds.center, 0);
                    Next(21, 0.3f);
                }
                else if (stage == 21)
                {
                    Check(!ReferenceEquals(previousGame, game) && game.levelNumber == level && game.Remaining == game.cubes.Length,
                        (hazardIndex == 0 ? "Moving" : "Static") + " red-white barrier contact fully restarts current level");
                    hazardIndex++;
                    if (hazardIndex < 2) Next(20, 0.1f);
                    else
                    {
                        game.cubes[0].Collect();
                        previousGame = game;
                        game.player.ReturnToCourse(GameObject.Find("Finish - recover missed cubes").transform.position, 0);
                        Next(22, 0.3f);
                    }
                }
                else if (stage == 22)
                {
                    Check(ReferenceEquals(game, previousGame) && !game.cubes[0].gameObject.activeSelf && Body.position.z < 20,
                        "Finish recovers missed cubes without losing collected progress");
                    foreach (var cube in game.cubes) cube.Collect();
                    Check(game.HasWon, "All cubes complete the level");
                    Press(Key.Enter);
                    Next(23, 0.4f);
                }
                else if (stage == 23)
                {
                    Press();
                    if (level < 3)
                    {
                        level++;
                        Check(game.levelNumber == level, "Enter advances to next level");
                        Next(0, 0.2f);
                    }
                    else
                    {
                        Check(game.levelNumber == 1, "Final victory restarts campaign from level 1");
                        Next(30, 0.6f);
                    }
                }
                else if (stage == 30)
                {
                    ScreenCapture.CaptureScreenshot("output/rollaball/three-lane-gameplay.png");
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
        private static void ReturnStart() => game.player.ReturnToCourse(new Vector3(0, 0.7f, -10), 0);
        private static void Press(params Key[] keys) => held = keys;
        private static void Queue()
        {
            if (keyboard != null && InputState.currentUpdateType == InputUpdateType.Dynamic)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
        }
        private static void Next(int next, float delay) { stage = next; deadline = Time.time + delay; }
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message + " at " + Body.position + " lane=" + game.player.CurrentLane + " velocity=" + Body.linearVelocity + " nearby=" + string.Join(",", Physics.OverlapSphere(Body.position, 0.8f).Select(c => c.name)));
            report.AppendLine("PASS: " + message);
        }
        private static void Finish(int code)
        {
            EditorApplication.update -= Tick;
            InputSystem.onBeforeUpdate -= Queue;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            EditorSettings.enterPlayModeOptionsEnabled = oldEnabled;
            EditorSettings.enterPlayModeOptions = oldOptions;
            Directory.CreateDirectory("output/rollaball");
            File.WriteAllText("output/rollaball/three-lane-validation.txt", report.ToString());
            Debug.Log("THREE_LANE_VALIDATION_EXIT: " + code + "\n" + report);
            EditorApplication.Exit(code);
        }
    }
}
