using UnityEngine;

namespace EyeSky.Radio
{
    /// <summary>
    /// Hace sonar los canales con filtro de radio. La banda siempre se oye;
    /// del escaner solo lo sintonizado, y mas bajo si la banda esta hablando.
    /// </summary>
    public sealed class RadioAudio : MonoBehaviour
    {
        const float VoiceVolume = 0.9f;
        const float StaticVolume = 0.045f;
        const float ClickVolume = 0.16f;
        const float ScannerDucked = 0.4f;

        sealed class Output
        {
            public AudioSource Voice;
            public AudioSource Static;
            public AudioSource Effects;
            public bool WasTalking;
        }

        public RadioBus bus;

        readonly Output[] _outputs = new Output[3];
        AudioClip _click;

        void Awake()
        {
            _click = ProceduralAudio.Click("MicClick");
            var noise = ProceduralAudio.Noise("RadioStatic", 2f);
            for (var i = 0; i < _outputs.Length; i++)
                _outputs[i] = CreateOutput((RadioChannel)i, noise);
            bus.LineStarted += OnLineStarted;
        }

        Output CreateOutput(RadioChannel channel, AudioClip noise)
        {
            var child = new GameObject($"Canal_{channel}");
            child.transform.SetParent(transform, false);

            var output = new Output
            {
                Voice = child.AddComponent<AudioSource>(),
                Static = child.AddComponent<AudioSource>(),
                Effects = child.AddComponent<AudioSource>(),
            };
            output.Voice.playOnAwake = false;
            output.Effects.playOnAwake = false;
            output.Static.playOnAwake = false;
            output.Static.loop = true;
            output.Static.clip = noise;
            output.Static.volume = StaticVolume;

            // El sonido de un transmisor: sin graves, sin agudos y algo saturado
            child.AddComponent<AudioHighPassFilter>().cutoffFrequency = 450f;
            child.AddComponent<AudioLowPassFilter>().cutoffFrequency = 3300f;
            child.AddComponent<AudioDistortionFilter>().distortionLevel = 0.5f;
            return output;
        }

        void OnLineStarted(RadioChannel channel, RadioBus.Line line)
        {
            var output = _outputs[(int)channel];
            output.Effects.PlayOneShot(_click, ClickVolume);
            if (line.Clip != null)
            {
                output.Voice.clip = line.Clip;
                output.Voice.pitch = line.Pitch;
                output.Voice.Play();
            }
        }

        void Update()
        {
            var crewTalking = bus.Current(RadioChannel.Crew) != null;
            for (var i = 0; i < _outputs.Length; i++)
            {
                var channel = (RadioChannel)i;
                var output = _outputs[i];
                var talking = bus.Current(channel) != null;

                if (talking && !output.Static.isPlaying)
                    output.Static.Play();
                else if (!talking && output.Static.isPlaying)
                    output.Static.Stop();

                if (output.WasTalking && !talking)
                    output.Effects.PlayOneShot(_click, ClickVolume);
                output.WasTalking = talking;

                var audible = channel == RadioChannel.Crew || channel == bus.Tuned;
                var level = channel != RadioChannel.Crew && crewTalking ? ScannerDucked : 1f;
                output.Voice.mute = !audible;
                output.Static.mute = !audible;
                output.Effects.mute = !audible;
                output.Voice.volume = VoiceVolume * level;
            }
        }
    }
}
