using System;
using System.Linq;
using EyeSky.Core;
using EyeSky.Police;
using EyeSky.Radio;
using UnityEditor;
using UnityEngine;

namespace EyeSky.EditorTools
{
    /// <summary>Comprueba las reglas de la policia una a una, sin partida.</summary>
    public static class PoliceTests
    {
        const float Step = 0.1f;
        static int _failures;
        static PoliceSettings _settings;

        static void Check(bool passed, string description)
        {
            Debug.Log($"[EYE] {(passed ? "OK" : "FALLO")}: {description}");
            if (!passed)
                _failures++;
        }

        static PoliceModel New(int seed = 11) => new(_settings, seed);

        static void Advance(PoliceModel model, float seconds)
        {
            for (var t = 0f; t < seconds; t += Step)
                model.Tick(Step);
        }

        /// <summary>Linea de comandos: ejecuta las comprobaciones y sale con el resultado.</summary>
        public static void RunAll()
        {
            _settings = ScriptableObject.CreateInstance<PoliceSettings>();
            try
            {
                Thresholds();
                Scoring();
                Timing();
                Decay();
                Round();
                Interference();
                Lines();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _failures++;
            }

            Debug.Log($"[EYE] Resultado: {_failures} fallos");
            EditorApplication.Exit(_failures == 0 ? 0 : 1);
        }

        static void Thresholds()
        {
            var model = New();
            Check(model.State == PoliceState.Calm, "empieza en calma");
            model.Apply(HeistEventKind.HackLockout);
            Check(model.State == PoliceState.Rumor, "a 25 puntos, rumor");
            model.Apply(HeistEventKind.HackLockout);
            Check(model.State == PoliceState.Verifying, "a 50 puntos, verificacion");
            model.Apply(HeistEventKind.BodyFound);
            Check(model.State == PoliceState.EnRoute, "a 80 puntos, en camino");

            model = New();
            model.Apply(HeistEventKind.AlarmConfirmed);
            Check(model.State == PoliceState.EnRoute, "alarma confirmada, directo en camino");
        }

        static void Scoring()
        {
            void Expect(HeistEventKind kind, float points)
            {
                var model = New();
                model.Apply(kind);
                Check(Mathf.Approximately(model.Suspicion, points), $"{kind} suma {points}");
            }

            Expect(HeistEventKind.HackFailed, 8f);
            Expect(HeistEventKind.HackLockout, 25f);
            Expect(HeistEventKind.GuardReported, 5f);
            Expect(HeistEventKind.MissedCheck, 20f);
            Expect(HeistEventKind.Gunshot, 35f);
            Expect(HeistEventKind.BodyFound, 60f);

            var outages = New();
            outages.Apply(HeistEventKind.Blackout);
            Check(Mathf.Approximately(outages.Suspicion, 10f), "el primer apagon suma 10");
            outages.Apply(HeistEventKind.Blackout);
            Check(Mathf.Approximately(outages.Suspicion, 30f), "el siguiente suma 20");
        }

        static void Timing()
        {
            var inRange = true;
            for (var seed = 1; seed <= 200; seed++)
            {
                var model = New(seed);
                model.Apply(HeistEventKind.AlarmConfirmed);
                inRange &= model.SecondsToArrival is >= 90f and <= 120f;
            }
            Check(inRange, "en camino tarda de 90 a 120 s");

            var verifying = New();
            verifying.Apply(HeistEventKind.BodyFound);
            Check(verifying.SecondsToArrival is >= 180f and <= 240f, "verificacion tarda de 180 a 240 s");

            var before = verifying.SecondsToArrival;
            verifying.Apply(HeistEventKind.AlarmConfirmed);
            Check(verifying.SecondsToArrival <= before && verifying.SecondsToArrival <= 120f, "escalar solo puede acortar el tiempo");

            var late = New();
            late.Apply(HeistEventKind.BodyFound);
            Advance(late, 170f);
            var nearly = late.SecondsToArrival;
            late.Apply(HeistEventKind.AlarmConfirmed);
            Check(late.SecondsToArrival <= nearly, "una unidad que ya llegaba no se retrasa al escalar");

            var arriving = New();
            arriving.Apply(HeistEventKind.AlarmConfirmed);
            var reached = new System.Collections.Generic.List<int>();
            arriving.MilestoneReached += reached.Add;
            Advance(arriving, 125f);
            Check(arriving.State == PoliceState.Arrived, "al acabar el tiempo, llegada");
            Check(reached.SequenceEqual(new[] { 0, 1, 2, 3 }), "pasa por las cuatro calles en orden");

            arriving.Apply(HeistEventKind.BodyFound);
            Check(arriving.State == PoliceState.Arrived, "tras llegar ya nada cambia");
        }

