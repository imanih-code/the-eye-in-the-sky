using System.Collections.Generic;
using EyeSky.Core;
using EyeSky.Radio;
using UnityEngine;

namespace EyeSky.Police
{
    /// <summary>
    /// Conecta los hechos de la partida con el modelo, y el modelo con la radio.
    /// Es la unica salida de la policia hacia el jugador: lo que se dice por el canal y las sirenas.
    /// </summary>
    public sealed class PoliceResponse : MonoBehaviour
    {
        const string Central = "CENTRAL";
        const string SlowUnit = "UNIDAD 7";
        const string FastUnit = "UNIDAD 14";

        public PoliceSettings settings;
        public SirenAudio siren;

        float _chatterLeft;
        float _elapsed;
        bool _roundNoticed;

        public sealed class TimelineEntry
        {
            public float Seconds;
            public string Text;
            public bool Important;
        }

        string _currentCause;

        public PoliceModel Model { get; private set; }

        /// <summary>Todo lo que paso, para el informe final.</summary>
        public readonly List<TimelineEntry> Timeline = new();

        /// <summary>Lo que hizo salir a la primera unidad.</summary>
        public string BetrayedBy { get; private set; }
        public float BetrayedAt { get; private set; }

        void Awake()
        {
            Model = new PoliceModel(settings, System.Environment.TickCount);
            Model.StateChanged += OnStateChanged;
            Model.MilestoneReached += OnMilestone;
            Model.InterferenceResolved += OnInterference;
            _chatterLeft = settings.chatterSecondsMin * 0.4f;
        }

        void OnEnable()
        {
            HeistEvents.Raised += OnHeistEvent;
        }

        void OnDisable()
        {
            HeistEvents.Raised -= OnHeistEvent;
        }

        static string Describe(HeistEvents.Entry entry) => entry.Kind switch
        {
            HeistEventKind.HackFailed => "HACKEO FALLIDO",
            HeistEventKind.HackLockout => "SISTEMA BLOQUEADO",
            HeistEventKind.Blackout => "APAGON",
            HeistEventKind.GuardReported => "UN GUARDIA OYO ALGO",
            HeistEventKind.MissedCheck => $"CONTROL SIN RESPUESTA ({entry.Detail})",
            HeistEventKind.Gunshot => "DISPARO",
            HeistEventKind.BodyFound => $"CUERPO ENCONTRADO ({entry.Detail})",
            HeistEventKind.AlarmConfirmed => $"ALARMA: {entry.Detail}",
            HeistEventKind.InterferenceSucceeded => "CENTRAL SUPLANTADA",
            _ => "INTERFERENCIA DETECTADA",
        };

        void AddTimeline(string text, bool important)
        {
            Timeline.Add(new TimelineEntry { Seconds = _elapsed, Text = text, Important = important });
        }

        void OnHeistEvent(HeistEvents.Entry entry)
        {
            var before = Model.Suspicion;
            var position = Timeline.Count;
            _currentCause = Describe(entry);
            Model.Apply(entry.Kind);
            var added = Model.Suspicion - before;

            // La causa va delante de lo que provoca
            Timeline.Insert(position, new TimelineEntry
            {
                Seconds = _elapsed,
                Text = added > 0.01f ? $"{_currentCause}  +{added:0}" : _currentCause,
            });
            _currentCause = null;

            if (entry.Kind == HeistEventKind.Gunshot)
                Say(Central, "police_gunshot");
            else if (entry.Kind == HeistEventKind.Blackout && Model.State < PoliceState.Verifying)
                Say(Central, "police_outage");
        }

        static void Say(string speaker, string key) => RadioBus.Say(RadioChannel.Police, speaker, key);

        string CurrentUnit => Model.State == PoliceState.EnRoute ? FastUnit : SlowUnit;

        void Update()
        {
            _elapsed += Time.deltaTime;
            Model.Tick(Time.deltaTime);
            TickChatter();
            UpdateSiren();
        }

        void TickChatter()
        {
            if (Model.State == PoliceState.Arrived)
                return;

            if (!_roundNoticed && _elapsed >= settings.roundNoticeSeconds)
            {
                _roundNoticed = true;
                Say(Central, "police_round_notice");
                return;
            }

            _chatterLeft -= Time.deltaTime;
            if (_chatterLeft > 0f)
                return;

            _chatterLeft = Random.Range(settings.chatterSecondsMin, settings.chatterSecondsMax);
            if (RadioBus.Instance != null && RadioBus.Instance.Current(RadioChannel.Police) == null)
                Say(Central, "police_chatter");
        }

        void OnStateChanged(PoliceState previous, PoliceState next)
        {
            AddTimeline(next switch
            {
                PoliceState.Calm => "LA CENTRAL SE OLVIDA",
                PoliceState.Rumor => "LA CENTRAL OYE ALGO",
                PoliceState.Verifying => "SALE LA UNIDAD 7, SIN PRISA",
                PoliceState.EnRoute => "SALE LA UNIDAD 14, CON PRIORIDAD",
                _ => "LA POLICIA ESTA EN LA PUERTA",
            }, true);

            if (next >= PoliceState.Verifying && BetrayedBy == null)
            {
                BetrayedBy = _currentCause ?? "LA RONDA DE LAS 02:30";
                BetrayedAt = _elapsed;
            }

            switch (next)
            {
                case PoliceState.Rumor:
                    Say(Central, "police_rumor");
                    break;
                case PoliceState.Verifying:
                    Say(Central, "police_verify");
                    Say(SlowUnit, "police_verify_ack");
                    break;
                case PoliceState.EnRoute:
                    Say(Central, "police_enroute");
                    Say(FastUnit, "police_enroute_ack");
                    if (Model.SilentApproach)
                        Say(Central, "police_silent");
                    break;
                case PoliceState.Arrived:
                    Say(CurrentUnitOr(previous), "police_arrived");
                    break;
            }
        }

        static string CurrentUnitOr(PoliceState previous) => previous == PoliceState.EnRoute ? FastUnit : SlowUnit;

        void OnMilestone(int index) => Say(CurrentUnit, $"police_route_{index}");

        void OnInterference(bool success) => Say(Central, success ? "police_interference_ok" : "police_interference_bad");

        void UpdateSiren()
        {
            if (siren == null)
                return;

            var audible = Model.State == PoliceState.EnRoute && !Model.SilentApproach && Model.SecondsToArrival <= settings.sirenSeconds;
            siren.SetLevel(audible ? 1f - Model.SecondsToArrival / settings.sirenSeconds : 0f);
        }
    }
}
