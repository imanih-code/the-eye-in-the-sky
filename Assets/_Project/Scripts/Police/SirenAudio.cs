using EyeSky.Radio;
using UnityEngine;

namespace EyeSky.Police
{
    /// <summary>La sirena, oida desde dentro de la camioneta: lejana y apagada.</summary>
    public sealed class SirenAudio : MonoBehaviour
    {
        const float MaxVolume = 0.22f;

        AudioSource _source;

        void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.volume = 0f;
            _source.clip = ProceduralAudio.Siren("Sirena");
            gameObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 1100f;
        }

        public void SetLevel(float level)
        {
            if (level > 0f && !_source.isPlaying)
                _source.Play();
            else if (level <= 0f && _source.isPlaying)
                _source.Stop();
            _source.volume = level * MaxVolume;
        }

        public bool IsAudible => _source != null && _source.isPlaying && _source.volume > 0f;
    }
}
