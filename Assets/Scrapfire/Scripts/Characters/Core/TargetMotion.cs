using UnityEngine;

namespace Scrapfire.Characters.Core
{
    /// <summary>Actual horizontal displacement, independent of input and networking.</summary>
    [DisallowMultipleComponent]
    public sealed class TargetMotion : MonoBehaviour
    {
        public Vector3 Velocity { get; private set; }
        private Vector3 _previous;
        private void OnEnable() { _previous = transform.position; Velocity = Vector3.zero; }
        private void LateUpdate()
        {
            Vector3 delta = transform.position - _previous;
            _previous = transform.position;
            delta.y = 0f;
            Velocity = Time.deltaTime > 0.0001f && delta.sqrMagnitude < 4f
                ? delta / Time.deltaTime : Vector3.zero;
        }
    }
}
