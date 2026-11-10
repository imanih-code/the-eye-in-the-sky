using EyeSky.Radio;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>Sonidos de la consola, generados por codigo.</summary>
    public sealed class HackAudio : MonoBehaviour
    {
        static readonly int[] ToneSemitones = { 0, 2, 4, 7, 9, 12 };

        AudioSource _effects;
        AudioSource _noise;
        AudioClip[] _tones;
        AudioClip _error;
        AudioClip _success;

        void Awake()
        {
            _effects = gameObject.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _noise = gameObject.AddComponent<AudioSource>();
            _noise.playOnAwake = false;
            _noise.loop = true;
            _noise.volume = 0.22f;
            _noise.clip = ProceduralAudio.Noise("Ruido", 2f);

            _tones = new AudioClip[ToneSemitones.Length];
            for (var i = 0; i < _tones.Length; i++)
                _tones[i] = ProceduralAudio.Tone($"Tono{i}", 440f * Mathf.Pow(2f, ToneSemitones[i] / 12f), 0.38f, 0.5f);
            _error = ProceduralAudio.Tone("Error", 130f, 0.22f, 0.5f);
            _success = ProceduralAudio.Tone("Ok", 880f, 0.12f, 0.35f);
        }

        public void PlayTone(int index) => _effects.PlayOneShot(_tones[index]);

        public void PlayError() => _effects.PlayOneShot(_error);

        public void PlaySuccess() => _effects.PlayOneShot(_success);

        public void SetNoise(bool on)
        {
            if (on && !_noise.isPlaying)
                _noise.Play();
            else if (!on && _noise.isPlaying)
                _noise.Stop();
        }
    }
}
