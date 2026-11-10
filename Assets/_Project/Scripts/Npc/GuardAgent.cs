using System.Collections.Generic;
using EyeSky.Core;
using EyeSky.Feed;
using EyeSky.Radio;
using EyeSky.Security;
using UnityEngine;

namespace EyeSky.Npc
{
    public enum GuardState
    {
        Patrol,
        Investigate,
        Looking,
        Reporting,
        Chase,
        Restoring,
        Held,
        Down,
    }

    /// <summary>Un guardia: patrulla, ve y oye de verdad, y avisa por radio si encuentra algo.</summary>
    [RequireComponent(typeof(ActorMotor))]
    [RequireComponent(typeof(IsoPuppet))]
    public sealed class GuardAgent : MonoBehaviour
    {
        public static readonly List<GuardAgent> All = new();

        public string displayName;
        public bool isOperator;
        public Vector3[] patrol;

        ActorMotor _motor;
        IsoPuppet _puppet;
        Flashlight _flashlight;

        readonly List<BuildingLayout.DoorDef> _openedByMe = new();
        readonly HashSet<GuardAgent> _bodiesReported = new();

        int _patrolIndex;
        float _pauseLeft;
        float _spot;
        float _hear;
        CrewAgent _target;
        Vector3 _lastSeen;
        float _reportLeft;
        string _reportCamera;
        float _captureClock;
        float _refreshLeft;
        float _lookLeft;
        float _lookTurn;
        float _wakeLeft;
        float _workLeft;
        string _restoreRoom;
        GuardState _beforeHold;

        public GuardState State { get; private set; } = GuardState.Patrol;
        public ActorMotor Motor => _motor;
        public Vector3 Facing => _puppet.Facing;
        public bool IsDown => State == GuardState.Down;
        public bool Dead { get; private set; }
        public bool IsBusy => State is GuardState.Reporting or GuardState.Chase or GuardState.Restoring or GuardState.Held or GuardState.Down;
        public string RestoringRoom => State == GuardState.Restoring ? _restoreRoom : null;

        static NpcWorld World => NpcWorld.Instance;
        static NpcSettings Settings => World.settings;

