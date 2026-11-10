using System.Collections.Generic;
using EyeSky.UI;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>
    /// Un laberinto con ciclos: recoger las llaves y salir mientras el cortafuegos
    /// te persigue por el camino mas corto, cada vez mas rapido.
    /// </summary>
    public sealed class CortafuegosPuzzle : IHackPuzzle
    {
        const int CellPixels = 30;
        const float HunterStartDelay = 0.8f;
        static readonly Vector2Int[] Directions = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };

        readonly HackSettings.Cortafuegos _tuning;
        readonly System.Random _random;
        readonly int _width;
        readonly int _height;
        readonly bool[,] _open;
        readonly int[,] _distance;
        readonly List<Vector2Int> _keys = new();
        readonly Vector2Int _exit;
        Vector2Int _player;
        Vector2Int _hunter;
        float _playerClock;
        float _hunterClock;
        float _time;
        int _collected;

        public string Title => "CORTAFUEGOS";
        public string Hint => "FLECHAS O WASD. RECOGE LAS LLAVES Y SAL. NO DEJES QUE TE ALCANCE";

        public CortafuegosPuzzle(HackSettings.Cortafuegos tuning, int seed)
        {
            _tuning = tuning;
            _random = new System.Random(seed);
            _width = tuning.roomsWide * 2 + 1;
            _height = tuning.roomsHigh * 2 + 1;
            _open = new bool[_width, _height];
            _distance = new int[_width, _height];

            CarveMaze();
            Braid();

            _player = new Vector2Int(1, 1);
            _hunter = new Vector2Int(_width - 2, _height - 2);
            _exit = new Vector2Int(_width - 1, RoomCoordinate(_random.Next(tuning.roomsHigh)));
            PlaceKeys();
        }

        static int RoomCoordinate(int index) => index * 2 + 1;

        void CarveMaze()
        {
            var stack = new Stack<Vector2Int>();
            var start = new Vector2Int(1, 1);
            _open[1, 1] = true;
            stack.Push(start);
            var candidates = new List<Vector2Int>();
            while (stack.Count > 0)
            {
                var cell = stack.Peek();
                candidates.Clear();
                foreach (var direction in Directions)
                {
                    var next = cell + direction * 2;
                    if (IsRoom(next) && !_open[next.x, next.y])
                        candidates.Add(direction);
                }

                if (candidates.Count == 0)
                {
                    stack.Pop();
                    continue;
                }

                var chosen = candidates[_random.Next(candidates.Count)];
                _open[cell.x + chosen.x, cell.y + chosen.y] = true;
                var target = cell + chosen * 2;
                _open[target.x, target.y] = true;
                stack.Push(target);
            }
        }

        bool IsRoom(Vector2Int cell) => cell.x > 0 && cell.y > 0 && cell.x < _width - 1 && cell.y < _height - 1;

        // Sin callejones sin salida: siempre se puede dar la vuelta y despistarlo
        void Braid()
        {
            for (var x = 1; x < _width - 1; x += 2)
            for (var y = 1; y < _height - 1; y += 2)
            {
                var room = new Vector2Int(x, y);
                var exits = 0;
                var walls = new List<Vector2Int>();
                foreach (var direction in Directions)
                {
                    var wall = room + direction;
                    if (_open[wall.x, wall.y])
                        exits++;
                    else if (IsRoom(room + direction * 2))
                        walls.Add(wall);
                }

                if (exits <= 1 && walls.Count > 0)
                {
                    var wall = walls[_random.Next(walls.Count)];
                    _open[wall.x, wall.y] = true;
                }
                else if (walls.Count > 0 && _random.NextDouble() < 0.12)
                {
                    var wall = walls[_random.Next(walls.Count)];
                    _open[wall.x, wall.y] = true;
                }
            }
        }

        void PlaceKeys()
        {
            ComputeDistances(_hunter);
            var fromHunter = (int[,])_distance.Clone();
            ComputeDistances(_player);

            // Ni al lado de la salida del jugador ni en la boca del perseguidor
            var rooms = new List<Vector2Int>();
            for (var x = 1; x < _width - 1; x += 2)
            for (var y = 1; y < _height - 1; y += 2)
                if (_distance[x, y] >= 10 && fromHunter[x, y] >= 12)
                    rooms.Add(new Vector2Int(x, y));

            // Separadas entre si, para obligar a cruzar el laberinto
            for (var attempt = 0; attempt < 200 && _keys.Count < _tuning.keys; attempt++)
            {
                var candidate = rooms[_random.Next(rooms.Count)];
                if (candidate == _hunter)
                    continue;
                var farEnough = true;
                foreach (var key in _keys)
                    farEnough &= Mathf.Abs(key.x - candidate.x) + Mathf.Abs(key.y - candidate.y) >= 10;
                if (farEnough)
                    _keys.Add(candidate);
            }

            while (_keys.Count < _tuning.keys)
                _keys.Add(rooms[_random.Next(rooms.Count)]);
        }

        void ComputeDistances(Vector2Int from)
        {
            for (var x = 0; x < _width; x++)
            for (var y = 0; y < _height; y++)
                _distance[x, y] = int.MaxValue;

            var pending = new Queue<Vector2Int>();
            pending.Enqueue(from);
            _distance[from.x, from.y] = 0;
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (!Walkable(next) || _distance[next.x, next.y] != int.MaxValue)
                        continue;
                    _distance[next.x, next.y] = _distance[cell.x, cell.y] + 1;
                    pending.Enqueue(next);
                }
            }
        }

        bool Walkable(Vector2Int cell)
        {
            if (cell.x < 0 || cell.y < 0 || cell.x >= _width || cell.y >= _height)
                return false;
            return _open[cell.x, cell.y] || (cell == _exit && _collected >= _tuning.keys);
        }

        float HunterRatio => _tuning.hunterStartRatio + _collected * _tuning.hunterRatioPerKey;

        public HackStep Tick(float deltaTime, in HackInput input, RectInt area)
        {
            _time += deltaTime;
            _playerClock += deltaTime;
            var playerStep = 1f / _tuning.cellsPerSecond;
            if (input.Move != Vector2Int.zero && _playerClock >= playerStep)
            {
                var next = _player + new Vector2Int(input.Move.x, 0);
                if (input.Move.x == 0 || !Walkable(next))
                    next = _player + new Vector2Int(0, input.Move.y);
                if (next != _player && Walkable(next))
                {
                    var previous = _player;
                    _player = next;
                    _playerClock = 0f;
                    if (_keys.Remove(_player))
                        _collected++;
                    if (_player == _exit)
                        return HackStep.Solved;
                    if (_player == _hunter || previous == _hunter)
                        return HackStep.AttemptFailed;
                }
            }

            if (_time < HunterStartDelay)
                return HackStep.Running;

            _hunterClock += deltaTime;
            var hunterStep = playerStep / HunterRatio;
            if (_hunterClock >= hunterStep)
            {
                _hunterClock = 0f;
                ComputeDistances(_player);
                var best = _hunter;
                foreach (var direction in Directions)
                {
                    var next = _hunter + direction;
                    if (next != _exit && Walkable(next) && _distance[next.x, next.y] < _distance[best.x, best.y])
                        best = next;
                }
                _hunter = best;
                if (_hunter == _player)
                    return HackStep.AttemptFailed;
            }
            return HackStep.Running;
        }

        public void Draw(ConsoleSurface surface, RectInt area)
        {
            var origin = new Vector2Int(area.x + (area.width - _width * CellPixels) / 2, area.y + 42);
            RectInt Cell(Vector2Int c) => new(origin.x + c.x * CellPixels, origin.y + c.y * CellPixels, CellPixels, CellPixels);

            for (var x = 0; x < _width; x++)
            for (var y = 0; y < _height; y++)
                surface.Fill(Cell(new Vector2Int(x, y)), _open[x, y] ? HackPalette.Grid : HackPalette.Back);

            for (var x = 0; x < _width; x++)
            for (var y = 0; y < _height; y++)
                if (!_open[x, y])
                    surface.Frame(Cell(new Vector2Int(x, y)), HackPalette.Line);

            var exit = Cell(_exit);
            surface.Fill(exit, _collected >= _tuning.keys ? HackPalette.Flow : HackPalette.Line);
            surface.Text("S", exit.x + 12, exit.y + 11, HackPalette.Back);

            foreach (var key in _keys)
            {
                var rect = Cell(key);
                surface.Fill(rect.x + 9, rect.y + 9, 12, 12, HackPalette.Amber);
            }

            var player = Cell(_player);
            surface.Fill(player.x + 6, player.y + 6, 18, 18, HackPalette.Flow);
            var hunter = Cell(_hunter);
            surface.Fill(hunter.x + 5, hunter.y + 5, 20, 20, HackPalette.Red);

            surface.Text($"LLAVES {_collected}/{_tuning.keys}", area.x + 8, area.y + 34, HackPalette.Amber);
        }

