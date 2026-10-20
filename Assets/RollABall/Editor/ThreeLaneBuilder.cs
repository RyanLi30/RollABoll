using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ryan.RollABall.Editor
{
    public static class ThreeLaneBuilder
    {
        private static Material road, laneA, laneB, cyan, white, red, black, yellow, purple, blue, green, support, leaves;
        public static void Create()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            road = Mat("Track foundation", new Color(0.12f, 0.17f, 0.23f));
            laneA = Mat("Lane slate", new Color(0.24f, 0.32f, 0.39f));
            laneB = Mat("Lane center", new Color(0.3f, 0.38f, 0.45f));
            cyan = Mat("Track cyan", new Color(0.12f, 0.8f, 1));
            white = Mat("Barrier white", new Color(0.98f, 0.96f, 0.86f));
            red = Mat("Barrier red", new Color(0.88f, 0.045f, 0.065f));
            black = Mat("Barrier black", new Color(0.035f, 0.045f, 0.055f));
            yellow = Mat("Player yellow", new Color(1, 0.78f, 0.07f));
            purple = Mat("Spring magenta", new Color(0.9f, 0.08f, 1));
            blue = Mat("Collectible blue", new Color(0.025f, 0.48f, 1));
            green = Mat("Checkpoint green", new Color(0.1f, 0.88f, 0.48f));
            support = Mat("Supports", new Color(0.19f, 0.25f, 0.3f));
            leaves = Mat("Scenery teal", new Color(0.08f, 0.48f, 0.38f));
            for (int level = 1; level <= 3; level++) Build(level);
            PrototypeBuilder.RegisterLevels();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(PrototypeBuilder.ScenePath);
            Debug.Log("THREE_LANE_COURSE_CREATED");
        }

        private static void Build(int level)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.67f, 0.75f, 0.85f);
            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.48f, 0.68f, 0.78f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 85;
            RenderSettings.fogEndDistance = 240;
            var world = new GameObject("Three-lane sky railway").transform;
            var platforms = new GameObject("Landing platforms").transform;
            platforms.SetParent(world);
            var hazards = new GameObject("Danger barriers - red and white").transform;
            hazards.SetParent(world);
            var springs = new GameObject("Springboards - long gaps only").transform;
            springs.SetParent(world);
            var pickups = new GameObject("Blue cubes - follow the safe lane").transform;
            var ballObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ballObject.name = "Player - three-lane ball";
            ballObject.transform.position = new Vector3(0, 0.7f, -10);
            ballObject.transform.localScale = Vector3.one * 1.3f;
            ballObject.GetComponent<Renderer>().sharedMaterial = yellow;
            var body = ballObject.AddComponent<Rigidbody>();
            body.mass = 1;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.maxAngularVelocity = 40;
            var player = ballObject.AddComponent<BallController>();
            player.ConfigureThreeLanes(12 + level * 2);
            var game = new GameObject("Game - three lane progression").AddComponent<RollABallGame>();
            game.player = player;
            game.levelNumber = level;
            game.levelTitle = new[] { "Three Lane Express", "Switch and Launch", "Skyline Rush" }[level - 1];
            game.nextScenePath = level < 3 ? PrototypeBuilder.LevelPaths[level] : "";
            game.particleMaterial = blue;
            var sections = new List<CourseSection>();
            var cubes = new List<CollectibleCube>();
            int count = 5 + level;
            float z = 0;
            for (int i = 0; i < count; i++)
            {
                float top = i * 0.1f * level;
                var deck = Box($"Section {i:00} - three lanes", new Vector3(0, top - 0.75f, z), new Vector3(12, 1.5f, 32), road, platforms);
                var section = deck.AddComponent<CourseSection>();
                section.number = i;
                section.requiresSpring = i < count - 1 && i % 2 == 0;
                section.gapAfter = i == count - 1 ? 0 : section.requiresSpring ? 18 + 4 * level : 3 + level;
                section.entry = new Vector3(0, top + 0.7f, z - 11);
                sections.Add(section);
                for (int lane = -1; lane <= 1; lane++)
                {
                    Box("Lane " + (lane + 2), new Vector3(lane * 4, top + 0.015f, z), new Vector3(3.92f, 0.025f, 31.9f),
                        lane == 0 ? laneB : laneA, deck.transform, false);
                    for (float dash = -14; dash <= 14; dash += 4)
                        Box("Lane direction", new Vector3(lane * 4, top + 0.035f, z + dash),
                            new Vector3(0.08f, 0.025f, 1), cyan, deck.transform, false);
                }
                for (int side = -1; side <= 1; side += 2)
                {
                    Box("Edge guide", new Vector3(side * 6.15f, top + 0.2f, z), new Vector3(0.25f, 0.4f, 32), cyan, deck.transform);
                    Box("Elevated support", new Vector3(side * 4.8f, top - 5, z), new Vector3(0.65f, 9, 1.2f), support, world);
                    for (int p = -1; p <= 1; p++)
                    {
                        Box("Guide post", new Vector3(side * 6.9f, top + 1.5f, z + p * 10), new Vector3(0.2f, 3, 0.2f), support, world, false);
                        Box("Guide light", new Vector3(side * 6.9f, top + 3, z + p * 10), new Vector3(0.4f, 0.18f, 0.4f), cyan, world, false);
                    }
                    var tree = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    tree.name = "Distant scenery tower";
                    tree.transform.SetParent(world);
                    tree.transform.position = new Vector3(side * 15, top - 1, z + 7);
                    tree.transform.localScale = new Vector3(1.5f, 4, 1.5f);
                    tree.GetComponent<Renderer>().sharedMaterial = support;
                    Object.DestroyImmediate(tree.GetComponent<Collider>());
                    var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    crown.name = "Low poly canopy";
                    crown.transform.SetParent(world);
                    crown.transform.position = new Vector3(side * 15, top + 5, z + 7);
                    crown.transform.localScale = new Vector3(7, 9, 7);
                    crown.GetComponent<Renderer>().sharedMaterial = leaves;
                    Object.DestroyImmediate(crown.GetComponent<Collider>());
                }
                // Every row has at least one fully open lane; the cubes mark that lane.
                int safeLane = (i + level) % 3 - 1;
                for (int lane = -1; lane <= 1; lane++)
                {
                    if (lane == safeLane || (i == 0 && lane == 1)) continue;
                    Barrier($"Warning barrier {i:00} lane {lane}", new Vector3(lane * 4, top, z + 1), hazards);
                }
                if (i > 0 && i % 2 == 1)
                {
                    var moving = Barrier($"Moving warning barrier {i:00}", new Vector3(0, top, z - 4), hazards).AddComponent<MovingObstacle>();
                    moving.travel = Vector3.right * 8;
                    moving.period = 5.4f - level * 0.7f;
                    moving.rotationSpeed = level > 1 && i % 3 == 0 ? 35 + level * 15 : 0;
                    moving.GetComponent<Rigidbody>().isKinematic = true;
                }
                AddCube(new Vector3(safeLane * 4, top + 0.85f, z + 6), game, cubes, pickups);
                if (i < level + 1) AddCube(new Vector3(safeLane * 4, top + 0.85f, z - 2), game, cubes, pickups);
                if (i > 0)
                {
                    var checkpoint = new GameObject("Checkpoint section " + i);
                    checkpoint.transform.SetParent(world);
                    checkpoint.transform.position = new Vector3(0, top + 1, z - 8);
                    var trigger = checkpoint.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(12, 2, 0.8f);
                    var point = checkpoint.AddComponent<ParkourCheckpoint>();
                    point.number = i;
                    point.respawnPosition = section.entry;
                    Box("Checkpoint line", new Vector3(0, top + 0.04f, z - 8), new Vector3(12, 0.025f, 0.2f), green, world, false);
                }
                if (i < count - 1)
                {
                    Material edge = section.requiresSpring ? purple : yellow;
                    Box(section.requiresSpring ? "SPRING REQUIRED - long gap" : "JUMP - short gap",
                        new Vector3(0, top + 0.04f, z + 15.7f), new Vector3(12, 0.04f, 0.45f), edge, world, false);
                }
                z += 32 + section.gapAfter;
            }
            for (int i = 0; i < sections.Count - 1; i++)
            {
                var section = sections[i];
                if (!section.requiresSpring) continue;
                float top = section.transform.position.y + 0.75f;
                var next = sections[i + 1];
                for (int lane = -1; lane <= 1; lane++)
                {
                    var board = new GameObject($"Long-gap spring {i:00} lane {lane}");
                    board.transform.SetParent(springs);
                    board.transform.position = new Vector3(lane * 4, top + 0.12f, section.transform.position.z + 14.2f);
                    var trigger = board.AddComponent<BoxCollider>();
                    trigger.isTrigger = true;
                    trigger.size = new Vector3(3.8f, 0.4f, 2.2f);
                    var spring = board.AddComponent<SpringBoard>();
                    spring.upwardSpeed = 10 + level;
                    spring.destination = new Vector3(lane * 4, next.entry.y - 0.05f, next.entry.z);
                    spring.plate = Box("Elastic plate", board.transform.position, new Vector3(3.7f, 0.18f, 2.2f), purple, board.transform, false).transform;
                    for (int arrow = -1; arrow <= 1; arrow++)
                        Box("Launch arrow", board.transform.position + new Vector3(arrow * 0.4f, 0.12f, -Mathf.Abs(arrow) * 0.3f),
                            new Vector3(0.22f, 0.03f, 0.6f), white, board.transform, false);
                }
            }
            game.cubes = cubes.ToArray();
            var end = sections[sections.Count - 1];
            var finish = new GameObject("Finish - recover missed cubes");
            finish.transform.position = new Vector3(0, end.entry.y + 1, end.transform.position.z + 13);
            var finishTrigger = finish.AddComponent<BoxCollider>();
            finishTrigger.isTrigger = true;
            finishTrigger.size = new Vector3(12, 3, 1);
            finish.AddComponent<CourseFinish>();
            for (int side = -1; side <= 1; side += 2)
                Box("Finish post", new Vector3(side * 5.7f, end.entry.y + 2, end.transform.position.z + 13), new Vector3(0.4f, 5.5f, 0.4f), green, world);
            Box("Finish arch", new Vector3(0, end.entry.y + 4.75f, end.transform.position.z + 13), new Vector3(12, 0.5f, 0.4f), green, world);
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 68;
            camera.farClipPlane = 550;
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            var follow = cameraObject.AddComponent<FollowCamera>();
            follow.target = ballObject.transform;
            follow.offset = new Vector3(0, 6.5f, -11);
            camera.transform.position = player.transform.position + follow.offset;
            camera.transform.LookAt(player.transform.position + Vector3.forward * 10);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(45, -35, 0);
            RenderSettings.sun = light;
            EditorSceneManager.SaveScene(scene, PrototypeBuilder.LevelPaths[level - 1]);
        }

        private static GameObject Barrier(string name, Vector3 position, Transform parent)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 1.15f, 0);
            collider.size = new Vector3(2.85f, 2.3f, 0.9f);
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            root.AddComponent<RestartHazard>();
            Box("White warning panel", position + new Vector3(0, 1.1f, 0), new Vector3(2.6f, 1.5f, 0.75f), white, root.transform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                for (int stripe = 0; stripe < 5; stripe++)
                    Box("Striped support", position + new Vector3(side * 1.26f, 0.23f + stripe * 0.4f, 0),
                        new Vector3(0.32f, 0.4f, 0.9f), stripe % 2 == 0 ? red : white, root.transform, false);
                Box("Foot", position + new Vector3(side * 1.15f, 0.08f, 0), new Vector3(0.6f, 0.16f, 1.4f), black, root.transform, false);
            }
            for (int face = -1; face <= 1; face += 2)
                for (int row = 0; row < 2; row++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var stripe = Box("Red chevron", position + new Vector3(side * 0.46f, 0.85f + row * 0.5f, face * 0.4f),
                            new Vector3(1.18f, 0.19f, 0.06f), red, root.transform, false);
                        stripe.transform.rotation = Quaternion.Euler(0, 0, -side * 32);
                    }
            for (int lamp = -1; lamp <= 1; lamp++)
            {
                Box("Lamp housing", position + new Vector3(lamp * 0.8f, 2.02f, 0), new Vector3(0.42f, 0.48f, 0.7f), black, root.transform, false);
                Box("Amber warning lamp", position + new Vector3(lamp * 0.8f, 2.07f, -0.37f), new Vector3(0.25f, 0.27f, 0.08f), yellow, root.transform, false);
            }
            return root;
        }
        private static void AddCube(Vector3 position, RollABallGame game, List<CollectibleCube> cubes, Transform parent)
        {
            var obj = Box("Blue cube " + (cubes.Count + 1).ToString("00"), position, Vector3.one * 0.85f, blue, parent);
            obj.GetComponent<BoxCollider>().isTrigger = true;
            var cube = obj.AddComponent<CollectibleCube>();
            cube.game = game;
            cubes.Add(cube);
        }
        private static GameObject Box(string name, Vector3 position, Vector3 size, Material material, Transform parent, bool solid = true)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = name;
            obj.transform.SetParent(parent);
            obj.transform.position = position;
            var scale = parent != null ? parent.lossyScale : Vector3.one;
            obj.transform.localScale = new Vector3(size.x / scale.x, size.y / scale.y, size.z / scale.z);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(obj.GetComponent<Collider>());
            return obj;
        }
        private static Material Mat(string name, Color color)
        {
            string path = "Assets/RollABall/Materials/" + name.Replace(' ', '_') + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.22f);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