        void Awake()
        {
            _motor = GetComponent<ActorMotor>();
            _puppet = GetComponent<IsoPuppet>();
            _flashlight = GetComponentInChildren<Flashlight>();
            _motor.DoorCrossed += OnDoorCrossed;
            All.Add(this);
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        void Start()
        {
            _motor.SetSpeed(Settings.patrolSpeed);
            if (patrol != null && patrol.Length > 0)
            {
                // Cada partida empieza por otro punto, con pausas distintas
                _patrolIndex = Random.Range(0, patrol.Length);
                _pauseLeft = Random.Range(0f, Settings.patrolPauseMax);
            }
        }

        void Say(string key, params object[] args) => RadioBus.Say(RadioChannel.Security, displayName, key, args);

        static int DoorCost(BuildingLayout.DoorDef door) => 1;

        void OnDoorCrossed(BuildingLayout.DoorDef door)
        {
            if (_openedByMe.Remove(door))
                World.doors.SetOpen(door.id, false);
        }

        // --- Lo que le pasa --------------------------------------------------------------

        /// <summary>Se queda quieto mientras le reducen. Corta el aviso que estuviera dando.</summary>
        public void Hold()
        {
            if (IsDown)
                return;
            _beforeHold = State;
            State = GuardState.Held;
            _motor.Stop();
        }

        public void Neutralize(bool lethal)
        {
            State = GuardState.Down;
            Dead = lethal;
            _wakeLeft = Settings.wakeSeconds;
            _motor.Stop();
            _puppet.Downed = true;
            if (_flashlight != null)
                _flashlight.enabled = false;
        }

        public void Wake()
        {
            State = GuardState.Patrol;
            _puppet.Downed = false;
            if (_flashlight != null)
                _flashlight.enabled = true;
            Say("woke");
            HeistEvents.Raise(HeistEventKind.AlarmConfirmed, transform.position, $"{displayName} despierta");
        }

        /// <summary>Le han visto venir: avisa al momento, sin ventana.</summary>
        public void ReportNow(CrewAgent intruder)
        {
            Say("alarm_saw", World.layout.Room(_motor.CurrentRoom).spoken);
            HeistEvents.Raise(HeistEventKind.AlarmConfirmed, transform.position, $"{displayName} ve a {intruder.displayName}");
        }

        /// <summary>El operador ha visto algo en un monitor.</summary>
        public void ReportFromCamera(CrewAgent intruder, string cameraLabel)
        {
            if (IsBusy)
                return;
            BeginReport(intruder, cameraLabel);
            Say("alarm_cam", cameraLabel);
        }

        void BeginReport(CrewAgent intruder, string cameraLabel)
        {
            _target = intruder;
            _lastSeen = intruder.transform.position;
            _reportCamera = cameraLabel;
            _reportLeft = Settings.reportSeconds;
            State = GuardState.Reporting;
            _motor.Stop();
        }

        public void Investigate(Vector3 point)
        {
            if (IsBusy)
                return;
            _motor.SetSpeed(Settings.patrolSpeed * 1.4f);
            _motor.Go(point, DoorCost);
            State = GuardState.Investigate;
        }

        public void GoRestoreLights(string roomId)
        {
            if (IsBusy)
                return;
            _restoreRoom = roomId;
            _workLeft = Settings.restoreWorkSeconds;
            _motor.SetSpeed(Settings.patrolSpeed * 1.4f);
            _motor.Go(World.fuseBoxPoint, DoorCost);
            State = GuardState.Restoring;
            Say("lights_go");
        }

        /// <summary>Para las pruebas: lo planta en un sitio mirando hacia un lado.</summary>
        public void StandAt(Vector3 position, Vector3 facing)
        {
            patrol = System.Array.Empty<Vector3>();
            State = GuardState.Patrol;
            _spot = 0f;
            _hear = 0f;
            _motor.Warp(position);
            _puppet.Face(facing);
        }

        // --- Bucle ---------------------------------------------------------------------

        void Update()
        {
            if (State == GuardState.Down)
            {
                if (!Dead)
                {
                    _wakeLeft -= Time.deltaTime;
                    if (_wakeLeft <= 0f)
                        Wake();
                }
                return;
            }
            if (State == GuardState.Held)
                return;

            if (_motor.Status == ActorMotor.MotorStatus.Blocked)
            {
                var door = _motor.BlockedDoor;
                if (!World.doors.IsLockedAgainstGuards(door.id))
                {
                    World.doors.SetOpen(door.id, true);
                    _openedByMe.Add(door);
                    _motor.Resume();
                }
            }

            if (State != GuardState.Reporting)
            {
                Watch();
                if (State == GuardState.Patrol)
                    Listen();
            }

            switch (State)
            {
                case GuardState.Patrol:
                    TickPatrol();
                    break;
                case GuardState.Investigate:
                    if (_motor.Status == ActorMotor.MotorStatus.Idle)
                        StartLooking();
                    break;
                case GuardState.Looking:
                    TickLooking();
                    break;
                case GuardState.Reporting:
                    TickReporting();
                    break;
                case GuardState.Chase:
                    TickChase();
                    break;
                case GuardState.Restoring:
                    TickRestoring();
                    break;
            }
        }

        void TickPatrol()
        {
            if (patrol == null || patrol.Length == 0 || _motor.Status != ActorMotor.MotorStatus.Idle)
                return;

            _pauseLeft -= Time.deltaTime;
            if (_pauseLeft > 0f)
                return;

            _pauseLeft = Random.Range(Settings.patrolPauseMin, Settings.patrolPauseMax);
            _motor.SetSpeed(Settings.patrolSpeed);
            _motor.Go(patrol[_patrolIndex], DoorCost);
            _patrolIndex = (_patrolIndex + 1) % patrol.Length;
        }

        void StartLooking()
        {
            State = GuardState.Looking;
            _lookLeft = Settings.lookAroundSeconds;
            _lookTurn = 0f;
        }

        void TickLooking()
        {
            _lookLeft -= Time.deltaTime;
            _lookTurn -= Time.deltaTime;
            if (_lookTurn <= 0f)
            {
                _lookTurn = 1f;
                _puppet.Face(Quaternion.Euler(0f, 90f, 0f) * Facing);
            }
            if (_lookLeft <= 0f)
                State = GuardState.Patrol;
        }

        void TickReporting()
        {
            if (_target != null)
                _puppet.Face(_target.transform.position - transform.position);

            _reportLeft -= Time.deltaTime;
            if (_reportLeft > 0f)
                return;

            HeistEvents.Raise(HeistEventKind.AlarmConfirmed, transform.position,
                _reportCamera != null ? $"{displayName} en {_reportCamera}" : $"{displayName} ve a {_target.displayName}");

            if (isOperator)
            {
                GuardRoster.Instance.DispatchNearest(_lastSeen);
                _reportCamera = null;
                State = GuardState.Patrol;
                return;
            }

            _reportCamera = null;
            _captureClock = 0f;
            _refreshLeft = 0f;
            _motor.SetSpeed(Settings.chaseSpeed);
            State = GuardState.Chase;
        }

        void TickChase()
        {
            var seesTarget = _target != null && !_target.IsOut && CanSeeNow(_target);
            if (seesTarget)
            {
                _lastSeen = _target.transform.position;
                if (Vector3.Distance(transform.position, _lastSeen) <= Settings.captureDistance)
                {
                    _captureClock += Time.deltaTime;
                    if (_captureClock >= Settings.captureSeconds)
                    {
                        _target.Capture();
                        StartLooking();
                        return;
                    }
                }
                else
                {
                    _captureClock = 0f;
                }
            }

            _refreshLeft -= Time.deltaTime;
            if (_refreshLeft <= 0f)
            {
                _refreshLeft = 0.4f;
                if (seesTarget || _motor.Status == ActorMotor.MotorStatus.Idle)
                {
                    if (!seesTarget && Vector3.Distance(transform.position, _lastSeen) < 1f)
                    {
                        StartLooking();
                        return;
                    }
                    _motor.Go(_lastSeen, DoorCost);
                }
            }
        }

        void TickRestoring()
        {
            if (_motor.Status != ActorMotor.MotorStatus.Idle)
                return;
            _workLeft -= Time.deltaTime;
            if (_workLeft > 0f)
                return;
            World.lighting.SetLit(_restoreRoom, true);
            Say("lights_done");
            State = GuardState.Patrol;
        }

        // --- Sentidos ------------------------------------------------------------------

        // Lo mismo que lee la imagen: a oscuras solo existe lo que cae dentro de la linterna
        bool InSight(Vector3 position, string room, bool hidden, out float distance)
        {
            distance = Vector3.Distance(transform.position, position);
            if (hidden && distance > Settings.hiddenFindDistance)
                return false;

            var lit = World.lighting.IsLitAt(position);
            var range = lit ? Settings.sightRange : Settings.sightRange * Settings.darkRangeFactor;
            var halfAngle = lit ? Settings.sightHalfAngle : Settings.flashlightHalfAngle;
            if (distance > range)
                return false;

            var toTarget = position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude > 0.01f && Vector3.Angle(Facing, toTarget) > halfAngle)
                return false;

            return World.HasLineOfSight(transform.position, _motor.CurrentRoom, position, room);
        }

