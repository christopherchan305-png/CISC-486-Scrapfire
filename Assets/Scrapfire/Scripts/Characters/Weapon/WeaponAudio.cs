using UnityEngine;
using Scrapfire.Audio;
using Scrapfire.Pooling;

namespace Scrapfire.Characters.Weapon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RangedWeapon))]
    public sealed class WeaponAudio : MonoBehaviour
    {
        public AudioClip FireClip;
        public GameObject VoicePrefab;
        [Range(0f, 1f)] public float FireVolume = 0.65f;
        [Min(0)] public int PrewarmCount = 16;

        private RangedWeapon _weapon;
        private SharedObjectPool _pool;

        private void Awake()
        {
            _weapon = GetComponent<RangedWeapon>();
        }

        private void OnEnable()
        {
            _pool = SharedObjectPool.ForScene(gameObject.scene);

            if (VoicePrefab != null)
            {
                _pool.Prewarm(VoicePrefab, PrewarmCount);
            }

            _weapon.Attacked += PlayFire;
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.Attacked -= PlayFire;
            }
        }

        private void PlayFire(Vector3 origin, Vector3 direction)
        {
            if (FireClip == null || VoicePrefab == null)
            {
                return;
            }

            PooledObject instance = _pool.Rent(VoicePrefab, origin, Quaternion.identity);
            instance.GetComponent<PooledAudioVoice>().Play(FireClip, FireVolume);
        }
    }
}
