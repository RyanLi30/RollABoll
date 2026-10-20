using UnityEngine;

namespace Ryan.RollABall
{
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0, 4.5f, -8);
        public float lookAhead = 10f;
        private void Start()
        {
            if (target != null) transform.position = target.position + offset;
        }
        private void LateUpdate()
        {
            if (target == null) return;
            if (Vector3.Distance(transform.position, target.position + offset) > 20f)
                transform.position = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, target.position + offset,
                1f - Mathf.Exp(-7f * Time.deltaTime));
            transform.LookAt(target.position + Vector3.forward * lookAhead + Vector3.up * 0.5f);
        }
    }
}
