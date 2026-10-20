using System.Linq;
using UnityEngine;

namespace Ryan.RollABall
{
    public sealed class CourseFinish : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.attachedRigidbody == null || !other.attachedRigidbody.TryGetComponent<BallController>(out var ball)) return;
            var game = Object.FindFirstObjectByType<RollABallGame>();
            if (game == null || game.HasWon) return;
            var missing = game.cubes.Where(c => c.gameObject.activeSelf).OrderBy(c => c.transform.position.z).FirstOrDefault();
            if (missing == null) return;
            var section = Object.FindObjectsByType<CourseSection>(FindObjectsSortMode.None)
                .OrderBy(s => Mathf.Abs(s.transform.position.z - missing.transform.position.z)).First();
            ball.ReturnToCourse(section.entry, section.number);
            game.ShowNotice("Missed cubes: returned to section " + (section.number + 1));
        }
    }
}