        static void Decay()
        {
            var model = New();
            model.Apply(HeistEventKind.HackLockout);
            model.Apply(HeistEventKind.HackFailed);
            var start = model.Suspicion;
            Advance(model, 40f);
            Check(Mathf.Abs(model.Suspicion - (start - 2f)) < 0.1f, "en rumor baja un punto cada 20 s");
            Advance(model, 200f);
            Check(model.State == PoliceState.Calm, "de rumor se puede volver a la calma");

            var verifying = New();
            verifying.Apply(HeistEventKind.BodyFound);
            var points = verifying.Suspicion;
            Advance(verifying, 60f);
            Check(Mathf.Approximately(verifying.Suspicion, points) && verifying.State == PoliceState.Verifying, "desde verificacion ya no baja");
        }

        static void Round()
        {
            var fired = true;
            for (var seed = 1; seed <= 30; seed++)
            {
                var model = New(seed);
                Advance(model, 12.9f * 60f);
                fired &= model.State == PoliceState.Calm;
                Advance(model, 4.2f * 60f);
                fired &= model.State >= PoliceState.Verifying;
            }
            Check(fired, "sin errores, la ronda llega entre los minutos 13 y 17");
        }

        static void Interference()
        {
            var calm = New();
            var resolved = false;
            calm.InterferenceResolved += _ => resolved = true;
            calm.Apply(HeistEventKind.InterferenceSucceeded);
            Check(!resolved && calm.State == PoliceState.Calm, "en calma, interferir se gasta para nada");

            var model = New();
            model.Apply(HeistEventKind.AlarmConfirmed);
            var before = model.SecondsToArrival;
            model.Apply(HeistEventKind.InterferenceSucceeded);
            Check(Mathf.Approximately(model.SecondsToArrival, before + 120f), "con exito, 120 s mas");

            var bad = New();
            bad.Apply(HeistEventKind.AlarmConfirmed);
            before = bad.SecondsToArrival;
            bad.Apply(HeistEventKind.InterferenceFailed);
            Check(Mathf.Approximately(bad.SecondsToArrival, before - 30f), "con fallo, 30 s menos");

            var slowed = New();
            slowed.Apply(HeistEventKind.AlarmConfirmed);
            Advance(slowed, 40f);
            var progress = slowed.RouteProgress;
            slowed.Apply(HeistEventKind.InterferenceSucceeded);
            slowed.Tick(Step);
            Check(slowed.RouteProgress >= progress, "el retraso no hace retroceder al coche");
        }

        static void Lines()
        {
            BuildingBuilder.PrepareRadioBook();
            var book = AssetDatabase.LoadAssetAtPath<RadioLineBook>(BuildingBuilder.LinesAssetPath);
            var bank = RadioVoiceTool.BuildBank();
            var keys = new[]
            {
                "police_chatter", "police_round_notice", "police_rumor", "police_verify", "police_verify_ack", "police_enroute",
                "police_enroute_ack", "police_silent", "police_route_0", "police_route_1", "police_route_2", "police_route_3",
                "police_arrived", "police_interference_ok", "police_interference_bad", "police_gunshot", "police_outage",
            };
            Check(keys.All(k => book.Pick(k) != null), "cada momento de la policia tiene su frase");

            var missing = RadioVoiceTool.AllTexts(book).Distinct().Where(t => bank.ClipFor(t) == null).ToList();
            Check(missing.Count == 0, $"todas las frases tienen audio (faltan {missing.Count})");
            Check(bank.entries.All(e => e.clip != null && e.clip.length > 0.2f), "ningun audio esta vacio");
        }
    }
}
