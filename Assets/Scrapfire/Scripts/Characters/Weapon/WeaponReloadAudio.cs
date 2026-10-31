using UnityEngine;

namespace Scrapfire.Characters.Weapon
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RangedWeapon), typeof(AudioSource))]
    public sealed class WeaponReloadAudio : MonoBehaviour
    {
        public AudioClip MagazineReloadClip;
        public AudioClip SingleRoundReloadClip;
        [Range(0f, 1f)] public float Volume = 0.65f;

        private RangedWeapon _weapon;
        private AudioSource _source;

        private void Awake()
        {
            _weapon = GetComponent<RangedWeapon>();
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
        }

        private void OnEnable()
        {
            _weapon.ReloadStepStarted += PlayStep;
            _weapon.StateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            if (_weapon != null)
            {
                _weapon.ReloadStepStarted -= PlayStep;
                _weapon.StateChanged -= OnStateChanged;
            }

            Stop();
        }

        private void PlayStep()
        {
            AudioClip clip = _weapon.Data.MagazineReloadMode == WeaponData.ReloadMode.SingleRound
                ? SingleRoundReloadClip : MagazineReloadClip;
            Stop();

            if (clip == null)
            {
                return;
            }

            _source.clip = clip;
            _source.volume = Volume;
            _source.Play();
        }

        private void OnStateChanged(WeaponState previous, WeaponState next)
        {
            if (previous == WeaponState.Reloading && next != WeaponState.Reloading)
            {
                Stop();
            }
        }

        private void Stop()
        {
            if (_source != null)
            {
                _source.Stop();
                _source.clip = null;
            }
        }
    }
}
