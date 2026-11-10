using System.Collections.Generic;
using EyeSky.Core;
using EyeSky.Radio;
using UnityEngine;

namespace EyeSky.Npc
{
    /// <summary>
    /// La vigilancia en conjunto: el operador mirando monitores, los controles de radio
    /// y quien va a arreglar un apagon.
    /// </summary>
    public sealed class GuardRoster : MonoBehaviour
    {
        const string BaseSpeaker = "BASE";
        const float ReassignSeconds = 5f;
        const float CameraDecay = 2f;
        static readonly string[] NumberWords = { "cero", "uno", "dos", "tres", "cuatro", "cinco", "seis", "siete", "ocho" };

        sealed class Blackout
        {
            public string RoomId;
            public float DelayLeft;
            public GuardAgent Assigned;
        }

        public static GuardRoster Instance { get; private set; }

        readonly Dictionary<CrewAgent, float> _cameraClock = new();
        readonly List<Blackout> _blackouts = new();
        float _checkLeft;

        static NpcWorld World => NpcWorld.Instance;
        static NpcSettings Settings => World.settings;

        void Awake()
        {
            Instance = this;
        }

        void OnEnable()
        {
            World.lighting.LitChanged += OnLitChanged;
        }

        void OnDisable()
        {
            World.lighting.LitChanged -= OnLitChanged;
        }

        void Start()
        {
            _checkLeft = Settings.checkIntervalSeconds;
        }

        GuardAgent Operator => GuardAgent.All.Find(g => g.isOperator && g.isActiveAndEnabled);

        void Update()
        {
            _checkLeft -= Time.deltaTime;
            if (_checkLeft <= 0f)
                RunCheckNow();

            TickCameras();
            TickBlackouts();
        }

        // --- Monitores -----------------------------------------------------------------

        void TickCameras()
        {
            var watcher = Operator;
            var watching = watcher != null && !watcher.IsDown;
            foreach (var crew in CrewAgent.All)
            {
                _cameraClock.TryGetValue(crew, out var clock);
                var cameraIndex = watching ? VisibleOnCamera(crew) : -1;
                if (cameraIndex < 0)
                {
                    _cameraClock[crew] = Mathf.Max(0f, clock - Time.deltaTime * CameraDecay);
                    continue;
                }

                clock += Time.deltaTime;
                if (clock >= Settings.cameraDetectSeconds)
                {
                    clock = 0f;
                    watcher.ReportFromCamera(crew, $"la cámara {NumberWords[cameraIndex]}");
                }
                _cameraClock[crew] = clock;
            }
        }

        /// <summary>El numero de la camara que le esta grabando, o -1.</summary>
        static int VisibleOnCamera(CrewAgent crew)
        {
            if (crew.IsOut || crew.Hidden || !World.lighting.IsLitAt(crew.transform.position))
                return -1;

            var cameras = World.layout.cameras;
            var point = new Vector2(crew.transform.position.x, crew.transform.position.z);
            for (var i = 0; i < cameras.Count; i++)
            {
                var camera = cameras[i];
                if (camera.watchedByOperator && camera.roomId == crew.Motor.CurrentRoom
                    && camera.view.Contains(point) && !World.cameras.IsLooped(i))
                    return i;
            }
            return -1;
        }

        // --- Controles de radio --------------------------------------------------------

        public void RunCheckNow()
        {
            _checkLeft = Settings.checkIntervalSeconds;
            var watcher = Operator;
            if (watcher != null && watcher.IsDown)
            {
                ReportMissing(watcher, "check_base_silent");
                return;
            }

            RadioBus.Say(RadioChannel.Security, BaseSpeaker, "check_call");
            foreach (var guard in GuardAgent.All)
            {
                if (!guard.isActiveAndEnabled || guard.isOperator)
                    continue;
                if (guard.IsDown)
                    ReportMissing(guard, "check_miss");
                else
                    RadioBus.Say(RadioChannel.Security, guard.displayName, "check_ok");
            }
        }

        void ReportMissing(GuardAgent missing, string lineKey)
        {
            RadioBus.Say(RadioChannel.Security, BaseSpeaker, lineKey, missing.displayName);
            HeistEvents.Raise(HeistEventKind.MissedCheck, missing.transform.position, missing.displayName);
            DispatchNearest(missing.transform.position);
        }

        public void DispatchNearest(Vector3 point)
        {
            var guard = NearestAvailable(point);
            if (guard != null)
                guard.Investigate(point);
        }

        static GuardAgent NearestAvailable(Vector3 point)
        {
            GuardAgent best = null;
            var bestDistance = float.MaxValue;
            foreach (var guard in GuardAgent.All)
            {
                if (!guard.isActiveAndEnabled || guard.isOperator || guard.IsBusy)
                    continue;
                var distance = (guard.transform.position - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    best = guard;
                    bestDistance = distance;
                }
            }
            return best;
        }

        public void OnGunshot(Vector3 position)
        {
            foreach (var guard in GuardAgent.All)
                if (guard.isActiveAndEnabled && !guard.isOperator
                    && Vector3.Distance(guard.transform.position, position) <= Settings.gunshotRadius)
                    guard.Investigate(position);
        }

        // --- Apagones ------------------------------------------------------------------

        void OnLitChanged(string roomId, bool lit)
        {
            if (lit)
            {
                _blackouts.RemoveAll(b => b.RoomId == roomId);
                return;
            }

            var room = World.layout.Room(roomId);
            HeistEvents.Raise(HeistEventKind.Blackout, room.area.center, roomId);
            _blackouts.Add(new Blackout
            {
                RoomId = roomId,
                DelayLeft = Random.Range(Settings.restoreDelayMin, Settings.restoreDelayMax),
            });
        }

        void TickBlackouts()
        {
            foreach (var blackout in _blackouts)
            {
                if (blackout.Assigned != null && blackout.Assigned.RestoringRoom == blackout.RoomId)
                    continue;

                // Si al que iba le ha pasado algo, otro tomara el relevo
                if (blackout.Assigned != null)
                {
                    blackout.Assigned = null;
                    blackout.DelayLeft = ReassignSeconds;
                }

                blackout.DelayLeft -= Time.deltaTime;
                if (blackout.DelayLeft > 0f)
                    continue;

                var guard = NearestAvailable(World.fuseBoxPoint);
                if (guard == null)
                {
                    blackout.DelayLeft = ReassignSeconds;
                    continue;
                }
                guard.GoRestoreLights(blackout.RoomId);
                blackout.Assigned = guard;
            }
        }
    }
}