#if UNITY_EDITOR
        public HackInput BotInput(RectInt area)
        {
            var goals = new List<Vector2Int>(_keys);
            if (goals.Count == 0)
                goals.Add(_exit);

            // Busca la meta mas cercana evitando la zona del perseguidor
            ComputeDistances(_hunter);
            var hunterDistance = (int[,])_distance.Clone();
            var frontier = new Queue<Vector2Int>();
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            frontier.Enqueue(_player);
            previous[_player] = _player;
            while (frontier.Count > 0)
            {
                var cell = frontier.Dequeue();
                if (goals.Contains(cell))
                    return InputTowards(cell, previous);
                foreach (var direction in Directions)
                {
                    var next = cell + direction;
                    if (!Walkable(next) || previous.ContainsKey(next) || hunterDistance[next.x, next.y] <= 2)
                        continue;
                    previous[next] = cell;
                    frontier.Enqueue(next);
                }
            }

            // Encerrado: huye hacia donde el perseguidor quede mas lejos
            var best = Vector2Int.zero;
            var bestDistance = -1;
            foreach (var direction in Directions)
            {
                var next = _player + direction;
                if (Walkable(next) && hunterDistance[next.x, next.y] > bestDistance)
                {
                    best = direction;
                    bestDistance = hunterDistance[next.x, next.y];
                }
            }
            return new HackInput { Move = best };
        }

        HackInput InputTowards(Vector2Int goal, Dictionary<Vector2Int, Vector2Int> previous)
        {
            var cell = goal;
            while (previous[cell] != _player && cell != _player)
                cell = previous[cell];
            return new HackInput { Move = cell - _player };
        }
#endif
    }
}
