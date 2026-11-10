using System.Collections.Generic;
using EyeSky.Core;
using EyeSky.Feed;
using EyeSky.Hacking;
using EyeSky.Npc;
using EyeSky.Radio;
using EyeSky.Security;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EyeSky.UI
{
    /// <summary>
    /// La consola de la camioneta: plano sin personas, camaras, luces, ordenes a la banda y radio.
    /// Se dibuja a baja resolucion, como la imagen de las camaras.
    /// </summary>
    public sealed class ConsoleView : MonoBehaviour
    {
        public const int Width = 960;
        public const int Height = 540;
        const int FeedWidth = 800;
        const int FeedHeight = 450;
        const int PanelX = 804;
        const int PanelWidth = 152;
        const int MapTop = 18;
        const int MapPixelsPerMeter = 4;
        const int LightsTop = 146;
        const int LightRowHeight = 11;
        const int RowHeight = 12;
        const int RadioColumns = 64;
        const float RedrawSeconds = 0.05f;
        const float PersonHalfWidth = 0.03f;

        static readonly Color32 Background = new(11, 13, 16, 255);
        static readonly Color32 Panel = new(18, 22, 27, 255);
        static readonly Color32 Line = new(52, 68, 72, 255);
        static readonly Color32 Text = new(159, 216, 176, 255);
        static readonly Color32 Dim = new(84, 112, 100, 255);
        static readonly Color32 Amber = new(232, 182, 84, 255);
        static readonly Color32 Red = new(226, 84, 66, 255);
        static readonly Color32 Black = new(3, 4, 5, 255);
        static readonly Color32 RoomLit = new(30, 44, 46, 255);
        static readonly Color32 RoomHover = new(48, 72, 70, 255);

        enum Armed
        {
            None,
            Go,
            Knockout,
            Kill,
        }

        enum PanelAction
        {
            ArmGo,
            Stop,
            Hide,
            ArmKnockout,
            ArmKill,
            Retreat,
            Loop,
            Sensors,
            Interfere,
        }

        public HackSession hacks;
        public SensorGrid sensors;
        public RadioBus radio;
        public HeistDirector director;
        public RawImage surfaceImage;
        public FeedView feed;
        public CameraNetwork cameras;
        public RoomLighting lighting;
        public DoorNetwork doors;
        public BuildingLayout layout;

        struct RoomCell
        {
            public BuildingLayout.RoomDef Room;
            public RectInt Rect;
        }

        struct CameraButton
        {
            public int Index;
            public RectInt Rect;
        }

        struct LightRow
        {
            public BuildingLayout.RoomDef Room;
            public RectInt Rect;
        }

        struct DoorDot
        {
            public BuildingLayout.DoorDef Door;
            public RectInt Rect;
        }

        struct ActionButton
        {
            public PanelAction Action;
            public string Label;
            public RectInt Rect;
        }

        readonly List<RoomCell> _cells = new();
        readonly List<CameraButton> _cameraButtons = new();
        readonly List<LightRow> _lightRows = new();
        readonly List<DoorDot> _doorDots = new();
        readonly List<ActionButton> _actions = new();
        readonly RectInt[] _crewButtons = new RectInt[2];
        RectInt _infraredButton;
        int _bandTop;
        int _noteTop;

        ConsoleSurface _surface;
        Vector2Int _pointer = new(-1, -1);
        float _redrawLeft;
        int _scale = 1;
        int _selected;
        Armed _armed = Armed.None;
        bool _appendRoute;
        Vector2Int _lastMove;
        float _repeatLeft;

        public ConsoleSurface Surface => _surface;

        CrewAgent Selected => _selected < CrewAgent.All.Count ? CrewAgent.All[_selected] : null;

        void Awake()
        {
            Prepare();
            surfaceImage.texture = _surface.Texture;
            hacks.ActiveChanged += OnHackChanged;
        }

        // Mientras se hackea no se ve ninguna camara
        void OnHackChanged(bool active)
        {
            feed.output.enabled = !active;
            feed.feedCamera.enabled = !active;
            if (!active)
                cameras.Show(cameras.Current);
            _armed = Armed.None;
        }

        /// <summary>Publico para poder capturar fuera de Play.</summary>
        public void Prepare()
        {
            if (_surface != null)
                return;
            _surface = new ConsoleSurface(Width, Height);
            BuildLayoutCache();
        }

        void BuildLayoutCache()
        {
            foreach (var room in layout.rooms)
            {
                if (layout.mapBounds.Overlaps(room.area))
                    _cells.Add(new RoomCell { Room = room, Rect = MapRect(room.area) });
            }

            for (var i = 0; i < layout.cameras.Count; i++)
            {
                var camera = layout.cameras[i];
                if (!layout.mapBounds.Contains(new Vector2(camera.focus.x, camera.focus.z)))
                {
                    _cameraButtons.Add(new CameraButton { Index = i, Rect = new RectInt(PanelX, 122, 72, 11) });
                    continue;
                }
                var center = MapPoint(camera.focus);
                _cameraButtons.Add(new CameraButton { Index = i, Rect = new RectInt(center.x - 5, center.y - 5, 11, 11) });
            }

            foreach (var door in layout.doors)
            {
                if (!layout.mapBounds.Contains(new Vector2(door.position.x, door.position.z)))
                {
                    _doorDots.Add(new DoorDot { Door = door, Rect = new RectInt(PanelX - 1, 62, 7, 7) });
                    continue;
                }
                var point = MapPoint(door.position);
                _doorDots.Add(new DoorDot { Door = door, Rect = new RectInt(point.x - 3, point.y - 3, 7, 7) });
            }

            var y = LightsTop;
            foreach (var room in layout.rooms)
            {
                if (!room.lightsHackable)
                    continue;
                _lightRows.Add(new LightRow { Room = room, Rect = new RectInt(PanelX, y, PanelWidth, LightRowHeight) });
                y += LightRowHeight;
            }
            _infraredButton = new RectInt(PanelX + 80, 122, 72, 11);

            _bandTop = y + 8;
            _crewButtons[0] = new RectInt(PanelX, _bandTop + 13, 74, 12);
            _crewButtons[1] = new RectInt(PanelX + 78, _bandTop + 13, 74, 12);

            var row = _bandTop + 50;
            void AddAction(PanelAction action, string label)
            {
                _actions.Add(new ActionButton { Action = action, Label = label, Rect = new RectInt(PanelX, row, PanelWidth, RowHeight - 1) });
                row += RowHeight;
            }
            AddAction(PanelAction.ArmGo, "G  IR A");
            AddAction(PanelAction.Stop, "H  ALTO");
            AddAction(PanelAction.Hide, "J  ESCONDERSE");
            AddAction(PanelAction.ArmKnockout, "N  NOQUEAR");
            AddAction(PanelAction.ArmKill, "K  ELIMINAR");
            AddAction(PanelAction.Retreat, "R  RETIRADA");
            row += 6;
            AddAction(PanelAction.Loop, "B  BUCLE DE CAMARA");
            AddAction(PanelAction.Sensors, "S  SENSORES");
            AddAction(PanelAction.Interfere, "P  INTERFERIR CENTRAL");
            _noteTop = row + 4;
        }

        Vector2Int MapPoint(Vector3 world)
        {
            var bounds = layout.mapBounds;
            return new Vector2Int(PanelX + Mathf.RoundToInt((world.x - bounds.xMin) * MapPixelsPerMeter),
                MapTop + Mathf.RoundToInt((bounds.yMax - world.z) * MapPixelsPerMeter));
        }

        RectInt MapRect(Rect area)
        {
            var bottomLeft = MapPoint(new Vector3(area.xMin, 0f, area.yMin));
            var topRight = MapPoint(new Vector3(area.xMax, 0f, area.yMax));
            return new RectInt(bottomLeft.x, topRight.y, topRight.x - bottomLeft.x, bottomLeft.y - topRight.y);
        }

        void Update()
        {
            // La imagen solo existe durante el golpe y fuera de los hackeos
            var showFeed = director.Started && !director.Finished && !hacks.Active;
            if (feed.output.enabled != showFeed)
            {
                feed.output.enabled = showFeed;
                feed.feedCamera.enabled = showFeed;
            }
            if (director.Started && !director.Finished)
                ReadInput();
            FitToScreen();

            _redrawLeft -= Time.unscaledDeltaTime;
            if (_redrawLeft <= 0f)
            {
                _redrawLeft = RedrawSeconds;
                Redraw();
                _surface.Present();
            }
        }

        void FitToScreen()
        {
            _scale = Mathf.Max(1, Mathf.Min(Screen.width / Width, Screen.height / Height));
            surfaceImage.rectTransform.sizeDelta = new Vector2(Width * _scale, Height * _scale);

            var feedRect = feed.output.rectTransform;
            feedRect.anchorMin = new Vector2(0f, 1f);
            feedRect.anchorMax = new Vector2(0f, 1f);
            feedRect.pivot = new Vector2(0f, 1f);
            feedRect.anchoredPosition = Vector2.zero;
            feedRect.sizeDelta = new Vector2(FeedWidth * _scale, FeedHeight * _scale);
        }

        void UpdatePointer(Mouse mouse)
        {
            var position = mouse.position.ReadValue();
            var originX = (Screen.width - Width * _scale) / 2;
            var originY = (Screen.height - Height * _scale) / 2;
            _pointer = new Vector2Int(Mathf.FloorToInt((position.x - originX) / _scale),
                Height - 1 - Mathf.FloorToInt((position.y - originY) / _scale));
        }

        // Durante un hackeo el teclado y el raton son solo del puzzle
        void FeedHack()
        {
            var input = new HackInput();
            var mouse = Mouse.current;
            if (mouse != null)
            {
                UpdatePointer(mouse);
                input.Click = mouse.leftButton.wasPressedThisFrame;
                input.Held = mouse.leftButton.isPressed;
                // El escaner se puede sintonizar sin dejar el puzzle: es lo unico que queda
                if (input.Click && ScannerRect.Contains(_pointer))
                    radio.TuneNext();
            }
            input.Pointer = _pointer;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame)
                {
                    hacks.Abort();
                    return;
                }

                var x = (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed ? 1 : 0)
                        - (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed ? 1 : 0);
                var y = (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed ? 1 : 0)
                        - (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed ? 1 : 0);
                input.Move = new Vector2Int(x, y);
                input.Step = StepWithRepeat(input.Move);
                input.Confirm = keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame;
                input.Backspace = keyboard.backspaceKey.wasPressedThisFrame;
                input.Typed = TypedCharacter(keyboard);
            }

            hacks.Tick(input);
        }

        Vector2Int StepWithRepeat(Vector2Int move)
        {
            const float firstRepeat = 0.22f;
            const float nextRepeat = 0.07f;

            if (move == Vector2Int.zero)
            {
                _lastMove = move;
                return move;
            }
            if (move != _lastMove)
            {
                _lastMove = move;
                _repeatLeft = firstRepeat;
                return move;
            }

            _repeatLeft -= Time.unscaledDeltaTime;
            if (_repeatLeft > 0f)
                return Vector2Int.zero;
            _repeatLeft = nextRepeat;
            return move;
        }

        static char TypedCharacter(Keyboard keyboard)
        {
            var letters = new[]
            {
                keyboard.aKey, keyboard.bKey, keyboard.cKey, keyboard.dKey, keyboard.eKey, keyboard.fKey, keyboard.gKey, keyboard.hKey,
            };
            for (var i = 0; i < letters.Length; i++)
                if (letters[i].wasPressedThisFrame)
                    return (char)('A' + i);

            var digits = new[]
            {
                keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key, keyboard.digit5Key, keyboard.digit6Key,
            };
            for (var i = 0; i < digits.Length; i++)
                if (digits[i].wasPressedThisFrame)
                    return (char)('1' + i);
            return '\0';
        }

        void ReadInput()
        {
            if (hacks.Active)
            {
                FeedHack();
                return;
            }

            var keyboard = Keyboard.current;
            _appendRoute = keyboard != null && keyboard.shiftKey.isPressed;

            var mouse = Mouse.current;
            if (mouse != null)
            {
                UpdatePointer(mouse);
                if (mouse.leftButton.wasPressedThisFrame)
                    Click(_pointer);
            }

            if (keyboard == null)
                return;

            var digits = new[]
            {
                keyboard.digit0Key, keyboard.digit1Key, keyboard.digit2Key, keyboard.digit3Key, keyboard.digit4Key,
                keyboard.digit5Key, keyboard.digit6Key, keyboard.digit7Key, keyboard.digit8Key,
            };
            for (var i = 0; i < digits.Length; i++)
                if (digits[i].wasPressedThisFrame)
                    cameras.Show(i);

            if (keyboard.iKey.wasPressedThisFrame)
                cameras.ToggleInfrared();
            if (keyboard.tabKey.wasPressedThisFrame)
                SelectCrew((_selected + 1) % Mathf.Max(CrewAgent.All.Count, 1));
            if (keyboard.escapeKey.wasPressedThisFrame)
                _armed = Armed.None;
            if (keyboard.gKey.wasPressedThisFrame)
                Perform(PanelAction.ArmGo);
            if (keyboard.hKey.wasPressedThisFrame)
                Perform(PanelAction.Stop);
            if (keyboard.jKey.wasPressedThisFrame)
                Perform(PanelAction.Hide);
            if (keyboard.nKey.wasPressedThisFrame)
                Perform(PanelAction.ArmKnockout);
            if (keyboard.kKey.wasPressedThisFrame)
                Perform(PanelAction.ArmKill);
            if (keyboard.rKey.wasPressedThisFrame)
                Perform(PanelAction.Retreat);
            if (keyboard.bKey.wasPressedThisFrame)
                Perform(PanelAction.Loop);
            if (keyboard.sKey.wasPressedThisFrame)
                Perform(PanelAction.Sensors);
            if (keyboard.pKey.wasPressedThisFrame)
                Perform(PanelAction.Interfere);
            if (keyboard.cKey.wasPressedThisFrame)
                radio.TuneNext();
            if (keyboard.tKey.wasPressedThisFrame)
                radio.ShowTranscript = !radio.ShowTranscript;
        }

        void SelectCrew(int index)
        {
            _selected = index;
            _armed = Armed.None;
        }

        void Perform(PanelAction action)
        {
            var crew = Selected;
            switch (action)
            {
                case PanelAction.ArmGo:
                    _armed = _armed == Armed.Go ? Armed.None : Armed.Go;
                    break;
                case PanelAction.Stop:
                    crew?.Issue(OrderKind.Stop);
                    _armed = Armed.None;
                    break;
                case PanelAction.Hide:
                    crew?.Issue(OrderKind.Hide);
                    _armed = Armed.None;
                    break;
                case PanelAction.ArmKnockout:
                    if (crew != null && crew.canFight)
                        _armed = _armed == Armed.Knockout ? Armed.None : Armed.Knockout;
                    break;
                case PanelAction.ArmKill:
                    if (crew != null && crew.canFight)
                        _armed = _armed == Armed.Kill ? Armed.None : Armed.Kill;
                    break;
                case PanelAction.Retreat:
                    crew?.Issue(OrderKind.Retreat);
                    _armed = Armed.None;
                    break;
                case PanelAction.Loop:
                    if (cameras.CurrentDef != null && cameras.CurrentDef.watchedByOperator)
                        hacks.LoopCamera(cameras.Current);
                    break;
                case PanelAction.Sensors:
                    hacks.DisableSensors();
                    break;
                case PanelAction.Interfere:
                    hacks.Interfere();
                    break;
            }
        }

        /// <summary>Publico para las pruebas: un clic en coordenadas de la consola.</summary>
        public void Click(Vector2Int point)
        {
            if (hacks.Active)
                return;

            foreach (var button in _cameraButtons)
            {
                if (!button.Rect.Contains(point))
                    continue;
                cameras.Show(button.Index);
                return;
            }

            if (_infraredButton.Contains(point))
            {
                cameras.ToggleInfrared();
                return;
            }

            foreach (var row in _lightRows)
            {
                if (!row.Rect.Contains(point))
                    continue;
                hacks.ToggleLights(row.Room.id);
                return;
            }

            for (var i = 0; i < _crewButtons.Length; i++)
            {
                if (!_crewButtons[i].Contains(point))
                    continue;
                SelectCrew(i);
                return;
            }

            foreach (var action in _actions)
            {
                if (!action.Rect.Contains(point))
                    continue;
                Perform(action.Action);
                return;
            }

            foreach (var dot in _doorDots)
            {
                if (!dot.Rect.Contains(point))
                    continue;
                hacks.OperateDoor(dot.Door);
                return;
            }

            if (_armed == Armed.Go)
            {
                foreach (var cell in _cells)
                {
                    if (!cell.Rect.Contains(point))
                        continue;
                    var center = cell.Room.area.center;
                    SendGo(new Vector3(center.x, 0f, center.y));
                    return;
                }
            }

            if (ScannerRect.Contains(point))
            {
                radio.TuneNext();
                return;
            }

            if (point.x >= 0 && point.x < FeedWidth && point.y >= 0 && point.y < FeedHeight)
                ClickFeed(point);
        }

        static RectInt ScannerRect => new(404, FeedHeight + 4, 396, Height - FeedHeight - 8);

        public Vector2Int ScannerCenter => Vector2Int.RoundToInt(ScannerRect.center);

        void SendGo(Vector3 destination)
        {
            Selected?.Issue(OrderKind.Go, destination, null, _appendRoute);
            if (!_appendRoute)
                _armed = Armed.None;
        }

        void ClickFeed(Vector2Int point)
        {
            var viewport = new Vector2((point.x + 0.5f) / FeedWidth, 1f - (point.y + 0.5f) / FeedHeight);

            if (_armed is Armed.Knockout or Armed.Kill)
            {
                var guard = GuardUnderPointer(viewport);
                if (guard == null)
                    return;
                Selected?.Issue(_armed == Armed.Kill ? OrderKind.Kill : OrderKind.Knockout, default, guard);
                _armed = Armed.None;
                return;
            }

            if (_armed == Armed.Go)
            {
                var ray = feed.feedCamera.ViewportPointToRay(viewport);
                var ground = new Plane(Vector3.up, Vector3.zero);
                if (!ground.Raycast(ray, out var enter))
                    return;
                var hit = ray.GetPoint(enter);
                if (cameras.CurrentDef.view.Contains(new Vector2(hit.x, hit.z)))
                    SendGo(hit);
                return;
            }

            for (var i = 0; i < CrewAgent.All.Count; i++)
            {
                var crew = CrewAgent.All[i];
                if (!crew.IsOut && IsVisibleOnFeed(crew.transform.position, false) && IsUnderPointer(crew.transform.position, viewport))
                {
                    SelectCrew(i);
                    return;
                }
            }
        }

        // Solo se puede senalar lo que se ve: a oscuras, unicamente al que lleva linterna
        bool IsVisibleOnFeed(Vector3 position, bool carriesLight)
        {
            var camera = cameras.CurrentDef;
            if (camera == null || !camera.view.Contains(new Vector2(position.x, position.z)))
                return false;
            return cameras.InfraredOn || carriesLight || lighting.IsLitAt(position);
        }

        GuardAgent GuardUnderPointer(Vector2 viewport)
        {
            foreach (var guard in GuardAgent.All)
            {
                if (guard.isActiveAndEnabled && !guard.IsDown && IsVisibleOnFeed(guard.transform.position, true)
                    && IsUnderPointer(guard.transform.position, viewport))
                    return guard;
            }
            return null;
        }

        bool IsUnderPointer(Vector3 feet, Vector2 viewport)
        {
            var bottom = feed.feedCamera.WorldToViewportPoint(feet);
            var top = feed.feedCamera.WorldToViewportPoint(feet + Vector3.up * 2f);
            return Mathf.Abs(viewport.x - bottom.x) <= PersonHalfWidth && viewport.y >= bottom.y - 0.01f && viewport.y <= top.y + 0.01f;
        }

        // --- Ayudas para las pruebas ------------------------------------------------------

        public Vector2Int LightRowCenter(string roomId)
        {
            foreach (var row in _lightRows)
                if (row.Room.id == roomId)
                    return Vector2Int.RoundToInt(row.Rect.center);
            return new Vector2Int(-1, -1);
        }

        public Vector2Int InfraredButtonCenter => Vector2Int.RoundToInt(_infraredButton.center);

        public Vector2Int CameraButtonCenter(int index)
        {
            foreach (var button in _cameraButtons)
                if (button.Index == index)
                    return Vector2Int.RoundToInt(button.Rect.center);
            return new Vector2Int(-1, -1);
        }

        public Vector2Int CrewButtonCenter(int index) => Vector2Int.RoundToInt(_crewButtons[index].center);

        public Vector2Int ActionCenter(string label)
        {
            foreach (var action in _actions)
                if (action.Label.Contains(label))
                    return Vector2Int.RoundToInt(action.Rect.center);
            return new Vector2Int(-1, -1);
        }

        public Vector2Int DoorCenter(string doorId)
        {
            foreach (var dot in _doorDots)
                if (dot.Door.id == doorId)
                    return Vector2Int.RoundToInt(dot.Rect.center);
            return new Vector2Int(-1, -1);
        }

        public Vector2Int RoomCenter(string roomId)
        {
            foreach (var cell in _cells)
                if (cell.Room.id == roomId)
                {
                    // El centro suele estar bajo el boton de la camara
                    var rect = cell.Rect;
                    return new Vector2Int(rect.xMin + 3, rect.yMax - 3);
                }
            return new Vector2Int(-1, -1);
        }

        /// <summary>El pixel de la consola donde cae un punto del mundo con la camara activa.</summary>
        public Vector2Int FeedPixel(Vector3 world)
        {
            var viewport = feed.feedCamera.WorldToViewportPoint(world);
            return new Vector2Int(Mathf.FloorToInt(viewport.x * FeedWidth), Mathf.FloorToInt((1f - viewport.y) * FeedHeight));
        }

        // --- Dibujo ----------------------------------------------------------------------

        public void Redraw()
        {
            _surface.Clear(Background);
            DrawMap();
            DrawLights();
            DrawBand();
            DrawRadio();
            DrawStreetNote();
            hacks.Draw(_surface);
            if (!director.Started)
                DrawTitle();
            else if (director.Finished)
                DrawReport(director.Report);
        }

        void DrawTitle()
        {
            _surface.Fill(0, 0, FeedWidth, FeedHeight, Black);
            _surface.Text("THE EYE IN THE SKY", 112, 130, Amber, 5);
            _surface.Text("TU ERES SUS OJOS. ELLOS HACEN EL RESTO.", 170, 196, Text, 2);
            _surface.Text("LA POLICIA SE OYE EN LA RADIO. NO SALE EN PANTALLA.", 94, 232, Dim, 2);
            _surface.Text("ESPACIO  EMPEZAR", 304, 310, Text, 2);
        }

        void DrawReport(HeistReport report)
        {
            const int rowHeight = 13;
            const int maxRows = 24;

            _surface.Fill(0, 0, FeedWidth, FeedHeight, Black);
            var titleColor = director.Outcome == HeistOutcome.Won ? Amber : Red;
            _surface.Text(report.Title, 20, 16, titleColor, 3);
            _surface.Text(report.Headline, 20, 50, Text, 1);
            _surface.Text(report.Stats, 20, 66, Dim, 1);

            var y = 90;
            var first = Mathf.Max(0, report.Timeline.Count - maxRows);
            for (var i = first; i < report.Timeline.Count; i++)
            {
                var entry = report.Timeline[i];
                _surface.Text(HeistReport.ClockAt(entry.Seconds), 20, y, Dim);
                _surface.Text(entry.Text.ToUpperInvariant(), 80, y, entry.Important ? Amber : Text);
                y += rowHeight;
            }

            _surface.Text("R  OTRA VEZ", 680, 430, Text, 1);
        }

        // Una nota de papel fija: no marca nada, hay que saber escuchar para usarla
        void DrawStreetNote()
        {
            var paper = new Color32(216, 208, 182, 255);
            var ink = new Color32(46, 42, 54, 255);
            var note = new RectInt(PanelX, _noteTop, PanelWidth, Height - _noteTop - 6);
            _surface.Fill(note, paper);

            var streets = new[] { "COMISARIA", "AV. DEL PUERTO", "PUENTE MAYOR", "PLAZA DEL RELOJ", "CALLE FERRER" };
            var y = note.y + 6;
            for (var i = 0; i < streets.Length; i++)
            {
                _surface.Text(streets[i], note.x + 14, y, ink);
                _surface.Fill(note.x + 6, y + 2, 3, 3, ink);
                if (i < streets.Length - 1)
                    _surface.Fill(note.x + 7, y + 6, 1, 10, ink);
                y += 15;
            }
            _surface.Text("AQUI ESTAMOS", note.x + 14, y - 15 + 9, new Color32(160, 40, 36, 255));
        }


        void DrawMap()
        {
            _surface.Fill(PanelX - 2, 4, PanelWidth + 4, 132, Panel);
            _surface.Text("PLANO", PanelX, 7, Dim);

            string hoverText = null;
            foreach (var cell in _cells)
            {
                var hovered = cell.Rect.Contains(_pointer);
                var fill = !lighting.IsLit(cell.Room.id) ? Black : hovered ? RoomHover : RoomLit;
                _surface.Fill(cell.Rect, fill);
                _surface.Frame(cell.Rect, Line);
                if (hovered)
                    hoverText = cell.Room.label;
            }

            foreach (var dot in _doorDots)
            {
                if (!layout.mapBounds.Contains(new Vector2(dot.Door.position.x, dot.Door.position.z)))
                    continue;
                var center = Vector2Int.RoundToInt(dot.Rect.center);
                var open = doors.IsOpen(dot.Door.id);
                _surface.Fill(center.x - 1, center.y - 1, 3, 3, open ? Text : Amber);
                if (dot.Rect.Contains(_pointer))
                    hoverText = $"{DoorKindLabel(dot.Door)} {(open ? "ABIERTA" : "CERRADA")}";
            }

            foreach (var button in _cameraButtons)
            {
                var active = button.Index == cameras.Current;
                var isStreet = button.Rect.width > 11;
                _surface.Fill(button.Rect, active ? Amber : Line);
                var color = active ? Background : Text;
                if (isStreet)
                    _surface.Text($"0 {layout.cameras[button.Index].label}", button.Rect.x + 3, button.Rect.y + 2, color);
                else
                    _surface.Text(button.Index.ToString(), button.Rect.x + 3, button.Rect.y + 2, color);
                if (button.Rect.Contains(_pointer))
                    hoverText = $"CAM {button.Index:00} {layout.cameras[button.Index].label}";
            }

            _surface.Text(hoverText ?? string.Empty, PanelX, 108, Text);

            var camera = cameras.CurrentDef;
            var hasInfrared = camera != null && camera.infrared;
            _surface.Fill(_infraredButton, cameras.InfraredOn ? Amber : hasInfrared ? Line : Panel);
            _surface.Frame(_infraredButton, Line);
            _surface.Text("I  IR", _infraredButton.x + 3, _infraredButton.y + 2,
                cameras.InfraredOn ? Background : hasInfrared ? Text : Dim);
        }

        static string DoorKindLabel(BuildingLayout.DoorDef door) => door.kind switch
        {
            BuildingLayout.DoorKind.Mechanical => "CERRADURA",
            BuildingLayout.DoorKind.Electronic => "ELECTRONICA",
            BuildingLayout.DoorKind.Biometric => "BIOMETRICA",
            BuildingLayout.DoorKind.Roller => "PERSIANA",
            _ => "ACORAZADA",
        };

        void DrawLights()
        {
            _surface.Fill(PanelX - 2, LightsTop - 12, PanelWidth + 4, 12 + _lightRows.Count * LightRowHeight + 2, Panel);
            _surface.Text("LUCES", PanelX, LightsTop - 10, Dim);

            foreach (var row in _lightRows)
            {
                if (row.Rect.Contains(_pointer))
                    _surface.Fill(row.Rect, RoomHover);
                var lit = lighting.IsLit(row.Room.id);
                var locked = hacks.LockSecondsLeft(HackSession.LightsId(row.Room.id));
                _surface.Text(row.Room.label, row.Rect.x + 2, row.Rect.y + 2, Text);
                if (locked > 0f)
                    _surface.Text(Mathf.CeilToInt(locked).ToString(), row.Rect.x + PanelWidth - 20, row.Rect.y + 2, Red);
                else
                    _surface.Text(lit ? "ON" : "OFF", row.Rect.x + PanelWidth - 20, row.Rect.y + 2, lit ? Text : Red);
            }
        }

        void DrawBand()
        {
            _surface.Fill(PanelX - 2, _bandTop, PanelWidth + 4, Height - _bandTop - 4, Panel);
            _surface.Text("BANDA", PanelX, _bandTop + 3, Dim);

            for (var i = 0; i < _crewButtons.Length && i < CrewAgent.All.Count; i++)
            {
                var crew = CrewAgent.All[i];
                var selected = i == _selected;
                var rect = _crewButtons[i];
                _surface.Fill(rect, selected ? Amber : rect.Contains(_pointer) ? RoomHover : Line);
                var color = crew.IsOut && crew.State == CrewState.Down ? Red : selected ? Background : Text;
                _surface.Text(crew.displayName, rect.x + 3, rect.y + 3, color);
            }

            var chosen = Selected;
            if (chosen != null)
                _surface.Text(chosen.StatusText, PanelX, _bandTop + 29, chosen.State == CrewState.Blocked ? Amber : Text);

            var hint = _armed switch
            {
                Armed.Go => "SALA O SUELO. SHIFT: RUTA",
                Armed.Knockout or Armed.Kill => "SENALA UN GUARDIA",
                _ => string.Empty,
            };
            _surface.Text(hint, PanelX, _bandTop + 39, Amber);

            foreach (var action in _actions)
                DrawAction(action, chosen);
        }

        void DrawAction(ActionButton action, CrewAgent crew)
        {
            var enabled = action.Action switch
            {
                PanelAction.ArmKnockout or PanelAction.ArmKill => crew != null && crew.canFight,
                PanelAction.Loop => cameras.CurrentDef != null && cameras.CurrentDef.watchedByOperator
                                    && hacks.LockSecondsLeft(HackSession.CameraId(cameras.Current)) <= 0f,
                PanelAction.Sensors => sensors.Armed && hacks.SensorsLockSeconds <= 0f,
                PanelAction.Interfere => !hacks.InterferenceUsed,
                _ => true,
            };
            var armed = (action.Action == PanelAction.ArmGo && _armed == Armed.Go)
                        || (action.Action == PanelAction.ArmKnockout && _armed == Armed.Knockout)
                        || (action.Action == PanelAction.ArmKill && _armed == Armed.Kill);

            if (armed)
                _surface.Fill(action.Rect, Amber);
            else if (enabled && action.Rect.Contains(_pointer))
                _surface.Fill(action.Rect, RoomHover);
            _surface.Frame(action.Rect, Line);

            var label = action.Label;
            if (action.Action == PanelAction.Loop)
            {
                var locked = hacks.LockSecondsLeft(HackSession.CameraId(cameras.Current));
                if (locked > 0f)
                    label = $"B  BLOQUEADO {Mathf.CeilToInt(locked)} S";
                else if (cameras.IsLooped(cameras.Current))
                    label = $"B  BUCLE {Mathf.CeilToInt(cameras.LoopSecondsLeft(cameras.Current))} S";
            }
            else if (action.Action == PanelAction.Sensors)
            {
                if (!sensors.Armed)
                    label = $"S  SENSORES OFF {Mathf.CeilToInt(sensors.OffSecondsLeft)} S";
                else if (hacks.SensorsLockSeconds > 0f)
                    label = $"S  BLOQUEADO {Mathf.CeilToInt(hacks.SensorsLockSeconds)} S";
            }
            else if (action.Action == PanelAction.Interfere && hacks.InterferenceUsed)
            {
                label = "P  CENTRAL YA USADA";
            }
            _surface.Text(label, action.Rect.x + 3, action.Rect.y + 2, armed ? Background : enabled ? Text : Dim);
        }

        void DrawRadio()
        {
            DrawChannel(4, "RADIO BANDA", RadioChannel.Crew);
            var scannerTitle = radio.Tuned == RadioChannel.Police ? "ESCANER  POLICIA" : "ESCANER  SEGURIDAD";
            DrawChannel(404, scannerTitle, radio.Tuned);
            _surface.Text("C CAMBIAR FRECUENCIA", 404 + 260, FeedHeight + 7, Dim);
            _surface.Text(radio.ShowTranscript ? "T TEXTO SI" : "T TEXTO NO", 4 + 320, FeedHeight + 7, Dim);
        }

        void DrawChannel(int x, string title, RadioChannel channel)
        {
            _surface.Fill(x, FeedHeight + 4, 396, Height - FeedHeight - 8, Panel);
            _surface.Text(title, x + 4, FeedHeight + 7, Dim);

            var line = radio.Current(channel);
            if (line == null)
                return;

            // Sin texto solo queda saber que alguien esta hablando
            _surface.Fill(x + 386, FeedHeight + 8, 5, 5, Amber);
            if (!radio.ShowTranscript)
                return;

            _surface.Text(line.Speaker, x + 4, FeedHeight + 22, Amber);
            var y = FeedHeight + 34;
            foreach (var wrapped in Wrap(line.Text.ToUpperInvariant(), RadioColumns))
            {
                _surface.Text(wrapped, x + 4, y, Text);
                y += 10;
            }
        }

        static IEnumerable<string> Wrap(string text, int columns)
        {
            var start = 0;
            while (text.Length - start > columns)
            {
                var cut = text.LastIndexOf(' ', start + columns, columns);
                if (cut <= start)
                    cut = start + columns;
                yield return text.Substring(start, cut - start);
                start = cut + 1;
            }
            yield return text.Substring(start);
        }
    }
}
