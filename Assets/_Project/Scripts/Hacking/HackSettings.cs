using System;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>Todo lo que se afina de los hackeos.</summary>
    [CreateAssetMenu(menuName = "Eye In The Sky/Ajustes de hackeo")]
    public sealed class HackSettings : ScriptableObject
    {
        [Serializable]
        public sealed class Empalme
        {
            public float seconds = 12f;
            public int attempts = 3;
            public float tolerance = 0.07f;
            public float holdSeconds = 1.5f;
            public float driftPerSecond = 0.035f;
        }

        [Serializable]
        public sealed class Puente
        {
            public float seconds = 25f;
            public int attempts = 2;
            public int size = 6;
            public int shorts = 4;
            public float sweepSeconds = 1.1f;
        }

        [Serializable]
        public sealed class Huella
        {
            public float seconds = 25f;
            public int attempts = 2;
            public int fingers = 3;
            public int samples = 8;
            public int blurRadius = 2;
            public int focusedBlurRadius = 1;
            [Range(2, 5)] public int changedMinutiae = 3;
            [Tooltip("Diferencia minima visible, tras el desenfoque, entre la buena y una falsa.")]
            public float minimumDifference = 0.1f;
        }

        [Serializable]
        public sealed class Cortafuegos
        {
            public float seconds = 40f;
            public int attempts = 2;
            public int roomsWide = 11;
            public int roomsHigh = 6;
            public float cellsPerSecond = 7f;
            public float hunterStartRatio = 0.85f;
            public float hunterRatioPerKey = 0.06f;
            public int keys = 3;
        }

        [Serializable]
        public sealed class Cripta
        {
            public float seconds = 75f;
            public int guesses = 6;
            public int length = 5;
            public int symbols = 8;
            public bool allowRepeats;
        }

        [Serializable]
        public sealed class Parche
        {
            public float seconds = 35f;
            public int errors = 3;
            public int fragments = 3;
            public int fragmentLength = 6;
            public int decoysPerFragment = 3;
            public float rowStepMin = 0.28f;
            public float rowStepMax = 0.65f;
        }

        [Serializable]
        public sealed class Interferencia
        {
            public float seconds = 32f;
            public int tonesMin = 6;
            public int tonesMax = 7;
            public float toneSeconds = 0.42f;
        }

        [Header("Efectos")]
        public float loopSeconds = 40f;
        public float sensorOffSeconds = 60f;
        public float doorLockSeconds = 30f;
        public float lockoutSeconds = 60f;

        [Header("Puzzles")]
        public Empalme empalme = new();
        public Puente puenteLights = new() { seconds = 20f, size = 5, shorts = 3, sweepSeconds = 1.6f };
        public Puente puenteDoor = new();
        public Huella huella = new();
        public Cortafuegos cortafuegos = new();
        public Cripta cripta = new();
        public Parche parcheEasy = new() { fragments = 2, fragmentLength = 4, decoysPerFragment = 2, rowStepMin = 0.5f, rowStepMax = 1f };
        public Parche parcheHard = new();
        public Interferencia interferencia = new();
    }
}
