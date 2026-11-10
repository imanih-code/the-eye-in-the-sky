using System;
using System.Collections.Generic;
using EyeSky.Core;
using EyeSky.Feed;
using EyeSky.Security;
using EyeSky.UI;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>
    /// Un hackeo en curso. Ocupa el monitor: mientras dura no se ve ninguna camara.
    /// Cada fallo deja rastro y agotar los intentos bloquea ese sistema.
    /// </summary>
    public sealed class HackSession : MonoBehaviour
    {
        public static readonly RectInt Area = new(0, 0, 800, 450);
        const string SensorsId = "sensores";
        const string VaultId = "camara";
        const string InterferenceId = "interferencia";

        sealed class Stage
        {
            public Func<int, IHackPuzzle> Create;
            public float Seconds;
            public int Attempts;
            /// <summary>Si existe, sustituye al bloqueo cuando se fallan todos los intentos.</summary>
            public Action Failed;
        }

        sealed class Job
        {
            public string Id;
            public Stage[] Stages;
            public Action Done;
        }

        public HackSettings settings;
        public CameraNetwork cameras;
        public RoomLighting lighting;
        public DoorNetwork doors;
        public SensorGrid sensors;
        public HackAudio audioOut;
        public HeistObjectives objectives;

        readonly Dictionary<string, float> _lockedUntil = new();
        Job _job;
        int _stageIndex;
        int _attemptsLeft;
        float _secondsLeft;
        int _nextSeed;

        public bool Active => _job != null;
        public IHackPuzzle Current { get; private set; }
        public int StageIndex => _stageIndex;
        public bool InterferenceUsed { get; private set; }

        /// <summary>Se dispara al empezar y al acabar un hackeo.</summary>
        public event Action<bool> ActiveChanged;

#if UNITY_EDITOR
        /// <summary>Solo para las pruebas: resuelve con el jugador perfecto.</summary>
        public bool AutoSolve { get; set; }
#endif

        void Awake()
        {
            _nextSeed = Environment.TickCount;
        }

        // --- Objetivos -------------------------------------------------------------------

        public float LockSecondsLeft(string id)
        {
            return _lockedUntil.TryGetValue(id, out var until) ? Mathf.Max(0f, until - Time.time) : 0f;
        }

        public static string CameraId(int index) => $"cam{index}";

        public static string LightsId(string roomId) => $"luz_{roomId}";

        public static string DoorId(string doorId) => $"puerta_{doorId}";

        public float SensorsLockSeconds => LockSecondsLeft(SensorsId);

        public void LoopCamera(int index)
        {
            var tuning = settings.empalme;
            Launch(CameraId(index), () => cameras.Loop(index, settings.loopSeconds), new Stage
            {
                Create = seed => new EmpalmePuzzle(tuning, seed),
                Seconds = tuning.seconds,
                Attempts = tuning.attempts,
            });
        }

        public void ToggleLights(string roomId)
        {
            var tuning = settings.puenteLights;
            Launch(LightsId(roomId), () => lighting.SetLit(roomId, !lighting.IsLit(roomId)), Puente(tuning));
        }

        static Stage Puente(HackSettings.Puente tuning) => new()
        {
            Create = seed => new PuentePuzzle(tuning, seed),
            Seconds = tuning.seconds,
            Attempts = tuning.attempts,
        };

        static Stage Parche(HackSettings.Parche tuning, Action failed = null) => new()
        {
            Create = seed => new ParchePuzzle(tuning, seed),
            Seconds = tuning.seconds,
            Attempts = 1,
            Failed = failed,
        };

        /// <summary>Lo que corresponda segun el tipo de puerta y como este.</summary>
        public void OperateDoor(BuildingLayout.DoorDef door)
        {
            var open = doors.IsOpen(door.id);
            switch (door.kind)
            {
                case BuildingLayout.DoorKind.Electronic:
                    if (open)
                        Launch(DoorId(door.id), () =>
                        {
                            doors.SetOpen(door.id, false);
                            doors.LockAgainstGuards(door.id, settings.doorLockSeconds);
                        }, Puente(settings.puenteDoor));
                    else
                        Launch(DoorId(door.id), () => doors.SetOpen(door.id, true), Puente(settings.puenteDoor));
                    break;

                case BuildingLayout.DoorKind.Roller:
                    if (!open)
                        Launch(DoorId(door.id), () => doors.SetOpen(door.id, true), Parche(settings.parcheEasy));
                    break;

                case BuildingLayout.DoorKind.Biometric:
                    if (!open)
                    {
                        var tuning = settings.huella;
                        Launch(DoorId(door.id), () => doors.SetOpen(door.id, true), new Stage
                        {
                            Create = seed => new HuellaPuzzle(tuning, seed),
                            Seconds = tuning.seconds,
                            Attempts = tuning.attempts,
                        });
                    }
                    break;

                case BuildingLayout.DoorKind.Vault:
                    if (!open)
                        OpenVault(door);
                    break;
            }
        }

        void OpenVault(BuildingLayout.DoorDef door)
        {
            if (Active || LockSecondsLeft(VaultId) > 0f || !objectives.CanOpenVault())
                return;

            var tuning = settings.cripta;
            var code = new Stage
            {
                Create = seed => new CriptaPuzzle(tuning, seed),
                Seconds = tuning.seconds,
                Attempts = 1,
            };

            // Si falla el parche la puerta se abre igual, pero avisa
            var alarm = Parche(settings.parcheHard, () =>
            {
                doors.SetOpen(door.id, true);
                HeistEvents.Raise(HeistEventKind.AlarmConfirmed, door.position, "alarma de la camara acorazada");
            });

            objectives.VaultHackRunning = Launch(VaultId, () =>
            {
                doors.SetOpen(door.id, true);
                objectives.OnVaultOpened();
            }, code, alarm);
        }

        public void DisableSensors()
        {
            if (!sensors.Armed)
                return;
            var tuning = settings.cortafuegos;
            Launch(SensorsId, () => sensors.DisableFor(settings.sensorOffSeconds), new Stage
            {
                Create = seed => new CortafuegosPuzzle(tuning, seed),
                Seconds = tuning.seconds,
                Attempts = tuning.attempts,
            });
        }

        public void Interfere()
        {
            if (InterferenceUsed)
                return;
            var tuning = settings.interferencia;
            var started = Launch(InterferenceId,
                () => HeistEvents.Raise(HeistEventKind.InterferenceSucceeded, Vector3.zero, "central suplantada"),
                new Stage
                {
                    Create = seed =>
                    {
                        var puzzle = new InterferenciaPuzzle(tuning, seed);
                        puzzle.ToneRequested += audioOut.PlayTone;
                        return puzzle;
                    },
                    Seconds = tuning.seconds,
                    Attempts = 1,
                    Failed = () => HeistEvents.Raise(HeistEventKind.InterferenceFailed, Vector3.zero, "la central sospecha"),
                });
            if (started)
                InterferenceUsed = true;
        }

        // --- Sesion ----------------------------------------------------------------------

        bool Launch(string id, Action done, params Stage[] stages)
        {
            if (Active || LockSecondsLeft(id) > 0f)
                return false;

            _job = new Job { Id = id, Stages = stages, Done = done };
            _stageIndex = 0;
            BeginStage();
            ActiveChanged?.Invoke(true);
            return true;
        }

        void BeginStage()
        {
            _attemptsLeft = _job.Stages[_stageIndex].Attempts;
            NewPuzzle();
        }

        void NewPuzzle()
        {
            var stage = _job.Stages[_stageIndex];
            Current = stage.Create(_nextSeed++);
            _secondsLeft = stage.Seconds;
        }

        /// <summary>La consola le pasa lo que hace el jugador cada frame.</summary>
        public void Tick(HackInput input)
        {
            if (!Active)
                return;

#if UNITY_EDITOR
            if (AutoSolve)
                input = Current.BotInput(Area);
#endif

            var step = Current.Tick(Time.deltaTime, input, Area);
            _secondsLeft -= Time.deltaTime;
            if (step == HackStep.Running && _secondsLeft <= 0f)
                step = HackStep.AttemptFailed;

            audioOut.SetNoise(Current is InterferenciaPuzzle { IsListening: true });

            switch (step)
            {
                case HackStep.Mistake:
                    audioOut.PlayError();
                    HeistEvents.Raise(HeistEventKind.HackFailed, Vector3.zero, _job.Id);
                    break;
                case HackStep.Solved:
                    AdvanceStage();
                    break;
                case HackStep.AttemptFailed:
                    AttemptFailed();
                    break;
            }
        }

        void AdvanceStage()
        {
            audioOut.PlaySuccess();
            _stageIndex++;
            if (_stageIndex < _job.Stages.Length)
            {
                BeginStage();
                return;
            }

            var done = _job.Done;
            Finish();
            done();
        }

        void AttemptFailed()
        {
            audioOut.PlayError();
            HeistEvents.Raise(HeistEventKind.HackFailed, Vector3.zero, _job.Id);
            _attemptsLeft--;
            if (_attemptsLeft > 0)
            {
                NewPuzzle();
                return;
            }

            var stage = _job.Stages[_stageIndex];
            if (stage.Failed != null)
            {
                var failed = stage.Failed;
                Finish();
                failed();
                return;
            }

            _lockedUntil[_job.Id] = Time.time + settings.lockoutSeconds;
            HeistEvents.Raise(HeistEventKind.HackLockout, Vector3.zero, _job.Id);
            Finish();
        }

        /// <summary>Abandonar cuenta como un fallo: no sale gratis.</summary>
        public void Abort()
        {
            if (!Active)
                return;
            HeistEvents.Raise(HeistEventKind.HackFailed, Vector3.zero, _job.Id);
            Finish();
        }

        void Finish()
        {
            objectives.VaultHackRunning = false;
            audioOut.SetNoise(false);
            _job = null;
            Current = null;
            ActiveChanged?.Invoke(false);
        }

        // --- Dibujo ----------------------------------------------------------------------

        public void Draw(ConsoleSurface surface)
        {
            if (!Active)
                return;
            var stage = _job.Stages[_stageIndex];
            DrawFrame(surface, Current, _secondsLeft, stage.Seconds, _attemptsLeft, stage.Attempts);
        }

        /// <summary>Publico para capturar los puzzles sin partida.</summary>
        public static void DrawFrame(ConsoleSurface surface, IHackPuzzle puzzle, float secondsLeft, float seconds, int attemptsLeft, int attempts)
        {
            surface.Fill(Area, HackPalette.Back);
            surface.Text(puzzle.Title, Area.x + 8, Area.y + 8, HackPalette.Text, 2);

            var fraction = Mathf.Clamp01(secondsLeft / seconds);
            const int barX = 420;
            const int barWidth = 280;
            surface.Frame(new RectInt(Area.x + barX, Area.y + 10, barWidth, 12), HackPalette.Line);
            surface.Fill(Area.x + barX + 2, Area.y + 12, Mathf.RoundToInt((barWidth - 4) * fraction), 8,
                secondsLeft <= 5f ? HackPalette.Red : HackPalette.Amber);
            surface.Text($"{Mathf.Max(0f, secondsLeft):0.0}", Area.x + barX + barWidth + 8, Area.y + 12,
                secondsLeft <= 5f ? HackPalette.Red : HackPalette.Text);

            for (var i = 0; i < attempts; i++)
                surface.Fill(Area.x + barX - 14 - i * 12, Area.y + 11, 9, 9, i < attemptsLeft ? HackPalette.Flow : HackPalette.Grid);

            puzzle.Draw(surface, Area);
            surface.Text(puzzle.Hint + "   ESC ABANDONAR", Area.x + 8, Area.y + Area.height - 12, HackPalette.Dim);
        }
    }
}
