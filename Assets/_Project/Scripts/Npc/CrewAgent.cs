using System.Collections.Generic;
using EyeSky.Core;
using EyeSky.Radio;
using EyeSky.Security;
using UnityEngine;
using UnityEngine.AI;

namespace EyeSky.Npc
{
    public enum CrewState
    {
        Waiting,
        Moving,
        Blocked,
        Picking,
        Hiding,
        Hidden,
        Attacking,
        Working,
        Collecting,
        Down,
        Escaped,
    }

    public enum OrderKind
    {
        Go,
        Stop,
        Hide,
        Knockout,
        Kill,
        Retreat,
    }

    /// <summary>
    /// Un miembro de la banda. No se controla: recibe ordenes por radio, tarda en reaccionar
    /// y sin ordenes espera, pregunta o se esconde.
    /// </summary>
    [RequireComponent(typeof(ActorMotor))]
    [RequireComponent(typeof(IsoPuppet))]
    public sealed class CrewAgent : MonoBehaviour
    {
        public static readonly List<CrewAgent> All = new();

        struct Order
        {
            public OrderKind Kind;
            public Vector3 Point;
            public GuardAgent Target;
        }

        public string displayName;
        public bool canPick;
        public bool canFight;
        [Tooltip("El saco que se ve a la espalda al cargar el botin.")]
        public GameObject carriedBag;

        HeistItem _collecting;
        ActorMotor _motor;
        IsoPuppet _puppet;
        readonly List<Vector3> _waypoints = new();

        Order? _pending;
        float _pendingDelay;
        bool _retreating;
        CrewState _resumeState;
        BuildingLayout.DoorDef _pickDoor;
        float _workLeft;
        float _idleClock;
        float _hearClock;
        bool _announcedSteps;
        float _nagLeft;
        float _refreshLeft;
        GuardAgent _target;
        bool _lethal;

        public CrewState State { get; private set; } = CrewState.Waiting;
        public ActorMotor Motor => _motor;
        public IsoPuppet Puppet => _puppet;
        public bool IsOut => State is CrewState.Down or CrewState.Escaped;
        public bool Hidden => State == CrewState.Hidden;

        static NpcWorld World => NpcWorld.Instance;
        static NpcSettings Settings => World.settings;

        void Awake()
        {
            _motor = GetComponent<ActorMotor>();
            _puppet = GetComponent<IsoPuppet>();
            All.Add(this);
            // Unity no garantiza el orden de arranque, y los botones de la consola dependen de este
            All.Sort((a, b) => string.CompareOrdinal(a.displayName, b.displayName));
        }

        void OnDestroy()
        {
            All.Remove(this);
        }

        void Start()
        {
            _motor.SetSpeed(canFight ? Settings.yunqueSpeed : Settings.liebreSpeed);
        }

        /// <summary>Radio del ruido que hace ahora mismo. Cero si esta quieto.</summary>
        public float NoiseRadius => State switch
        {
            CrewState.Moving or CrewState.Attacking => canFight ? Settings.yunqueNoiseRadius : Settings.liebreNoiseRadius,
            CrewState.Picking => Settings.pickNoiseRadius,
            // Mientras se trabaja la camara acorazada, quien esta en la puerta hace ruido
            _ => HeistObjectives.Instance.VaultHackRunning && _motor.CurrentRoom == "antesala" ? Settings.pickNoiseRadius : 0f,
        };

        public void SetCarrying(bool carrying)
        {
            if (carriedBag != null)
                carriedBag.SetActive(carrying);
        }

