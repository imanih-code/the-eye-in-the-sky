using EyeSky.UI;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>Igualar una onda con tres diales que derivan solos y mantener la coincidencia.</summary>
    public sealed class EmpalmePuzzle : IHackPuzzle
    {
        const int Knobs = 3;
        const int SliderX = 150;
        const int SliderY = 320;
        const int SliderWidth = 500;
        const int SliderHeight = 20;
        const int SliderGap = 34;
        static readonly string[] KnobNames = { "FRECUENCIA", "AMPLITUD", "FASE" };

        readonly HackSettings.Empalme _tuning;
        readonly System.Random _random;
        readonly float[] _target = new float[Knobs];
        readonly float[] _value = new float[Knobs];
        readonly float[] _drift = new float[Knobs];
        float _hold;
        float _driftClock;
        float _time;
        int _dragged = -1;

        public string Title => "EMPALME DE SENAL";
        public string Hint => "ARRASTRA LOS DIALES HASTA IGUALAR LA ONDA Y MANTENLA";

        public EmpalmePuzzle(HackSettings.Empalme tuning, int seed)
        {
            _tuning = tuning;
            _random = new System.Random(seed);
            for (var i = 0; i < Knobs; i++)
            {
                _target[i] = Next(0.15f, 0.85f);
                // Siempre empieza lejos de la solucion
                var offset = Next(0.25f, 0.4f) * (_random.Next(2) == 0 ? -1f : 1f);
                var start = _target[i] + offset;
                _value[i] = start is < 0f or > 1f ? _target[i] - offset : start;
            }
        }

        float Next(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        RectInt SliderRect(RectInt area, int index) => new(area.x + SliderX, area.y + SliderY + index * SliderGap, SliderWidth, SliderHeight);

        float Error(int index)
        {
            var distance = Mathf.Abs(_value[index] - _target[index]);
            return index == 2 ? Mathf.Min(distance, 1f - distance) : distance;
        }

        public HackStep Tick(float deltaTime, in HackInput input, RectInt area)
        {
            _time += deltaTime;
            if (!input.Held)
                _dragged = -1;
            for (var i = 0; i < Knobs; i++)
                if (input.Held && (input.Click || _dragged < 0) && SliderRect(area, i).Contains(input.Pointer))
                    _dragged = i;

            if (_dragged >= 0)
            {
                var rect = SliderRect(area, _dragged);
                _value[_dragged] = Mathf.Clamp01((input.Pointer.x - rect.x) / (float)rect.width);
            }

            _driftClock -= deltaTime;
            if (_driftClock <= 0f)
            {
                _driftClock = 0.7f;
                for (var i = 0; i < Knobs; i++)
                    _drift[i] = Next(-1f, 1f) * _tuning.driftPerSecond;
            }
            for (var i = 0; i < Knobs; i++)
                if (i != _dragged)
                    _value[i] = Mathf.Clamp01(_value[i] + _drift[i] * deltaTime);

            var matched = true;
            for (var i = 0; i < Knobs; i++)
                matched &= Error(i) <= _tuning.tolerance;

            _hold = matched ? _hold + deltaTime : 0f;
            return _hold >= _tuning.holdSeconds ? HackStep.Solved : HackStep.Running;
        }

        static float Wave(float[] knobs, float x, float wobble)
        {
            var frequency = Mathf.Lerp(1f, 5f, knobs[0]);
            var amplitude = Mathf.Lerp(0.2f, 1f, knobs[1]);
            return amplitude * Mathf.Sin(x * frequency * Mathf.PI * 2f + knobs[2] * Mathf.PI * 2f) + wobble;
        }

        public void Draw(ConsoleSurface surface, RectInt area)
        {
            var scope = new RectInt(area.x + 60, area.y + 50, 680, 240);
            surface.Fill(scope, HackPalette.Grid);
            surface.Frame(scope, HackPalette.Line);
            var middle = scope.y + scope.height / 2;
            surface.Fill(scope.x, middle, scope.width, 1, HackPalette.Line);

            for (var px = 0; px < scope.width; px++)
            {
                var x = px / (float)scope.width;
                var target = Wave(_target, x, 0f);
                var mine = Wave(_value, x, Mathf.Sin(px * 0.9f + _time * 31f) * 0.035f);
                surface.Fill(scope.x + px, middle - Mathf.RoundToInt(target * 104f), 1, 3, HackPalette.Amber);
                surface.Fill(scope.x + px, middle - Mathf.RoundToInt(mine * 104f), 1, 2, HackPalette.Flow);
            }

            for (var i = 0; i < Knobs; i++)
            {
                var rect = SliderRect(area, i);
                surface.Text(KnobNames[i], area.x + 60, rect.y + 6, HackPalette.Text);
                surface.Fill(rect, HackPalette.Grid);
                surface.Frame(rect, HackPalette.Line);
                surface.Fill(rect.x + Mathf.RoundToInt(_value[i] * rect.width) - 4, rect.y - 3, 8, rect.height + 6,
                    Error(i) <= _tuning.tolerance ? HackPalette.Flow : HackPalette.Amber);
            }

            var progress = Mathf.Clamp01(_hold / _tuning.holdSeconds);
            surface.Text("ENLACE", scope.x, area.y + 300, HackPalette.Dim);
            surface.Fill(scope.x + 48, area.y + 302, Mathf.RoundToInt(progress * 200f), 5, HackPalette.Flow);
        }

#if UNITY_EDITOR
        public HackInput BotInput(RectInt area)
        {
            var worst = 0;
            for (var i = 1; i < Knobs; i++)
                if (Error(i) > Error(worst))
                    worst = i;

            var rect = SliderRect(area, worst);
            return new HackInput
            {
                Held = true,
                Click = true,
                Pointer = new Vector2Int(rect.x + Mathf.RoundToInt(_target[worst] * rect.width), rect.y + 5),
            };
        }
#endif
    }
}
