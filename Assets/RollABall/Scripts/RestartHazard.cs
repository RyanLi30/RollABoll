using UnityEngine;

namespace Ryan.RollABall
{
    // Attach to a hazard collider. Both solid and trigger colliders are supported.
    [RequireComponent(typeof(Collider))]
    public sealed class RestartHazard : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision) => Hit(collision.collider);
        private void OnTriggerEnter(Collider other) => Hit(other);

        private static void Hit(Collider other)
        {
            var ball = other.attachedRigidbody != null
                ? other.attachedRigidbody.GetComponent<BallController>()
                : other.GetComponent<BallController>();
            if (ball == null || !ball.CanMove) return;
            var game = Object.FindFirstObjectByType<RollABallGame>();
            if (game != null && !game.HasWon) game.Restart();
        }
    }
}
