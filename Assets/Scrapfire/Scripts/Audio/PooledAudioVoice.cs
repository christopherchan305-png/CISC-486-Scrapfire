using UnityEngine;
using Scrapfire.Pooling;

namespace Scrapfire.Audio
{
    [RequireComponent(typeof(PooledObject), typeof(AudioSource))]
    public sealed class PooledAudioVoice : MonoBehaviour
    {
        private PooledObject _lease;
        private AudioSource _source;
        private bool _started;

        private void Awake()
        {
            _lease = GetComponent<PooledObject>();
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
        }

        public void Play(AudioClip clip, float volume)
        {
            if (clip == null)
            {
                _lease.Release();
                return;
            }

            _source.clip = clip;
            _source.volume = Mathf.Clamp01(volume);
            _source.pitch = 1f;
            _source.Play();
            _started = true;
        }

        private void Update()
        {
            if (_started && !_source.isPlaying)
            {
                _lease.Release();
            }
        }

        private void OnDisable()
        {
            _started = false;

            if (_source != null)
            {
                _source.Stop();
                _source.clip = null;
            }
        }
    }
}
