using EyeSky.UI;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>Reconocer tres huellas entre muestras borrosas casi iguales, sin ninguna pista.</summary>
    public sealed class HuellaPuzzle : IHackPuzzle
    {
        const int ReferenceScale = 2;
        const int SampleGapX = 16;
        const int SampleGapY = 30;
        const int Columns = 4;
        const float SampleNoise = 0.14f;
        const int GenerationTries = 40;

        readonly HackSettings.Huella _tuning;
        readonly System.Random _random;
        float[] _reference;
        float[][] _blurred;
        float[][] _focused;
        int _correct;
        int _finger;
        int _hovered = -1;

        public string Title => "LECTOR BIOMETRICO";
        public string Hint => "ELIGE LA MUESTRA QUE CORRESPONDE A LA HUELLA. PASA EL RATON PARA ENFOCAR";

        /// <summary>Menor diferencia real entre la buena y una falsa, para las pruebas.</summary>
        public float WeakestDifference { get; private set; }

        public HuellaPuzzle(HackSettings.Huella tuning, int seed)
        {
            _tuning = tuning;
            _random = new System.Random(seed);
            NextFinger();
        }

        void NextFinger()
        {
            var family = Fingerprints.NewFamily(_random);
            var minutiae = Fingerprints.NewMinutiae(_random);
            var clean = Fingerprints.Render(family, minutiae);
            var cleanBlurred = Fingerprints.Blur(clean, _tuning.blurRadius);

            _reference = clean;
            _correct = _random.Next(_tuning.samples);
            _blurred = new float[_tuning.samples][];
            _focused = new float[_tuning.samples][];
            WeakestDifference = float.MaxValue;

            for (var i = 0; i < _tuning.samples; i++)
            {
                var sample = clean;
                if (i != _correct)
                    sample = DistinguishableFake(family, minutiae, cleanBlurred);
                _blurred[i] = Fingerprints.WithNoise(Fingerprints.Blur(sample, _tuning.blurRadius), SampleNoise, _random);
                _focused[i] = Fingerprints.WithNoise(Fingerprints.Blur(sample, _tuning.focusedBlurRadius), SampleNoise * 0.6f, _random);
            }
        }

        // Una falsa solo vale si, ya borrosa, sigue viendose distinta: si no, seria adivinar
        float[] DistinguishableFake(Fingerprints.Family family, System.Collections.Generic.List<Fingerprints.Minutia> minutiae, float[] correctBlurred)
        {
            float[] candidate = null;
            var bestDifference = -1f;
            for (var attempt = 0; attempt < GenerationTries; attempt++)
            {
                var fake = Fingerprints.Render(family, Fingerprints.Altered(minutiae, _tuning.changedMinutiae, _random));
                var difference = Fingerprints.StrongestLocalDifference(correctBlurred, Fingerprints.Blur(fake, _tuning.blurRadius));
                if (difference > bestDifference)
                {
                    candidate = fake;
                    bestDifference = difference;
                }
                if (difference >= _tuning.minimumDifference)
                    break;
            }
            WeakestDifference = Mathf.Min(WeakestDifference, bestDifference);
            return candidate;
        }

        RectInt SampleRect(RectInt area, int index)
        {
            var x = area.x + 270 + index % Columns * (Fingerprints.Size + SampleGapX);
            var y = area.y + 90 + index / Columns * (Fingerprints.Size + SampleGapY);
            return new RectInt(x, y, Fingerprints.Size, Fingerprints.Size);
        }

        public HackStep Tick(float deltaTime, in HackInput input, RectInt area)
        {
            _hovered = -1;
            for (var i = 0; i < _tuning.samples; i++)
                if (SampleRect(area, i).Contains(input.Pointer))
                    _hovered = i;

            if (!input.Click || _hovered < 0)
                return HackStep.Running;
            if (_hovered != _correct)
                return HackStep.AttemptFailed;

            _finger++;
            if (_finger >= _tuning.fingers)
                return HackStep.Solved;
            NextFinger();
            return HackStep.Running;
        }

        public void Draw(ConsoleSurface surface, RectInt area)
        {
            surface.Text($"HUELLA {_finger + 1} DE {_tuning.fingers}", area.x + 40, area.y + 60, HackPalette.Amber);
            DrawPrint(surface, _reference, area.x + 40, area.y + 80, ReferenceScale);

            for (var i = 0; i < _tuning.samples; i++)
            {
                var rect = SampleRect(area, i);
                DrawPrint(surface, i == _hovered ? _focused[i] : _blurred[i], rect.x, rect.y, 1);
                surface.Frame(new RectInt(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 4),
                    i == _hovered ? HackPalette.Amber : HackPalette.Line);
            }
        }

        static void DrawPrint(ConsoleSurface surface, float[] pixels, int originX, int originY, int scale)
        {
            for (var y = 0; y < Fingerprints.Size; y++)
            for (var x = 0; x < Fingerprints.Size; x++)
            {
                var value = pixels[y * Fingerprints.Size + x];
                var color = new Color32((byte)(20 + value * 130), (byte)(30 + value * 210), (byte)(26 + value * 150), 255);
                surface.Fill(originX + x * scale, originY + y * scale, scale, scale, color);
            }
        }

#if UNITY_EDITOR
        public HackInput BotInput(RectInt area)
        {
            return new HackInput { Click = true, Held = true, Pointer = Vector2Int.RoundToInt(SampleRect(area, _correct).center) };
        }

        public Vector2Int WrongSamplePoint(RectInt area)
        {
            return Vector2Int.RoundToInt(SampleRect(area, (_correct + 1) % _tuning.samples).center);
        }
#endif
    }
}
