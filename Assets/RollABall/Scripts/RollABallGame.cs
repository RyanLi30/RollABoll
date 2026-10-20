using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ryan.RollABall
{
    public sealed class RollABallGame : MonoBehaviour
    {
        public BallController player;
        public CollectibleCube[] cubes;
        public Material particleMaterial;
        public int levelNumber = 1;
        public int levelCount = 3;
        public string levelTitle = "First Steps";
        public string nextScenePath;
        public string firstScenePath = "Assets/RollABall/Scenes/RollABallPrototype.unity";
        public int Remaining { get; private set; }
        public bool HasWon { get; private set; }
        public float Elapsed { get; private set; }
        private AudioSource audioSource;
        private AudioClip pickupSound;
        private AudioClip victorySound;
        private float feedbackUntil;
        private bool restarting;
        private string notice;
        private float noticeUntil;
        public void ShowNotice(string message) { notice = message; noticeUntil = Time.time + 4; }
        private GUIStyle title, subtitle, counter, body, button;

        private void Awake()
        {
            Remaining = cubes.Length;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0;
            pickupSound = MakeTone("Cube pickup", false);
            victorySound = MakeTone("Victory", true);
        }

        private void Update()
        {
            if (!HasWon) Elapsed += Time.deltaTime;
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) Restart();
            if (HasWon && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame) Advance();
        }

        public void Collect(CollectibleCube cube)
        {
            Remaining--;
            feedbackUntil = Time.unscaledTime + 0.65f;
            Burst(cube.transform.position);
            if (Remaining == 0)
            {
                HasWon = true;
                player.Stop();
                audioSource.PlayOneShot(victorySound, 0.35f);
            }
            else audioSource.PlayOneShot(pickupSound, 0.3f);
        }

        public void Restart()
        {
            if (restarting) return;
            restarting = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
        public void Advance()
        {
            if (!HasWon) return;
            SceneManager.LoadScene(string.IsNullOrEmpty(nextScenePath) ? firstScenePath : nextScenePath);
        }

        private void Burst(Vector3 position)
        {
            var effect = new GameObject("Pickup burst");
            effect.transform.position = position;
            var particles = effect.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.duration = 0.4f;
            main.startLifetime = 0.55f;
            main.startSpeed = 3f;
            main.startSize = 0.14f;
            main.startColor = new Color(0.15f, 0.65f, 1f);
            main.gravityModifier = 0.4f;
            var emission = particles.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 22) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
            particles.Play();
            Destroy(effect, 1.5f);
        }

        private static AudioClip MakeTone(string name, bool victory)
        {
            const int rate = 22050;
            float duration = victory ? 0.8f : 0.13f;
            var samples = new float[Mathf.CeilToInt(rate * duration)];
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            for (int i = 0; i < samples.Length; i++)
            {
                float time = (float)i / rate;
                float frequency = victory ? notes[Mathf.Min(3, (int)(time / 0.2f))] : 880f;
                float localTime = victory ? time % 0.2f : time;
                float envelope = Mathf.Min(1, localTime / 0.008f) * Mathf.Exp(-localTime * (victory ? 12 : 30));
                samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * time) * envelope * 0.5f;
            }
            var clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnGUI()
        {
            if (title == null)
            {
                title = Style(28, FontStyle.Bold, Color.white);
                subtitle = Style(12, FontStyle.Bold, new Color(0.35f, 0.75f, 1f));
                counter = Style(22, FontStyle.Bold, Color.white);
                body = Style(15, FontStyle.Normal, new Color(0.82f, 0.87f, 0.92f));
                button = new GUIStyle(GUI.skin.button) { fontSize = 16, fontStyle = FontStyle.Bold };
            }
            float scale = Mathf.Max(0.35f, Mathf.Min(Screen.width / 1000f, Screen.height / 650f));
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            Panel(new Rect(22, 22, 330, 150));
            GUI.Label(new Rect(42, 35, 300, 22), "RYAN LI  /  GAMEPLAY PROTOTYPE", subtitle);
            GUI.Label(new Rect(42, 59, 300, 40), "ROLL-A-BALL 3D", title);
            GUI.Label(new Rect(42, 103, 300, 24), $"LEVEL {levelNumber}/{levelCount}  -  {levelTitle}", body);
            GUI.Label(new Rect(42, 140, 300, 22), "PURPLE = LONG JUMP  /  RED = RESTART", subtitle);
            Panel(new Rect(width - 248, 22, 226, 115));
            GUI.Label(new Rect(width - 228, 37, 190, 22), "CUBES REMAINING", subtitle);
            GUI.Label(new Rect(width - 228, 63, 190, 30), $"{Remaining:00} / {cubes.Length:00}", counter);
            GUI.Label(new Rect(width - 228, 104, 190, 24), $"SPEED {player.MaximumSpeed:0}  /  FALLS {player.Falls}", body);
            Panel(new Rect(22, height - 66, width - 44, 44));
            GUI.Label(new Rect(40, height - 56, width - 220, 25), "W / S  Forward / Back    A / D  Change lane    Space  Jump    R  Retry", body);
            if (GUI.Button(new Rect(width - 163, height - 58, 130, 29), "Restart [R]", button)) Restart();
            Panel(new Rect(22, 184, 330, 37));
            GUI.Label(new Rect(42, 191, 300, 25), $"CHECKPOINT {player.CheckpointNumber}  /  BOUNCES {player.Bounces}", body);
            if (Time.time < noticeUntil)
            {
                Panel(new Rect(width / 2 - 240, height - 115, 480, 36));
                GUI.Label(new Rect(width / 2 - 225, height - 110, 460, 30), notice, body);
            }
            if (!HasWon && Time.unscaledTime < feedbackUntil)
                GUI.Label(new Rect(width / 2 - 90, 35, 200, 40), "+1 COLLECTED", counter);
            if (!HasWon) return;
            var panel = new Rect(width / 2 - 220, height / 2 - 125, 440, 250);
            Panel(panel);
            var centerTitle = new GUIStyle(title) { alignment = TextAnchor.MiddleCenter, fontSize = 40 };
            var centerBody = new GUIStyle(body) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(panel.x, panel.y + 25, 440, 60),
                levelNumber == levelCount ? "You Win!" : "Level Complete!", centerTitle);
            GUI.Label(new Rect(panel.x, panel.y + 95, 440, 50),
                $"All {cubes.Length} cubes collected in {Elapsed:0.0} seconds.\n"
                + (levelNumber == levelCount ? "All levels complete!" : "Ready for a harder challenge?"), centerBody);
            if (GUI.Button(new Rect(panel.x + 60, panel.y + 173, 320, 45),
                levelNumber == levelCount ? "Play from Level 1 [Enter]" : "Next level [Enter]", button)) Advance();
        }

        private static GUIStyle Style(int size, FontStyle weight, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight };
            style.normal.textColor = color;
            return style;
        }
        private static void Panel(Rect rect)
        {
            var old = GUI.color;
            GUI.color = new Color(0.045f, 0.065f, 0.09f, 0.94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
        private void OnDestroy()
        {
            if (pickupSound != null) Destroy(pickupSound);
            if (victorySound != null) Destroy(victorySound);
        }
    }
}
