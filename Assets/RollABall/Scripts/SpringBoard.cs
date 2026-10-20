using UnityEngine;

namespace Ryan.RollABall
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SpringBoard : MonoBehaviour
    {
        public Vector3 destination;
        public float upwardSpeed = 10f;
        public Transform plate;
        private float activated = -10f;
        private Vector3 originalScale;
        private AudioSource sound;
        private AudioClip bounceSound;

        private void Awake()
        {
            if (plate != null) originalScale = plate.localScale;
            sound = gameObject.AddComponent<AudioSource>();
            sound.spatialBlend = 0;
            const int rate = 22050;
            var samples = new float[rate / 4];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate;
                samples[i] = Mathf.Sin(2 * Mathf.PI * (220 * t + 1000 * t * t)) * Mathf.Exp(-t * 16) * 0.2f;
            }
            bounceSound = AudioClip.Create("Spring boing", samples.Length, 1, rate, false);
            bounceSound.SetData(samples, 0);
        }

        private void OnTriggerEnter(Collider other) => Touch(other);
        private void OnTriggerStay(Collider other) => Touch(other);
        private void Touch(Collider other)
        {
            if (!isActiveAndEnabled) return;
            if (Time.time - activated < 0.4f || other.attachedRigidbody == null) return;
            var ball = other.attachedRigidbody.GetComponent<BallController>();
            if (ball == null || other.attachedRigidbody.position.y < transform.position.y + 0.35f) return;
            if (!ball.LaunchFromSpring(destination, upwardSpeed)) return;
            activated = Time.time;
            sound.PlayOneShot(bounceSound, 0.7f);
        }

        private void Update()
        {
            if (plate == null) return;
            float elapsed = Time.time - activated;
            float compression = elapsed < 0.35f ? 1f - 0.55f * Mathf.Sin(elapsed / 0.35f * Mathf.PI) : 1f;
            plate.localScale = new Vector3(originalScale.x, originalScale.y * compression, originalScale.z);
        }

        private void OnDestroy()
        {
            if (bounceSound != null) Destroy(bounceSound);
        }
    }
}
