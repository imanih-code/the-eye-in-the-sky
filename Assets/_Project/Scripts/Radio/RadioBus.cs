using System;
using System.Collections.Generic;
using UnityEngine;

namespace EyeSky.Radio
{
    public enum RadioChannel
    {
        Crew,
        Security,
        Police,
    }

    /// <summary>Lo que se esta diciendo en cada canal. Una frase a la vez, sin historial.</summary>
    public sealed class RadioBus : MonoBehaviour
    {
        const int MaxQueued = 3;
        const float SecondsBase = 0.9f;
        const float SecondsPerCharacter = 0.055f;
        const float ClipTail = 0.3f;

        [Serializable]
        public sealed class SpeakerVoice
        {
            public string speaker;
            [Range(0.5f, 1.5f)] public float pitch = 1f;
        }

        public sealed class Line
        {
            public string Speaker;
            public string Text;
            public float SecondsLeft;
            public AudioClip Clip;
            public float Pitch = 1f;
        }

        sealed class ChannelState
        {
            public readonly Queue<Line> Queue = new();
            public Line Current;
        }

        public static RadioBus Instance { get; private set; }

        public RadioLineBook book;
        public RadioClipBank clips;
        [Tooltip("Una sola voz sintetica: cada personaje se distingue por el tono.")]
        public List<SpeakerVoice> voices = new();

        readonly ChannelState[] _channels =
        {
            new(), new(), new(),
        };

        /// <summary>Lo que tiene sintonizado el escaner: policia o seguridad, nunca los dos.</summary>
        public RadioChannel Tuned { get; private set; } = RadioChannel.Police;

        /// <summary>Muestra las palabras mientras suenan.</summary>
        public bool ShowTranscript { get; set; } = true;

        /// <summary>Se dispara cuando una frase empieza a sonar.</summary>
        public event Action<RadioChannel, Line> LineStarted;

        void Awake()
        {
            Instance = this;
        }

        void Update()
        {
            for (var i = 0; i < _channels.Length; i++)
            {
                var channel = _channels[i];
                if (channel.Current != null)
                {
                    channel.Current.SecondsLeft -= Time.deltaTime;
                    if (channel.Current.SecondsLeft <= 0f)
                        channel.Current = null;
                }

                if (channel.Current == null && channel.Queue.Count > 0)
                {
                    channel.Current = channel.Queue.Dequeue();
                    LineStarted?.Invoke((RadioChannel)i, channel.Current);
                }
            }
        }

        public Line Current(RadioChannel channel) => _channels[(int)channel].Current;

        public void TuneNext()
        {
            Tuned = Tuned == RadioChannel.Police ? RadioChannel.Security : RadioChannel.Police;
        }

        /// <summary>Dice una frase del libro. Los argumentos rellenan {0}, {1}...</summary>
        public static void Say(RadioChannel channel, string speaker, string key, params object[] args)
        {
            if (Instance == null || Instance.book == null)
                return;
            var template = Instance.book.Pick(key);
            if (template == null)
            {
                Debug.LogWarning($"Falta la frase de radio '{key}'");
                return;
            }
            Instance.Enqueue(channel, speaker, args.Length > 0 ? string.Format(template, args) : template);
        }

        void Enqueue(RadioChannel channel, string speaker, string text)
        {
            var state = _channels[(int)channel];
            // Si se acumulan, se pierde lo mas viejo: la radio no espera a nadie
            while (state.Queue.Count >= MaxQueued)
                state.Queue.Dequeue();

            var voice = voices.Find(v => v.speaker == speaker);
            var pitch = voice?.pitch ?? 1f;
            var clip = clips != null ? clips.ClipFor(text) : null;
            state.Queue.Enqueue(new Line
            {
                Speaker = speaker,
                Text = text,
                Clip = clip,
                Pitch = pitch,
                SecondsLeft = clip != null ? clip.length / pitch + ClipTail : SecondsBase + text.Length * SecondsPerCharacter,
            });
        }
    }
}
