using UnityEngine;

namespace Scrapfire.Characters.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Scrapfire/Player/Top Down Camera Follow")]
    public class TopDownCameraFollow : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset = new Vector3(14.4f, 18f, -18f);
        [Min(0f)] public float FollowSpeed = 12f;

        private void LateUpdate()
        {
            if (Target == null) return;
            Vector3 desired = Target.position + Offset;
            float blend = FollowSpeed <= 0f ? 1f : 1f - Mathf.Exp(-FollowSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, blend);
            // Keep a fixed world orientation; player rotation does not rotate the camera.
            if (Offset.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-Offset, Vector3.up);
        }

        public void SnapToTarget()
        {
            if (Target == null) return;
            transform.position = Target.position + Offset;
            if (Offset.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-Offset, Vector3.up);
        }
    }
}
