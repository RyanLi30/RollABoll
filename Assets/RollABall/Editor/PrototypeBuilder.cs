using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ryan.RollABall.Editor
{
    public static class PrototypeBuilder
    {
        public const string ScenePath = "Assets/Scenes/RollABallPrototype.unity";
        public static readonly string[] LevelPaths = { ScenePath, "Assets/Scenes/Level02.unity", "Assets/Scenes/Level03.unity" };

        public static void RegisterLevels()
        {
            var builds = EditorBuildSettings.scenes.Where(s => !LevelPaths.Contains(s.path)).ToList();
            builds.InsertRange(0, LevelPaths.Select(p => new EditorBuildSettingsScene(p, true)));
            EditorBuildSettings.scenes = builds.ToArray();
        }
        [MenuItem("Roll-a-Ball/Open prototype")]
        public static void Open()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(ScenePath);
            RegisterLevels();
        }
        [MenuItem("Roll-a-Ball/Create or rebuild auto-run course")]
        public static void Create() => AutoRunCourseBuilder.Create();
        public static void UpgradeRollingSky() => Create();
        public static void UpgradeHazards() => Create();
    }
}
