using System.Collections.Generic;
using System.Text;
using EyeSky.UI;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>Deducir un codigo: cada intento dice cuantos estan bien colocados y cuantos existen en otro sitio.</summary>
    public sealed class CriptaPuzzle : IHackPuzzle
    {
        const int RowTop = 52;
        const int RowHeight = 42;
        const int SlotSize = 34;
        const int SlotGap = 6;

        struct Attempt
        {
            public string Guess;
            public int Exact;
            public int Misplaced;
        }

        readonly HackSettings.Cripta _tuning;
        readonly string _code;
        readonly List<Attempt> _attempts = new();
        readonly StringBuilder _current = new();
        readonly string _alphabet;

        public string Title => "CRIPTA";
        public string Hint => "ESCRIBE EL CODIGO CON LAS TECLAS O CLIC. ENTER COMPRUEBA, RETROCESO BORRA";

        public CriptaPuzzle(HackSettings.Cripta tuning, int seed)
        {
            _tuning = tuning;
            _alphabet = "ABCDEFGHIJKL".Substring(0, tuning.symbols);
            var random = new System.Random(seed);
            var code = new StringBuilder();
            var pool = new List<char>(_alphabet);
            for (var i = 0; i < tuning.length; i++)
            {
                var index = random.Next(pool.Count);
                code.Append(pool[index]);
                if (!tuning.allowRepeats)
                    pool.RemoveAt(index);
            }
            _code = code.ToString();
        }

        /// <summary>Publico para las pruebas.</summary>
        public static void Score(string code, string guess, out int exact, out int misplaced)
        {
            exact = 0;
            var codeLeft = new Dictionary<char, int>();
            var guessLeft = new Dictionary<char, int>();
            for (var i = 0; i < code.Length; i++)
            {
                if (code[i] == guess[i])
                {
                    exact++;
                    continue;
                }
                codeLeft[code[i]] = codeLeft.GetValueOrDefault(code[i]) + 1;
                guessLeft[guess[i]] = guessLeft.GetValueOrDefault(guess[i]) + 1;
            }

            misplaced = 0;
            foreach (var pair in guessLeft)
                misplaced += Mathf.Min(pair.Value, codeLeft.GetValueOrDefault(pair.Key));
        }

        RectInt PaletteKey(RectInt area, int index) => new(area.x + 520 + index % 4 * 56, area.y + 150 + index / 4 * 56, 48, 48);

        public HackStep Tick(float deltaTime, in HackInput input, RectInt area)
        {
            if (input.Typed != '\0')
                Add(char.ToUpperInvariant(input.Typed));

            if (input.Click)
                for (var i = 0; i < _alphabet.Length; i++)
                    if (PaletteKey(area, i).Contains(input.Pointer))
                        Add(_alphabet[i]);

            if (input.Backspace && _current.Length > 0)
                _current.Length--;

            if (!input.Confirm || _current.Length < _tuning.length)
                return HackStep.Running;

            var guess = _current.ToString();
            _current.Clear();
            Score(_code, guess, out var exact, out var misplaced);
            _attempts.Add(new Attempt { Guess = guess, Exact = exact, Misplaced = misplaced });

            if (exact == _tuning.length)
                return HackStep.Solved;
            return _attempts.Count >= _tuning.guesses ? HackStep.AttemptFailed : HackStep.Running;
        }

        void Add(char symbol)
        {
            if (_current.Length >= _tuning.length || _alphabet.IndexOf(symbol) < 0)
                return;
            if (!_tuning.allowRepeats && _current.ToString().IndexOf(symbol) >= 0)
                return;
            _current.Append(symbol);
        }

        public void Draw(ConsoleSurface surface, RectInt area)
        {
            for (var row = 0; row < _tuning.guesses; row++)
            {
                var y = area.y + RowTop + row * RowHeight;
                var isCurrent = row == _attempts.Count;
                var text = row < _attempts.Count ? _attempts[row].Guess : isCurrent ? _current.ToString() : string.Empty;

                for (var slot = 0; slot < _tuning.length; slot++)
                {
                    var rect = new RectInt(area.x + 60 + slot * (SlotSize + SlotGap), y, SlotSize, SlotSize);
                    surface.Fill(rect, HackPalette.Grid);
                    surface.Frame(rect, isCurrent ? HackPalette.Amber : HackPalette.Line);
                    if (slot < text.Length)
                        surface.Text(text[slot].ToString(), rect.x + 12, rect.y + 10, HackPalette.Text, 2);
                }

                if (row < _attempts.Count)
                {
                    var feedbackX = area.x + 280;
                    surface.Text($"{_attempts[row].Exact} BIEN", feedbackX, y + 6, HackPalette.Flow);
                    surface.Text($"{_attempts[row].Misplaced} EN OTRO SITIO", feedbackX, y + 20, HackPalette.Amber);
                }
            }

            surface.Text("SIMBOLOS", area.x + 520, area.y + 130, HackPalette.Dim);
            for (var i = 0; i < _alphabet.Length; i++)
            {
                var key = PaletteKey(area, i);
                surface.Fill(key, HackPalette.Grid);
                surface.Frame(key, HackPalette.Line);
                surface.Text(_alphabet[i].ToString(), key.x + 19, key.y + 17, HackPalette.Text, 2);
            }
        }

#if UNITY_EDITOR
        public HackInput BotInput(RectInt area)
        {
            if (_current.Length < _code.Length)
                return new HackInput { Typed = _code[_current.Length] };
            return new HackInput { Confirm = true };
        }

        /// <summary>Para las pruebas: un codigo que seguro esta mal.</summary>
        public string WrongGuess => _code.Substring(1) + _code[0];
#endif
    }
}
