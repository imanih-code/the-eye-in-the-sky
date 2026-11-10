using System.Collections.Generic;
using System.Text;
using EyeSky.UI;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>
    /// Encontrar fragmentos de codigo en filas que se desplazan cada una a su ritmo,
    /// encajando un rectangulo encima y confirmando.
    /// </summary>
    public sealed class ParchePuzzle : IHackPuzzle
    {
        public const int Rows = 12;
        public const int Columns = 32;
        const string Alphabet = "0123456789ABCDEF";
        const float GraceSeconds = 0.12f;
        const int CellWidth = 20;
        const int CellHeight = 28;
        const int GridX = 20;
        const int GridY = 50;
        const int GenerationTries = 60;

        sealed class Row
        {
            public char[] Text;
            public int Offset;
            public int PreviousOffset;
            public float StepSeconds;
            public int Direction;
            public float Clock;
            public float SinceStep;
        }

        readonly HackSettings.Parche _tuning;
        readonly System.Random _random;
        readonly Row[] _rows = new Row[Rows];
        readonly List<string> _pending = new();
        readonly List<string> _done = new();
        Vector2Int _cursor = new(4, 5);
        Vector2Int _lastPointer = new(-1, -1);
        int _errors;

        public string Title => "PARCHE EN CALIENTE";
        public string Hint => "ENCAJA EL RECUADRO EN CADA FRAGMENTO Y PULSA ESPACIO";

        public ParchePuzzle(HackSettings.Parche tuning, int seed)
        {
            _tuning = tuning;
            _random = new System.Random(seed);
            for (var i = 0; i < Rows; i++)
                _rows[i] = new Row();
            Generate();
        }

        void Generate()
        {
            for (var attempt = 0; attempt < GenerationTries; attempt++)
            {
                _pending.Clear();
                FillRandom();
                if (PlantFragments())
                    break;
            }

            foreach (var row in _rows)
            {
                row.Offset = _random.Next(Columns);
                row.PreviousOffset = row.Offset;
                row.Direction = _random.Next(2) == 0 ? 1 : -1;
                row.StepSeconds = _tuning.rowStepMin + (float)_random.NextDouble() * (_tuning.rowStepMax - _tuning.rowStepMin);
                row.Clock = 0f;
                row.SinceStep = 10f;
            }
        }

        void FillRandom()
        {
            foreach (var row in _rows)
            {
                row.Text = new char[Columns];
                for (var i = 0; i < Columns; i++)
                    row.Text[i] = Alphabet[_random.Next(Alphabet.Length)];
            }
        }

        bool PlantFragments()
        {
            var occupied = new bool[Rows, Columns];
            var length = _tuning.fragmentLength;

            for (var f = 0; f < _tuning.fragments; f++)
            {
                var fragment = RandomText(length);
                if (!Plant(fragment, occupied))
                    return false;
                _pending.Add(fragment);
                for (var d = 0; d < _tuning.decoysPerFragment; d++)
                {
                    // Un senuelo se distingue solo por un caracter
                    var decoy = fragment.ToCharArray();
                    var position = _random.Next(length);
                    decoy[position] = Different(decoy[position]);
                    if (!Plant(new string(decoy), occupied))
                        return false;
                }
            }

            foreach (var fragment in _pending)
                if (CountOccurrences(fragment) != 1)
                    return false;
            return true;
        }

        string RandomText(int length)
        {
            var builder = new StringBuilder(length);
            for (var i = 0; i < length; i++)
                builder.Append(Alphabet[_random.Next(Alphabet.Length)]);
            return builder.ToString();
        }

        char Different(char original)
        {
            char replacement;
            do
                replacement = Alphabet[_random.Next(Alphabet.Length)];
            while (replacement == original);
            return replacement;
        }

        bool Plant(string text, bool[,] occupied)
        {
            for (var attempt = 0; attempt < 80; attempt++)
            {
                var rowIndex = _random.Next(Rows);
                var start = _random.Next(Columns);
                var free = true;
                for (var i = 0; i < text.Length; i++)
                    free &= !occupied[rowIndex, (start + i) % Columns];
                if (!free)
                    continue;

                for (var i = 0; i < text.Length; i++)
                {
                    var column = (start + i) % Columns;
                    occupied[rowIndex, column] = true;
                    _rows[rowIndex].Text[column] = text[i];
                }
                return true;
            }
            return false;
        }

        /// <summary>Cuantas veces aparece, contando las que cruzan el borde. Publico para las pruebas.</summary>
        public int CountOccurrences(string fragment)
        {
            var count = 0;
            foreach (var row in _rows)
            for (var start = 0; start < Columns; start++)
            {
                var matches = true;
                for (var i = 0; i < fragment.Length && matches; i++)
                    matches = row.Text[(start + i) % Columns] == fragment[i];
                if (matches)
                    count++;
            }
            return count;
        }

        public IReadOnlyList<string> Pending => _pending;

        static int Wrap(int value) => (value % Columns + Columns) % Columns;

        string ReadCursor(int offset, Row row)
        {
            var builder = new StringBuilder(_tuning.fragmentLength);
            for (var i = 0; i < _tuning.fragmentLength; i++)
                builder.Append(row.Text[Wrap(_cursor.x + i + offset)]);
            return builder.ToString();
        }

        Vector2Int CursorFromPointer(Vector2Int pointer, RectInt area)
        {
            return new Vector2Int(Wrap(Mathf.FloorToInt((pointer.x - area.x - GridX) / (float)CellWidth)),
                Mathf.Clamp(Mathf.FloorToInt((pointer.y - area.y - GridY) / (float)CellHeight), 0, Rows - 1));
        }

        public HackStep Tick(float deltaTime, in HackInput input, RectInt area)
        {
            foreach (var row in _rows)
            {
                row.Clock += deltaTime;
                row.SinceStep += deltaTime;
                if (row.Clock < row.StepSeconds)
                    continue;
                row.Clock = 0f;
                row.SinceStep = 0f;
                row.PreviousOffset = row.Offset;
                row.Offset = Wrap(row.Offset + row.Direction);
            }

            if (input.Pointer != _lastPointer && area.Contains(input.Pointer))
            {
                _lastPointer = input.Pointer;
                _cursor = CursorFromPointer(input.Pointer, area);
            }

            if (input.Step != Vector2Int.zero)
                _cursor = new Vector2Int(Wrap(_cursor.x + input.Step.x), Mathf.Clamp(_cursor.y + input.Step.y, 0, Rows - 1));

            if (!input.Confirm)
                return HackStep.Running;

            var current = _rows[_cursor.y];
            var text = ReadCursor(current.Offset, current);
            var hit = _pending.Remove(text);
            if (!hit && current.SinceStep <= GraceSeconds)
            {
                // Una fila en movimiento no castiga un acierto real
                text = ReadCursor(current.PreviousOffset, current);
                hit = _pending.Remove(text);
            }

            if (!hit)
            {
                _errors++;
                return _errors >= _tuning.errors ? HackStep.AttemptFailed : HackStep.Mistake;
            }

            _done.Add(text);
            if (_pending.Count == 0)
                return HackStep.Solved;
            Reorder();
            return HackStep.Running;
        }

        void Reorder()
        {
            for (var i = _rows.Length - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (_rows[i], _rows[j]) = (_rows[j], _rows[i]);
            }
        }

        public void Draw(ConsoleSurface surface, RectInt area)
        {
            for (var y = 0; y < Rows; y++)
            {
                var row = _rows[y];
                for (var x = 0; x < Columns; x++)
                {
                    var character = row.Text[Wrap(x + row.Offset)];
                    surface.Text(character.ToString(), area.x + GridX + x * CellWidth + 4,
                        area.y + GridY + y * CellHeight + 7, HackPalette.Text, 2);
                }
            }

            for (var i = 0; i < _tuning.fragmentLength; i++)
            {
                var column = Wrap(_cursor.x + i);
                surface.Frame(new RectInt(area.x + GridX + column * CellWidth, area.y + GridY + _cursor.y * CellHeight,
                    CellWidth, CellHeight), HackPalette.Amber);
            }

            var panelX = area.x + GridX + Columns * CellWidth + 8;
            surface.Text("FALTAN", panelX, area.y + 52, HackPalette.Dim);
            for (var i = 0; i < _pending.Count; i++)
                surface.Text(_pending[i], panelX, area.y + 66 + i * 14, HackPalette.Amber);

            surface.Text("ERRORES", panelX, area.y + 130, HackPalette.Dim);
            surface.Text($"{_errors}/{_tuning.errors}", panelX, area.y + 144, _errors > 0 ? HackPalette.Red : HackPalette.Text);
        }

#if UNITY_EDITOR
        public HackInput BotInput(RectInt area)
        {
            var fragment = _pending[0];
            for (var y = 0; y < Rows; y++)
            for (var x = 0; x < Columns; x++)
            {
                var row = _rows[y];
                var matches = true;
                for (var i = 0; i < fragment.Length && matches; i++)
                    matches = row.Text[Wrap(x + i + row.Offset)] == fragment[i];
                if (!matches)
                    continue;

                return new HackInput
                {
                    Confirm = true,
                    Pointer = new Vector2Int(area.x + GridX + x * CellWidth + 2, area.y + GridY + y * CellHeight + 2),
                };
            }
            return default;
        }
#endif
    }
}
