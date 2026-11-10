using System;
using System.Collections.Generic;
using EyeSky.Npc;
using EyeSky.Police;

namespace EyeSky.Core
{
    public enum HeistOutcome
    {
        InProgress,
        Won,
        WalkedAway,
        Caught,
    }

    /// <summary>El informe final: ahora si se cuenta todo lo que estaba oculto.</summary>
    public sealed class HeistReport
    {
        public string Title;
        public string Headline;
        public string Stats;
        public readonly List<PoliceResponse.TimelineEntry> Timeline = new();

        static readonly string[] RoutePlaces = { "COMISARIA", "AV. DEL PUERTO", "PUENTE MAYOR", "PLAZA DEL RELOJ", "CALLE FERRER" };

        /// <summary>La hora en el reloj de las camaras.</summary>
        public static string ClockAt(float secondsFromStart)
        {
            var time = TimeSpan.FromSeconds(HeistDirector.ClockStartSeconds + secondsFromStart);
            return $"{time.Hours:00}:{time.Minutes:00}:{time.Seconds:00}";
        }

        public static HeistReport Build(HeistOutcome outcome, PoliceResponse police, float elapsedSeconds)
        {
            var report = new HeistReport();
            report.Timeline.AddRange(police.Timeline);

            report.Title = outcome switch
            {
                HeistOutcome.Won => "GOLPE COMPLETADO",
                HeistOutcome.WalkedAway => "SALISTEIS SIN EL BOTIN",
                _ => "CONEXION PERDIDA",
            };

            if (police.BetrayedBy != null)
                report.Headline = $"TE DELATO: {police.BetrayedBy} A LAS {ClockAt(police.BetrayedAt)}";
            else
                report.Headline = "NADIE LLEGO A LLAMAR A LA POLICIA";

            var neutralized = 0;
            foreach (var guard in GuardAgent.All)
                if (guard.IsDown)
                    neutralized++;
            var failedHacks = HeistEvents.Count(HeistEventKind.HackFailed);

            var duration = TimeSpan.FromSeconds(elapsedSeconds);
            var whereUnit = police.Model.State >= PoliceState.Verifying
                ? $"LA UNIDAD LLEGO A {PlaceOf(police.Model)}"
                : "NINGUNA UNIDAD SALIO";
            report.Stats = $"DURACION {duration.Minutes:00}:{duration.Seconds:00}   GUARDIAS FUERA {neutralized}   HACKEOS FALLIDOS {failedHacks}   {whereUnit}";
            return report;
        }

        static string PlaceOf(PoliceModel model)
        {
            if (model.State == PoliceState.Arrived)
                return "LA PUERTA";
            var index = 0;
            while (index < PoliceModel.RouteMilestones.Length && model.RouteProgress >= PoliceModel.RouteMilestones[index])
                index++;
            return RoutePlaces[index];
        }
    }
}