        public string StatusText
        {
            get
            {
                if (_pending != null)
                    return "RECIBIDO";
                return State switch
                {
                    CrewState.Waiting => "ESPERANDO",
                    CrewState.Moving => _retreating ? "RETIRADA" : "EN MARCHA",
                    CrewState.Blocked => "PUERTA CERRADA",
                    CrewState.Picking => "FORZANDO PUERTA",
                    CrewState.Hiding => "BUSCA ESCONDITE",
                    CrewState.Hidden => "ESCONDIDO",
                    CrewState.Attacking => "A POR GUARDIA",
                    CrewState.Working => "NEUTRALIZANDO",
                    CrewState.Collecting => "RECOGIENDO",
                    CrewState.Down => "REDUCIDO",
                    _ => "A SALVO",
                };
            }
        }

        void Say(string key, params object[] args) => RadioBus.Say(RadioChannel.Crew, displayName, key, args);

        // --- Ordenes -------------------------------------------------------------------

        public void Issue(OrderKind kind, Vector3 point = default, GuardAgent target = null, bool append = false)
        {
            if (IsOut)
                return;

            // Anadir un punto a la ruta en marcha no interrumpe nada
            if (append && kind == OrderKind.Go && State is CrewState.Moving or CrewState.Blocked or CrewState.Picking)
            {
                _waypoints.Add(point);
                Say("ack_append");
                return;
            }

            Say(kind switch
            {
                OrderKind.Go => "ack_go",
                OrderKind.Stop => "ack_stop",
                OrderKind.Hide => "ack_hide",
                OrderKind.Knockout => "ack_knockout",
                OrderKind.Kill => "ack_kill",
                _ => "ack_retreat",
            });

            _pending = new Order { Kind = kind, Point = point, Target = target };
            _pendingDelay = Random.Range(Settings.orderDelayMin, Settings.orderDelayMax);
            _idleClock = 0f;
            _hearClock = 0f;
            _announcedSteps = false;
        }

        void Execute(Order order)
        {
            _retreating = false;
            _puppet.Crouched = false;
            _waypoints.Clear();

            switch (order.Kind)
            {
                case OrderKind.Go:
                    _waypoints.Add(order.Point);
                    StartNextWaypoint();
                    break;
                case OrderKind.Stop:
                    _motor.Stop();
                    State = CrewState.Waiting;
                    break;
                case OrderKind.Hide:
                    StartHiding();
                    break;
                case OrderKind.Knockout:
                case OrderKind.Kill:
                    StartAttack(order.Target, order.Kind == OrderKind.Kill);
                    break;
                case OrderKind.Retreat:
                    _retreating = true;
                    _waypoints.Add(World.vanPoint);
                    StartNextWaypoint();
                    break;
            }
        }

        int DoorCost(BuildingLayout.DoorDef door)
        {
            if (World.doors.IsOpen(door.id))
                return 1;
            return canPick && door.kind == BuildingLayout.DoorKind.Mechanical ? 3 : 20;
        }

        void StartNextWaypoint()
        {
            var point = _waypoints[0];
            _waypoints.RemoveAt(0);
            _motor.Go(point, DoorCost);
            State = CrewState.Moving;
        }

        void StartHiding()
        {
            _motor.Stop();
            var room = World.layout.Room(_motor.CurrentRoom);
            if (room != null && room.hidingSpots.Count > 0)
            {
                var best = room.hidingSpots[0];
                foreach (var spot in room.hidingSpots)
                    if ((spot - transform.position).sqrMagnitude < (best - transform.position).sqrMagnitude)
                        best = spot;
                _motor.Go(best, DoorCost);
                State = CrewState.Hiding;
                return;
            }
            EnterHidden();
        }

        void EnterHidden()
        {
            State = CrewState.Hidden;
            _puppet.Crouched = true;
        }

        void StartAttack(GuardAgent target, bool lethal)
        {
            if (target == null || target.IsDown)
            {
                State = CrewState.Waiting;
                return;
            }
            _target = target;
            _lethal = lethal;
            _refreshLeft = 0f;
            _puppet.Crouched = false;
            State = CrewState.Attacking;
        }

        // --- Reacciones ----------------------------------------------------------------

