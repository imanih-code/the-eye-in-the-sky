using System;
using EyeSky.Core;
using UnityEngine;

namespace EyeSky.Police
{
    public enum PoliceState
    {
        Calm,
        Rumor,
        Verifying,
        EnRoute,
        Arrived,
    }

    /// <summary>
    /// La respuesta policial, sin nada de imagen ni sonido. El jugador solo la conoce por la radio:
    /// ningun dato de aqui debe llegar a la consola.
    /// </summary>
    public sealed class PoliceModel
    {
        /// <summary>Puntos del recorrido, de 0 (salida) a 1 (la puerta).</summary>
        public static readonly float[] RouteMilestones = { 0.15f, 0.45f, 0.7f, 0.9f };

        readonly PoliceSettings _settings;
        readonly System.Random _random;
        readonly float _roundAt;
        float _elapsed;
        bool _roundDone;
        bool _blackoutSeen;
        int _nextMilestone;

        public PoliceState State { get; private set; } = PoliceState.Calm;
        public float Suspicion { get; private set; }
        public float SecondsToArrival { get; private set; } = float.PositiveInfinity;
        public float RouteProgress { get; private set; }
        public bool SilentApproach { get; private set; }

        public event Action<PoliceState, PoliceState> StateChanged;
        public event Action<int> MilestoneReached;
        public event Action<bool> InterferenceResolved;

        public PoliceModel(PoliceSettings settings, int seed)
        {
            _settings = settings;
            _random = new System.Random(seed);
            _roundAt = Range(settings.roundSecondsMin, settings.roundSecondsMax);
        }

        bool UnitOnTheWay => State is PoliceState.Verifying or PoliceState.EnRoute;

        float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);

        public void Tick(float deltaTime)
        {
            if (State == PoliceState.Arrived)
                return;

            _elapsed += deltaTime;
            if (!_roundDone && _elapsed >= _roundAt)
            {
                _roundDone = true;
                if (State < PoliceState.Verifying)
                    Enter(PoliceState.Verifying);
            }

            if (State < PoliceState.Verifying)
            {
                Suspicion = Mathf.Max(0f, Suspicion - deltaTime / _settings.secondsPerDecayPoint);
                Evaluate();
                return;
            }

            // El coche avanza a lo que le queda: si se retrasa va mas lento, nunca hacia atras
            RouteProgress += deltaTime * (1f - RouteProgress) / Mathf.Max(SecondsToArrival, 0.01f);
            SecondsToArrival -= deltaTime;
            while (_nextMilestone < RouteMilestones.Length && RouteProgress >= RouteMilestones[_nextMilestone])
            {
                var reached = _nextMilestone;
                _nextMilestone++;
                MilestoneReached?.Invoke(reached);
            }

            if (SecondsToArrival <= 0f)
                Enter(PoliceState.Arrived);
        }

        public void Apply(HeistEventKind kind)
        {
            if (State == PoliceState.Arrived)
                return;

            switch (kind)
            {
                case HeistEventKind.HackFailed:
                    Add(_settings.hackFailed);
                    break;
                case HeistEventKind.HackLockout:
                    Add(_settings.hackLockout);
                    break;
                case HeistEventKind.Blackout:
                    Add(_blackoutSeen ? _settings.nextBlackout : _settings.firstBlackout);
                    _blackoutSeen = true;
                    break;
                case HeistEventKind.GuardReported:
                    Add(_settings.guardReported);
                    break;
                case HeistEventKind.MissedCheck:
                    Add(_settings.missedCheck);
                    break;
                case HeistEventKind.Gunshot:
                    Add(_settings.gunshot);
                    break;
                case HeistEventKind.BodyFound:
                    Add(_settings.bodyFound);
                    break;
                case HeistEventKind.AlarmConfirmed:
                    Enter(PoliceState.EnRoute);
                    break;
                case HeistEventKind.InterferenceSucceeded:
                    ResolveInterference(true);
                    break;
                case HeistEventKind.InterferenceFailed:
                    ResolveInterference(false);
                    break;
            }
        }

        void Add(float points)
        {
            Suspicion += points;
            Evaluate();
        }

        void Evaluate()
        {
            if (Suspicion >= _settings.enRouteAt)
                Enter(PoliceState.EnRoute);
            else if (Suspicion >= _settings.verifyAt)
                Enter(PoliceState.Verifying);
            else if (State < PoliceState.Verifying)
                Enter(Suspicion >= _settings.rumorAt ? PoliceState.Rumor : PoliceState.Calm);
        }

        void Enter(PoliceState next)
        {
            // Hacia atras solo se puede ir de rumor a calma
            var isDecay = State == PoliceState.Rumor && next == PoliceState.Calm;
            if (next <= State && !isDecay)
                return;

            var previous = State;
            State = next;

            if (next == PoliceState.Verifying)
            {
                SecondsToArrival = Mathf.Min(SecondsToArrival, Range(_settings.verifySecondsMin, _settings.verifySecondsMax));
            }
            else if (next == PoliceState.EnRoute)
            {
                SecondsToArrival = Mathf.Min(SecondsToArrival, Range(_settings.enRouteSecondsMin, _settings.enRouteSecondsMax));
                SilentApproach = _random.NextDouble() < _settings.silentApproachChance;
            }

            StateChanged?.Invoke(previous, next);
        }

        void ResolveInterference(bool success)
        {
            // En calma no hay a quien desviar: se gasta para nada
            if (!UnitOnTheWay)
                return;

            SecondsToArrival = success
                ? SecondsToArrival + _settings.interferenceBonusSeconds
                : Mathf.Max(5f, SecondsToArrival - _settings.interferencePenaltySeconds);
            InterferenceResolved?.Invoke(success);
        }

        /// <summary>Si cae toda la banda, ya no hay prisa por disimular.</summary>
        public void ForceArrivalWithin(float seconds)
        {
            Enter(PoliceState.EnRoute);
            SecondsToArrival = Mathf.Min(SecondsToArrival, seconds);
        }
    }
}
