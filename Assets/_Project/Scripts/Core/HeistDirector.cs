using System.Collections.Generic;
using EyeSky.Hacking;
using EyeSky.Npc;
using EyeSky.Police;
using EyeSky.Radio;
using EyeSky.Security;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace EyeSky.Core
{
    /// <summary>El principio y el final del golpe.</summary>
    public sealed class HeistDirector : MonoBehaviour
    {
        public const float ClockStartSeconds = 2f * 3600f + 15f * 60f;
        const float CutSeconds = 1.4f;
        const float FirstCutDelay = 2.5f;

        // Al reintentar no vuelve a salir la portada
        static bool _titleSeen;

        public PoliceResponse police;
        public PoliceArrival arrival;
        public CameraNetwork cameras;
        public HackSession hacks;
        public HeistObjectives objectives;
        public AudioSource ambience;

        readonly List<int> _pendingCuts = new();
        bool _arrived;
        bool _crewDownHandled;
        float _cutLeft;
        float _startedAt;

        public bool Started { get; private set; }
        public bool Finished => Outcome != HeistOutcome.InProgress;
        public HeistOutcome Outcome { get; private set; } = HeistOutcome.InProgress;
        public HeistReport Report { get; private set; }
        public float ElapsedSeconds => Time.time - _startedAt;

        void Awake()
        {
            HeistEvents.Clear();
            if (_titleSeen)
                return;
            Time.timeScale = 0f;
        }

        void Start()
        {
            if (_titleSeen)
                StartHeist();
        }

        public void StartHeist()
        {
            if (Started)
                return;
            Started = true;
            _titleSeen = true;
            Time.timeScale = 1f;
            _startedAt = Time.time;
            if (ambience != null)
                ambience.Play();
            RadioBus.Say(RadioChannel.Crew, "LIEBRE", "objective_intro");
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (!Started)
            {
                if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame))
                    StartHeist();
                return;
            }

            if (Finished)
            {
                if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                return;
            }

            CheckCrew();
            if (Finished)
                return;

            if (!_arrived && police.Model.State == PoliceState.Arrived)
                BeginArrival();
            if (_arrived)
                TickCuts();
        }

        void CheckCrew()
        {
            if (CrewAgent.All.Count == 0)
                return;

            if (!_crewDownHandled && CrewAgent.All.TrueForAll(c => c.State == CrewState.Down))
            {
                _crewDownHandled = true;
                police.Model.ForceArrivalWithin(police.settings.allCrewDownSeconds);
            }

            // Hay que estar antes de que llegue la policia: el que cayo no cuenta
            var anyEscaped = CrewAgent.All.Exists(c => c.State == CrewState.Escaped);
            var allOut = CrewAgent.All.TrueForAll(c => c.IsOut);
            if (!_arrived && anyEscaped && allOut)
                End(objectives.LootEscaped ? HeistOutcome.Won : HeistOutcome.WalkedAway);
        }

        void BeginArrival()
        {
            _arrived = true;
            arrival.Show();
            _cutLeft = FirstCutDelay;

            // La calle es lo ultimo que se apaga: ahi es donde esta la policia
            for (var i = 1; i < cameras.layout.cameras.Count; i++)
                _pendingCuts.Add(i);
            for (var i = _pendingCuts.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (_pendingCuts[i], _pendingCuts[j]) = (_pendingCuts[j], _pendingCuts[i]);
            }
            _pendingCuts.Add(0);
        }

        void TickCuts()
        {
            _cutLeft -= Time.deltaTime;
            if (_cutLeft > 0f)
                return;

            _cutLeft = CutSeconds;
            if (_pendingCuts.Count > 0)
            {
                cameras.Cut(_pendingCuts[0]);
                _pendingCuts.RemoveAt(0);
                return;
            }
            End(HeistOutcome.Caught);
        }

        void End(HeistOutcome outcome)
        {
            hacks.Abort();
            Outcome = outcome;
            Report = HeistReport.Build(outcome, police, ElapsedSeconds);
        }
    }
}