        /// <summary>Un guardia le ha visto.</summary>
        public void OnSpotted(GuardAgent guard)
        {
            if (IsOut || State is CrewState.Working or CrewState.Attacking)
                return;

            Say("spotted");
            _pending = null;
            _waypoints.Clear();
            _puppet.Crouched = false;

            var distance = Vector3.Distance(transform.position, guard.transform.position);
            if (canFight && distance <= Settings.engageDistance)
                StartAttack(guard, false);
            else
                StartHiding();
        }

        public void Capture()
        {
            if (IsOut)
                return;
            Say("captured");
            HeistObjectives.Instance.OnCrewDown(this);
            State = CrewState.Down;
            _pending = null;
            _motor.Stop();
            GetComponent<NavMeshAgent>().enabled = false;
            _puppet.Downed = true;
        }

        // --- Bucle ---------------------------------------------------------------------

        void Update()
        {
            if (IsOut)
                return;

            if (_pending != null)
            {
                _pendingDelay -= Time.deltaTime;
                if (_pendingDelay <= 0f)
                {
                    var order = _pending.Value;
                    _pending = null;
                    Execute(order);
                }
            }

            switch (State)
            {
                case CrewState.Moving:
                    if (!HandleBlockedDoor(CrewState.Moving) && _motor.Status == ActorMotor.MotorStatus.Idle)
                        ReachedWaypoint();
                    break;
                case CrewState.Blocked:
                    TickBlocked();
                    break;
                case CrewState.Picking:
                    TickPicking();
                    break;
                case CrewState.Hiding:
                    if (_motor.Status == ActorMotor.MotorStatus.Idle)
                        EnterHidden();
                    break;
                case CrewState.Attacking:
                    TickAttack();
                    break;
                case CrewState.Working:
                    TickWorking();
                    break;
                case CrewState.Collecting:
                    TickCollecting();
                    break;
                case CrewState.Waiting:
                    if (_pending == null)
                        TickWaiting();
                    break;
            }
        }

        void ReachedWaypoint()
        {
            if (_waypoints.Count > 0)
            {
                StartNextWaypoint();
                return;
            }

            if (_retreating)
            {
                State = CrewState.Escaped;
                Say("escaped");
                gameObject.SetActive(false);
                return;
            }

            State = CrewState.Waiting;
            _idleClock = 0f;
            Say("arrived", World.layout.Room(_motor.CurrentRoom).spoken);
        }

        bool HandleBlockedDoor(CrewState resumeState)
        {
            if (_motor.Status != ActorMotor.MotorStatus.Blocked)
                return false;

            var door = _motor.BlockedDoor;
            _resumeState = resumeState;
            if (canPick && door.kind == BuildingLayout.DoorKind.Mechanical)
            {
                _pickDoor = door;
                _workLeft = Settings.pickSeconds;
                State = CrewState.Picking;
                Say("picking");
                return true;
            }

            State = CrewState.Blocked;
            _nagLeft = Settings.doorNagSeconds;
            SayDoorProblem(door);
            return true;
        }

        void SayDoorProblem(BuildingLayout.DoorDef door)
        {
            Say(door.kind == BuildingLayout.DoorKind.Mechanical ? "door_cannot" : "door_system");
        }

        void TickBlocked()
        {
            var door = _motor.BlockedDoor;
            if (door == null || World.doors.IsOpen(door.id))
            {
                _motor.Resume();
                State = _resumeState;
                return;
            }

            _nagLeft -= Time.deltaTime;
            if (_nagLeft <= 0f)
            {
                _nagLeft = Settings.doorNagSeconds;
                SayDoorProblem(door);
            }
        }

        void TickPicking()
        {
            _workLeft -= Time.deltaTime;
            if (_workLeft > 0f)
                return;
            World.doors.SetOpen(_pickDoor.id, true);
            Say("picked");
            _motor.Resume();
            State = _resumeState;
        }

