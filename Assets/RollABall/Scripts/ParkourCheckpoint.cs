using UnityEngine;

namespace Ryan.RollABall
{
    public sealed class ParkourCheckpoint : MonoBehaviour
    {
        public Vector3 respawnPosition;
        public int number;
        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<BallController>(out var ball)) ball.SetCheckpoint(respawnPosition, number);
        }
    }
}
