using System;
using System.Collections.Generic;
using UnityEngine;

namespace EyeSky.Core
{
    public enum HeistEventKind
    {
        HackFailed,
        HackLockout,
        Blackout,
        GuardReported,
        MissedCheck,
        Gunshot,
        BodyFound,
        AlarmConfirmed,
        InterferenceSucceeded,
        InterferenceFailed,
    }

    /// <summary>Hechos que le importan a la policia. Quien los provoca no sabe que pasara despues.</summary>
    public static class HeistEvents
    {
        public readonly struct Entry
        {
            public readonly HeistEventKind Kind;
            public readonly Vector3 Position;
            public readonly string Detail;
            public readonly float Time;

            public Entry(HeistEventKind kind, Vector3 position, string detail, float time)
            {
                Kind = kind;
                Position = position;
                Detail = detail;
                Time = time;
            }
        }

        public static event Action<Entry> Raised;

        /// <summary>Para el informe final y la depuracion.</summary>
        public static readonly List<Entry> Log = new();

        public static void Raise(HeistEventKind kind, Vector3 position, string detail)
        {
            var entry = new Entry(kind, position, detail, UnityEngine.Time.time);
            Log.Add(entry);
            Raised?.Invoke(entry);
        }

        public static int Count(HeistEventKind kind)
        {
            var count = 0;
            foreach (var entry in Log)
                if (entry.Kind == kind)
                    count++;
            return count;
        }

        public static void Clear()
        {
            Log.Clear();
        }
    }
}
