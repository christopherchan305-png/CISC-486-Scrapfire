using UnityEngine;

namespace Scrapfire.Pooling
{
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        private SharedObjectPool _owner;
        internal GameObject Prefab { get; private set; }
        public bool InUse { get; internal set; }

        internal void Bind(SharedObjectPool owner, GameObject prefab)
        {
            _owner = owner;
            Prefab = prefab;
        }

        public void Release()
        {
            if (_owner != null)
            {
                _owner.Return(this);
            }
        }
    }
}
