using System;
using System.Collections.Generic;
using EyeSky.UI;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>
    /// Suplantar a la central: suena una secuencia de tonos entre ruido, una sola vez,
    /// y hay que repetirla en el teclado. Se resuelve de oido.
    /// </summary>
    public sealed class InterferenciaPuzzle : IHackPuzzle
    {
        public const int Tones = 6;
        const float GapSeconds = 0.14f;
        const float ReferenceSeconds = 0.26f;
        const float PauseBeforeSequence = 0.9f;
        const int KeySize = 70;

        enum Phase
        {
            Reference,
            Pause,
            Listening,
            Answer,
        }

        readonly HackSettings.Interferencia _tuning;
        readonly int[] _sequence;
        readonly List<int> _answer = new();
        Phase _phase = Phase.Reference;
        int _played = -1;
        float _clock;

        /// <summary>Hay que hacer sonar este tono.</summary>
        public event Action<int> ToneRequested;

        public bool IsListening => _phase != Phase.Answer;
        public bool WaitingForAnswer => _phase == Phase.Answer;
        public string Title => "INTERFERIR CENTRAL";
        public string Hint => _phase == Phase.Answer ? "REPITE LA SECUENCIA CON LAS TECLAS 1 A 6" : "ESCUCHA. SOLO SONARA UNA VEZ";

        public InterferenciaPuzzle(HackSettings.Interferencia tuning, int seed)
        {
            _tuning = tuning;
            var random = new System.Random(seed);
            _sequence = new int[random.Next(tuning.tonesMin, tuning.tonesMax + 1)];
            for (var i = 0; i < _sequence.Length; i++)
                _sequence[i] = random.Next(Tones);
        }

        RectInt Key(RectInt area, int index) => new(area.x + 180 + index * (KeySize + 6), area.y + 230, KeySize, KeySize);

        public HackStep Tick(float deltaTime, in HackInput input, RectInt area)
        {
            switch (_phase)
            {
                case Phase.Reference:
                    Advance(deltaTime, ReferenceSeconds, Tones, i => i, () => SetPhase(Phase.Pause));
                    return HackStep.Running;
                case Phase.Pause:
                    _clock += deltaTime;
                    if (_clock >= PauseBeforeSequence)
                        SetPhase(Phase.Listening);
                    return HackStep.Running;
                case Phase.Listening:
                    Advance(deltaTime, _tuning.toneSeconds + GapSeconds, _sequence.Length, i => _sequence[i], () => SetPhase(Phase.Answer));
                    return HackStep.Running;
            }

            var pressed = -1;
            if (input.Typed >= '1' && input.Typed <= '6')
                pressed = input.Typed - '1';
            if (input.Click)
                for (var i = 0; i < Tones; i++)
                    if (Key(area, i).Contains(input.Pointer))
                        pressed = i;

            if (pressed < 0)
                return HackStep.Running;

            ToneRequested?.Invoke(pressed);
            _answer.Add(pressed);
            if (_answer[^1] != _sequence[_answer.Count - 1])
                return HackStep.AttemptFailed;
            return _answer.Count == _sequence.Length ? HackStep.Solved : HackStep.Running;
        }

        void SetPhase(Phase phase)
        {
            _phase = phase;
            _played = -1;
            _clock = 0f;
        }

        void Advance(float deltaTime, float interval, int count, Func<int, int> toneAt, Action finished)
        {
            _clock += deltaTime;
            if (_played >= 0 && _clock < interval)
                return;

            _clock = 0f;
            _played++;
            if (_played >= count)
            {
                finished();
                return;
            }
            ToneRequested?.Invoke(toneAt(_played));
        }

        public void Draw(ConsoleSurface surface, RectInt area)
        {
            var status = _phase switch
            {
                Phase.Reference => "CALIBRANDO TONOS",
                Phase.Pause => "PREPARADO",
                Phase.Listening => "ESCUCHA",
                _ => $"{_answer.Count} DE {_sequence.Length}",
            };
            surface.Text(status, area.x + 180, area.y + 120, HackPalette.Amber, 2);

            for (var i = 0; i < Tones; i++)
            {
                var key = Key(area, i);
                var height = 8 + i * 5;
                surface.Fill(key, HackPalette.Grid);
                surface.Frame(key, _phase == Phase.Answer ? HackPalette.Amber : HackPalette.Line);
                surface.Text((i + 1).ToString(), key.x + 30, key.y + 12, HackPalette.Text, 2);
                surface.Fill(key.x + 10, key.yMax - 6 - height, KeySize - 20, height, HackPalette.Dim);
            }
        }

#if UNITY_EDITOR
        public HackInput BotInput(RectInt area)
        {
            if (_phase != Phase.Answer)
                return default;
            return new HackInput { Typed = (char)('1' + _sequence[_answer.Count]) };
        }

        public char WrongDigit => (char)('1' + (_sequence[_answer.Count] + 1) % Tones);
#endif
    }
}
