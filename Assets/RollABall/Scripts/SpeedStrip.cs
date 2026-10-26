using UnityEngine;

namespace Ryan.RollABall
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SpeedStrip : MonoBehaviour
    {
        public float multiplier = 1.35f;
        public float duration = 4f;
        private BallController occupant;

        private void OnTriggerEnter(Collider other) => Touch(other);
        private void OnTriggerStay(Collider other) => Touch(other);
        private void Touch(Collider other)
        {
            if (!isActiveAndEnabled || other.attachedRigidbody == null) return;
            var ball = other.attachedRigidbody.GetComponent<BallController>();
            if (ball == null || ball == occupant || !ball.CanMove || ball.SpringInFlight) return;
            // Thin floor trigger: jumping over a strip is a valid way to avoid it.
            occupant = ball;
            ball.ApplySpeedStrip(multiplier, duration);
        }

        private void OnTriggerExit(Collider other)
        {
            if (occupant != null && other.attachedRigidbody == occupant.GetComponent<Rigidbody>()) occupant = null;
        }
    }
}
