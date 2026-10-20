using UnityEngine;

namespace Ryan.RollABall
{
    public sealed class CollectibleCube : MonoBehaviour
    {
        public RollABallGame game;
        private bool collected;

        private void Update()
        {
            transform.Rotate(0, 45f * Time.deltaTime, 0, Space.World);
        }

        public void Collect()
        {
            if (collected || game == null || game.HasWon) return;
            collected = true;
            game.Collect(this);
            gameObject.SetActive(false);
        }
    }
}
