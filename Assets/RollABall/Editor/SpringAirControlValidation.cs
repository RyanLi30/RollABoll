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
    public static class SpringAirControlValidation
    {
        static int stage, level = 1, scenario;
        static float deadline;
        static double timeout;
        static bool oldEnabled;
        static EnterPlayModeOptions oldOptions;
        static InputSettings.BackgroundBehavior oldBackground;
        static Keyboard keyboard;
        static Key[] held = Array.Empty<Key>();
        static RollABallGame game;
        static Vector3 landing;
        static int bounces, falls;
        static bool preheld;
        static readonly System.Text.StringBuilder report = new System.Text.StringBuilder();
        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
            EditorSceneManager.OpenScene(PrototypeBuilder.LevelPaths[0]);
            oldEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            oldOptions = EditorSettings.enterPlayModeOptions;
            oldBackground = InputSystem.settings.backgroundBehavior;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var view = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            view.position = new Rect(0, 0, 1280, 760); view.Show(); view.Focus();
            EditorApplication.update += Tick;
            InputSystem.onBeforeUpdate += Queue;
            timeout = EditorApplication.timeSinceStartup + 180;
            EditorApplication.EnterPlaymode();
        }
        static Rigidbody Body => game.player.GetComponent<Rigidbody>();
        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > timeout) throw new Exception("Timeout stage " + stage);
                if (!EditorApplication.isPlaying || Time.time < deadline) return;
                game = UnityEngine.Object.FindFirstObjectByType<RollABallGame>();
                if (game == null) return;
                if (stage == 0)
                {
                    if (keyboard == null) keyboard = InputSystem.AddDevice<Keyboard>();
                    if (scenario == 3 && !preheld)
                    {
                        game.player.ReturnToCourse(new Vector3(0, 3, -10), 0);
                        held = new[] { Key.W, Key.D }; Next(8, .18f); return;
                    }
                    // Use the actual board and actual destination in each scene.
                    var board = UnityEngine.Object.FindObjectsByType<SpringBoard>(FindObjectsSortMode.None)
                        .Where(s => Mathf.Abs(s.transform.position.x) < 0.1f).OrderBy(s => s.transform.position.z).First();
                    landing = board.destination;
                    bounces = game.player.Bounces; falls = game.player.Falls;
                    game.player.ReturnToCourse(board.transform.position + Vector3.up * .53f, 0);
                    held = scenario == 3 ? new[] { Key.W, Key.D } : new[] { Key.W }; Next(1, .15f);
                }
                else if (stage == 8) { preheld = true; Next(0, .02f); }
                else if (stage == 1)
                {
                    Check(game.player.Bounces == bounces + 1 && game.player.SpringInFlight, "Real board launches");
                    // Baseline, right correction, then right-to-left reversal.
                    held = scenario == 0 ? new[] { Key.W } : new[] { Key.W, Key.D };
                    Next(2, .65f);
                }
                else if (stage == 2)
                {
                    Check(game.player.SpringInFlight && Body.position.y > landing.y + 1, "Still airborne during steering");
                    Check(Mathf.Abs(Body.position.x - (scenario == 0 ? 0 : 4)) < .3f, "Airborne lateral input reaches requested lane");
                    if (scenario == 2) { held = new[] { Key.W, Key.A }; Next(3, .15f); }
                    else { held = scenario == 0 ? new[] { Key.W } : Array.Empty<Key>(); Next(6, .02f); }
                }
                else if (stage == 3) { held = new[] { Key.W }; Next(4, .12f); }
                else if (stage == 4) { held = new[] { Key.W, Key.LeftArrow }; Next(5, .75f); }
                else if (stage == 5)
                {
                    Check(game.player.SpringInFlight && Body.position.x < -3.7f, "Can reverse direction in air using A and arrow input");
                    Next(6, .02f);
                }
                else if (stage == 6)
                {
                    if (game.player.SpringInFlight) return;
                    held = Array.Empty<Key>(); Next(7, .12f);
                }
                else if (stage == 7)
                {
                    float expectedX = scenario == 0 ? 0 : scenario == 2 ? -4 : 4;
                    Check(Mathf.Abs(Body.position.x - expectedX) < .3f && Mathf.Abs(Body.position.z - landing.z) < 1 && Mathf.Abs(Body.position.y - landing.y) < .3f && game.player.Falls == falls,
                        "Safe landing after " + (scenario == 0 ? "no input" : scenario == 1 ? "right lane correction and release" : scenario == 2 ? "air reversal" : "D held before board contact") + "; position=" + Body.position);
                    scenario++;
                    if (scenario < 4) Next(0, .1f);
                    else if (level < 3)
                    {
                        level++; scenario = 0; preheld = false;
                        SceneManager.LoadScene(PrototypeBuilder.LevelPaths[level - 1]); Next(0, .3f);
                    }
                    else Finish(0);
                }
            }
            catch (Exception e) { report.AppendLine("FAIL: " + e); Debug.LogException(e); Finish(1); }
        }
        static void Queue()
        {
            if (keyboard != null && InputState.currentUpdateType == InputUpdateType.Dynamic)
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(held));
        }
        static void Next(int next, float delay) { stage = next; deadline = Time.time + delay; }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception(message + " at " + Body.position + " velocity=" + Body.linearVelocity);
            report.AppendLine("PASS: Level " + level + " scenario " + scenario + ": " + message);
        }
        static void Finish(int code)
        {
            EditorApplication.update -= Tick; InputSystem.onBeforeUpdate -= Queue;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.backgroundBehavior = oldBackground;
            EditorSettings.enterPlayModeOptionsEnabled = oldEnabled; EditorSettings.enterPlayModeOptions = oldOptions;
            Directory.CreateDirectory("output/rollaball");
            File.WriteAllText("output/rollaball/spring-air-control-validation.txt", report.ToString());
            Debug.Log("SPRING_AIR_CONTROL_EXIT: " + code + "\n" + report);
            EditorApplication.Exit(code);
        }
    }
}