        bool CanSeeNow(CrewAgent crew)
        {
            return InSight(crew.transform.position, crew.Motor.CurrentRoom, crew.Hidden, out _);
        }

        void Watch()
        {
            CrewAgent best = null;
            var bestDistance = float.MaxValue;
            foreach (var crew in CrewAgent.All)
            {
                if (crew.IsOut)
                    continue;
                if (InSight(crew.transform.position, crew.Motor.CurrentRoom, crew.Hidden, out var distance) && distance < bestDistance)
                {
                    best = crew;
                    bestDistance = distance;
                }
            }

            if (best == null)
            {
                _spot = Mathf.Max(0f, _spot - Time.deltaTime);
            }
            else if (State != GuardState.Chase)
            {
                // Cuanto mas lejos, mas tarda en darse cuenta
                _spot += Time.deltaTime / (Settings.spotSeconds * (1f + 2f * bestDistance / Settings.sightRange));
                if (_spot >= 1f)
                {
                    _spot = 0f;
                    BeginReport(best, null);
                    Say("alarm_saw", World.layout.Room(_motor.CurrentRoom).spoken);
                    best.OnSpotted(this);
                    return;
                }
            }

            WatchForBodies();
        }

        void WatchForBodies()
        {
            foreach (var other in All)
            {
                if (other == this || !other.isActiveAndEnabled || !other.IsDown || _bodiesReported.Contains(other))
                    continue;
                if (!InSight(other.transform.position, other.Motor.CurrentRoom, false, out _))
                    continue;

                _bodiesReported.Add(other);
                Say("body", World.layout.Room(_motor.CurrentRoom).spoken);
                HeistEvents.Raise(HeistEventKind.BodyFound, other.transform.position, other.displayName);
                Investigate(other.transform.position);
            }
        }

        void Listen()
        {
            CrewAgent heard = null;
            foreach (var crew in CrewAgent.All)
            {
                if (crew.IsOut || crew.NoiseRadius <= 0f)
                    continue;
                if (Vector3.Distance(transform.position, crew.transform.position) <= crew.NoiseRadius
                    && World.CanHear(_motor.CurrentRoom, crew.Motor.CurrentRoom))
                {
                    heard = crew;
                    break;
                }
            }

            if (heard == null)
            {
                _hear = Mathf.Max(0f, _hear - Time.deltaTime);
                return;
            }

            _hear += Time.deltaTime;
            if (_hear < Settings.hearSeconds)
                return;

            _hear = 0f;
            Say("strange");
            HeistEvents.Raise(HeistEventKind.GuardReported, transform.position, displayName);
            var error = Random.insideUnitCircle * 1f;
            Investigate(heard.transform.position + new Vector3(error.x, 0f, error.y));
        }
    }
}
