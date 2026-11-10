using UnityEngine;

namespace EyeSky.Radio
{
    /// <summary>Sonidos sinteticos sencillos, para no depender de archivos.</summary>
    public static class ProceduralAudio
    {
        public const int SampleRate = 44100;

        public static AudioClip Tone(string clipName, float frequency, float seconds, float volume)
        {
            var count = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[count];
            for (var i = 0; i < count; i++)
            {
                var time = i / (float)SampleRate;
                var envelope = Mathf.Min(1f, time * 80f) * Mathf.Min(1f, (seconds - time) * 30f);
                data[i] = Mathf.Sin(time * frequency * Mathf.PI * 2f) * envelope * volume;
            }
            return Make(clipName, data);
        }

        public static AudioClip Noise(string clipName, float seconds)
        {
            var random = new System.Random(7);
            var data = new float[Mathf.RoundToInt(SampleRate * seconds)];
            for (var i = 0; i < data.Length; i++)
                data[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return Make(clipName, data);
        }

        /// <summary>El chasquido de abrir y cerrar el microfono.</summary>
        public static AudioClip Click(string clipName)
        {
            var random = new System.Random(3);
            var data = new float[Mathf.RoundToInt(SampleRate * 0.035f)];
            for (var i = 0; i < data.Length; i++)
                data[i] = (float)(random.NextDouble() * 2.0 - 1.0) * (1f - i / (float)data.Length);
            return Make(clipName, data);
        }

        /// <summary>Sirena de dos tonos que sube y baja, en bucle.</summary>
        public static AudioClip Siren(string clipName)
        {
            const float seconds = 3.2f;
            var data = new float[Mathf.RoundToInt(SampleRate * seconds)];
            var phase = 0f;
            for (var i = 0; i < data.Length; i++)
            {
                var sweep = 0.5f - 0.5f * Mathf.Cos(i / (float)data.Length * Mathf.PI * 2f);
                phase += Mathf.Lerp(620f, 1250f, sweep) / SampleRate;
                data[i] = Mathf.Sin(phase * Mathf.PI * 2f);
            }
            return Make(clipName, data);
        }

        static AudioClip Make(string clipName, float[] data)
        {
            var clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