        void TickAttack()
        {
            if (_target == null || _target.IsDown)
            {
                _motor.Stop();
                State = CrewState.Waiting;
                return;
            }
            if (HandleBlockedDoor(CrewState.Attacking))
                return;

            var distance = Vector3.Distance(transform.position, _target.transform.position);
            if (_lethal)
            {
                var hasShot = distance <= Settings.shootRange && World.HasLineOfSight(transform.position, _motor.CurrentRoom,
                    _target.transform.position, _target.Motor.CurrentRoom);
                if (hasShot)
                {
                    Shoot();
                    return;
                }
            }
            else if (distance <= Settings.knockoutReach)
            {
                BeginKnockout();
                return;
            }

            _refreshLeft -= Time.deltaTime;
            if (_refreshLeft <= 0f)
            {
                _refreshLeft = 0.3f;
                _motor.Go(_target.transform.position, DoorCost);
            }
        }

        void Shoot()
        {
            _motor.Stop();
            _puppet.Face(_target.transform.position - transform.position);
            HeistEvents.Raise(HeistEventKind.Gunshot, transform.position, _motor.CurrentRoom);
            GuardRoster.Instance.OnGunshot(transform.position);
            _target.Neutralize(true);
            Say("shot_done");
            State = CrewState.Waiting;
            _idleClock = 0f;
        }

        // De frente y con luz, el guardia tiene tiempo de avisar
        void BeginKnockout()
        {
            _motor.Stop();
            var toCrew = transform.position - _target.transform.position;
            var fromBehind = Vector3.Angle(_target.Facing, toCrew) >= Settings.behindAngle;
            var inTheDark = !World.lighting.IsLitAt(_target.transform.position);
            if (!fromBehind && !inTheDark)
                _target.ReportNow(this);

            _target.Hold();
            _puppet.Face(-toCrew);
            _workLeft = Settings.knockoutSeconds;
            State = CrewState.Working;
        }

        void TickWorking()
        {
            _workLeft -= Time.deltaTime;
            if (_workLeft > 0f)
                return;
            _target.Neutralize(false);
            Say("takedown_done");
            State = CrewState.Waiting;
            _idleClock = 0f;
        }

        void TickCollecting()
        {
            if (_motor.Status != ActorMotor.MotorStatus.Idle)
                return;
            _workLeft -= Time.deltaTime;
            if (_workLeft > 0f)
                return;
            HeistObjectives.Instance.Collected(_collecting, this);
            State = CrewState.Waiting;
            _idleClock = 0f;
        }

        void TickWaiting()
        {
            // Lo que hay que coger en la sala lo coge sin que se lo digan
            if (HeistObjectives.Instance.TryGetItem(this, out _collecting, out var itemPoint))
            {
                _motor.Go(itemPoint, DoorCost);
                _workLeft = HeistObjectives.Instance.collectSeconds;
                State = CrewState.Collecting;
                return;
            }

            _idleClock += Time.deltaTime;
            if (_idleClock >= Settings.idleAskSeconds)
            {
                _idleClock = 0f;
                Say("idle_ask");
            }

            if (!GuardIsNear())
            {
                _hearClock = 0f;
                _announcedSteps = false;
                return;
            }

            if (!_announcedSteps)
            {
                _announcedSteps = true;
                Say("hear_steps");
            }

            _hearClock += Time.deltaTime;
            if (_hearClock >= Settings.autoHideSeconds)
            {
                _hearClock = 0f;
                StartHiding();
            }
        }

        bool GuardIsNear()
        {
            foreach (var guard in GuardAgent.All)
            {
                if (!guard.isActiveAndEnabled || guard.IsDown || guard.isOperator)
                    continue;
                var near = Vector3.Distance(transform.position, guard.transform.position) <= Settings.hearFootstepsRadius;
                if (near && World.CanHear(_motor.CurrentRoom, guard.Motor.CurrentRoom))
                    return true;
            }
            return false;
        }
    }
}
