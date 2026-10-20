using UnityEngine;

namespace Ryan.RollABall
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class MovingObstacle : MonoBehaviour
    {
        public Vector3 travel = Vector3.right;
        public float period = 4f;
        public float rotationSpeed;
        public float phase;
        private Rigidbody body;
        private Vector3 origin;
        private Quaternion originalRotation;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            origin = body.position;
            originalRotation = body.rotation;
        }

        private void FixedUpdate()
        {
            float time = Time.timeSinceLevelLoad;
            // Start at the authored position; phase offsets the rhythm without moving the path.
            float wave = (Mathf.Sin(time * 2f * Mathf.PI / Mathf.Max(0.25f, period) + phase)
                - Mathf.Sin(phase)) * 0.5f;
            body.MovePosition(origin + travel * wave);
            body.MoveRotation(originalRotation * Quaternion.Euler(0, rotationSpeed * time, 0));
        }
    }
}
