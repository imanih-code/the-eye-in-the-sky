using System.Collections.Generic;
using EyeSky.UI;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>
    /// Girar piezas para llevar corriente de un lado a otro sin tocar cortocircuitos,
    /// mientras una linea de barrido va girando lo que encuentra.
    /// </summary>
    public sealed class PuentePuzzle : IHackPuzzle
    {
        const int North = 1;
        const int East = 2;
        const int South = 4;
        const int West = 8;
        const int AllSides = 15;
        const int CellSize = 52;
        static readonly int[] Sides = { North, East, South, West };
        static readonly Vector2Int[] Directions = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };

        readonly HackSettings.Puente _tuning;
        readonly System.Random _random;
        readonly int _size;
        readonly int[,] _mask;
        readonly bool[,] _short;
        readonly bool[,] _powered;
        readonly List<Vector2Int> _path = new();
        readonly Dictionary<Vector2Int, int> _solution = new();
        readonly int _sourceRow;
        readonly int _targetRow;
        float _sweepClock;
        int _sweepColumn = -1;
        bool _shorted;

        public string Title => "PUENTE DE CORRIENTE";
        public string Hint => "CLIC PARA GIRAR. LLEVA LA CORRIENTE A LA SALIDA SIN TOCAR LO ROJO";

        public PuentePuzzle(HackSettings.Puente tuning, int seed)
        {
            _tuning = tuning;
            _random = new System.Random(seed);
            _size = tuning.size;
            _mask = new int[_size, _size];
            _short = new bool[_size, _size];
            _powered = new bool[_size, _size];
            _sourceRow = _random.Next(_size);
            _targetRow = _random.Next(_size);

            CarvePath();
            FillBoard();
            Scramble();
        }

        static int Rotate(int mask) => ((mask << 1) & AllSides) | (mask >> 3);

        static int Opposite(int side) => side switch
        {
            North => South,
            South => North,
            East => West,
            _ => East,
        };

        void CarvePath()
        {
            var visited = new bool[_size, _size];
            Walk(new Vector2Int(0, _sourceRow), visited);
            for (var i = 0; i < _path.Count; i++)
            {
                var mask = i == 0 ? West : SideTowards(_path[i], _path[i - 1]);
                mask |= i == _path.Count - 1 ? East : SideTowards(_path[i], _path[i + 1]);
                _solution[_path[i]] = mask;
            }
        }

        bool Walk(Vector2Int cell, bool[,] visited)
        {
            visited[cell.x, cell.y] = true;
            _path.Add(cell);
            if (cell.x == _size - 1 && cell.y == _targetRow)
                return true;

            var order = new List<int> { 0, 1, 2, 3 };
            for (var i = order.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }

            foreach (var index in order)
            {
                var next = cell + Directions[index];
                if (next.x < 0 || next.y < 0 || next.x >= _size || next.y >= _size || visited[next.x, next.y])
                    continue;
                if (Walk(next, visited))
                    return true;
            }

            _path.RemoveAt(_path.Count - 1);
            return false;
        }

        static int SideTowards(Vector2Int from, Vector2Int to)
        {
            var delta = to - from;
            for (var i = 0; i < Directions.Length; i++)
                if (Directions[i] == delta)
                    return Sides[i];
            return 0;
        }

        void FillBoard()
        {
            var free = new List<Vector2Int>();
            for (var x = 0; x < _size; x++)
            for (var y = 0; y < _size; y++)
            {
                var cell = new Vector2Int(x, y);
                if (_solution.TryGetValue(cell, out var mask))
                {
                    _mask[x, y] = mask;
                    continue;
                }
                free.Add(cell);
                _mask[x, y] = _random.Next(3) switch
                {
                    0 => North | South,
                    1 => North | East,
                    _ => North | East | South,
                };
            }

            for (var i = 0; i < _tuning.shorts && free.Count > 0; i++)
            {
                var index = _random.Next(free.Count);
                var cell = free[index];
                free.RemoveAt(index);
                _short[cell.x, cell.y] = true;
                _mask[cell.x, cell.y] = AllSides;
            }
        }

        void Scramble()
        {
            for (var x = 0; x < _size; x++)
            for (var y = 0; y < _size; y++)
                if (!_short[x, y])
                    for (var turns = _random.Next(4); turns > 0; turns--)
                        _mask[x, y] = Rotate(_mask[x, y]);

            // La salida nunca esta conectada de partida
            if (ComputeFlow())
                _mask[_path[0].x, _path[0].y] = Rotate(_mask[_path[0].x, _path[0].y]);
        }

        bool ComputeFlow()
        {
            System.Array.Clear(_powered, 0, _powered.Length);
            _shorted = false;
            var start = new Vector2Int(0, _sourceRow);
            if ((_mask[start.x, start.y] & West) == 0)
                return false;

            var pending = new Queue<Vector2Int>();
            pending.Enqueue(start);
            _powered[start.x, start.y] = true;
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                if (_short[cell.x, cell.y])
                {
                    _shorted = true;
                    continue;
                }

                for (var i = 0; i < Directions.Length; i++)
                {
                    if ((_mask[cell.x, cell.y] & Sides[i]) == 0)
                        continue;
                    var next = cell + Directions[i];
                    if (next.x < 0 || next.y < 0 || next.x >= _size || next.y >= _size || _powered[next.x, next.y])
                        continue;
                    if ((_mask[next.x, next.y] & Opposite(Sides[i])) == 0)
                        continue;
                    _powered[next.x, next.y] = true;
                    pending.Enqueue(next);
                }
            }

            var last = _size - 1;
            return !_shorted && _powered[last, _targetRow] && (_mask[last, _targetRow] & East) != 0;
        }

        Vector2Int BoardOrigin(RectInt area) =>
            new(area.x + (area.width - _size * CellSize) / 2, area.y + 44 + (area.height - 60 - _size * CellSize) / 2);

        public HackStep Tick(float deltaTime, in HackInput input, RectInt area)
        {
            if (input.Click)
            {
                var origin = BoardOrigin(area);
                var x = Mathf.FloorToInt((input.Pointer.x - origin.x) / (float)CellSize);
                var y = Mathf.FloorToInt((input.Pointer.y - origin.y) / (float)CellSize);
                if (input.Pointer.x >= origin.x && input.Pointer.y >= origin.y && x < _size && y < _size && !_short[x, y])
                    _mask[x, y] = Rotate(_mask[x, y]);
            }

            _sweepClock += deltaTime;
            if (_sweepClock >= _tuning.sweepSeconds)
            {
                _sweepClock = 0f;
                _sweepColumn = (_sweepColumn + 1) % _size;
                for (var y = 0; y < _size; y++)
                    if (!_short[_sweepColumn, y] && _random.Next(3) == 0)
                        _mask[_sweepColumn, y] = Rotate(_mask[_sweepColumn, y]);
            }

            return ComputeFlow() ? HackStep.Solved : HackStep.Running;
        }

        public void Draw(ConsoleSurface surface, RectInt area)
        {
            var origin = BoardOrigin(area);
            for (var x = 0; x < _size; x++)
            for (var y = 0; y < _size; y++)
                DrawCell(surface, new RectInt(origin.x + x * CellSize, origin.y + y * CellSize, CellSize, CellSize), x, y);

            if (_sweepColumn >= 0)
                surface.Fill(origin.x + _sweepColumn * CellSize, origin.y - 8, CellSize, 4, HackPalette.Amber);

            var sourceY = origin.y + _sourceRow * CellSize + CellSize / 2;
            surface.Fill(origin.x - 40, sourceY - 3, 40, 6, HackPalette.Flow);
            surface.Text("ENTRADA", origin.x - 90, sourceY - 3, HackPalette.Text);
            var targetY = origin.y + _targetRow * CellSize + CellSize / 2;
            var exitX = origin.x + _size * CellSize;
            surface.Fill(exitX, targetY - 3, 40, 6, HackPalette.Amber);
            surface.Text("SALIDA", exitX + 46, targetY - 3, HackPalette.Text);
        }

        void DrawCell(ConsoleSurface surface, RectInt rect, int x, int y)
        {
            surface.Fill(rect, HackPalette.Grid);
            surface.Frame(rect, HackPalette.Line);
            var center = rect.center;
            var cx = Mathf.RoundToInt(center.x);
            var cy = Mathf.RoundToInt(center.y);

            if (_short[x, y])
            {
                surface.Fill(cx - 12, cy - 12, 24, 24, HackPalette.Red);
                surface.Text("X", cx - 2, cy - 3, HackPalette.Back);
                return;
            }

            var color = _powered[x, y] ? HackPalette.Flow : HackPalette.Dim;
            var mask = _mask[x, y];
            const int thick = 3;
            var half = CellSize / 2;
            surface.Fill(cx - thick, cy - thick, thick * 2, thick * 2, color);
            if ((mask & North) != 0)
                surface.Fill(cx - thick, cy - half + 1, thick * 2, half, color);
            if ((mask & South) != 0)
                surface.Fill(cx - thick, cy, thick * 2, half - 1, color);
            if ((mask & West) != 0)
                surface.Fill(cx - half + 1, cy - thick, half, thick * 2, color);
            if ((mask & East) != 0)
                surface.Fill(cx, cy - thick, half - 1, thick * 2, color);
        }

#if UNITY_EDITOR
        public HackInput BotInput(RectInt area)
        {
            var origin = BoardOrigin(area);
            foreach (var cell in _path)
            {
                if (_mask[cell.x, cell.y] == _solution[cell])
                    continue;
                return new HackInput
                {
                    Click = true,
                    Held = true,
                    Pointer = new Vector2Int(origin.x + cell.x * CellSize + 5, origin.y + cell.y * CellSize + 5),
                };
            }
            return default;
        }
#endif
    }
}
