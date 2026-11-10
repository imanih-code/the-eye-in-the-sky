using System;
using System.Collections.Generic;
using System.IO;
using EyeSky.Hacking;
using EyeSky.UI;
using UnityEditor;
using UnityEngine;

namespace EyeSky.EditorTools
{
    /// <summary>
    /// Comprueba los puzzles sin partida: que siempre tengan solucion y que sean justos.
    /// Un jugador perfecto los resuelve con muchas semillas distintas.
    /// </summary>
    public static class HackPuzzleTests
    {
        const float Step = 1f / 60f;
        const int Seeds = 40;
        static readonly RectInt Area = HackSession.Area;

        static int _failures;

        static void Check(bool passed, string description)
        {
            Debug.Log($"[EYE] {(passed ? "OK" : "FALLO")}: {description}");
            if (!passed)
                _failures++;
        }

        /// <summary>Linea de comandos: ejecuta todas las comprobaciones y sale con el resultado.</summary>
        public static void RunAll()
        {
            var settings = ScriptableObject.CreateInstance<HackSettings>();
            try
            {
                Solvable("EMPALME", settings.empalme.seconds, s => new EmpalmePuzzle(settings.empalme, s));
                Solvable("PUENTE luces", settings.puenteLights.seconds, s => new PuentePuzzle(settings.puenteLights, s));
                Solvable("PUENTE puerta", settings.puenteDoor.seconds, s => new PuentePuzzle(settings.puenteDoor, s));
                Solvable("CRIPTA", settings.cripta.seconds, s => new CriptaPuzzle(settings.cripta, s));
                Solvable("HUELLA", settings.huella.seconds, s => new HuellaPuzzle(settings.huella, s), 5);
                Solvable("PARCHE facil", settings.parcheEasy.seconds, s => new ParchePuzzle(settings.parcheEasy, s));
                Solvable("PARCHE dificil", settings.parcheHard.seconds, s => new ParchePuzzle(settings.parcheHard, s));
                Solvable("INTERFERENCIA", settings.interferencia.seconds, s => new InterferenciaPuzzle(settings.interferencia, s));
                Firewall(settings);
                CodeScoring(settings);
                FingerprintFairness(settings);
                PatchUniqueness(settings);
                MistakesCount(settings);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                _failures++;
            }

            Debug.Log($"[EYE] Resultado: {_failures} fallos");
            EditorApplication.Exit(_failures == 0 ? 0 : 1);
        }

        static HackStep Run(IHackPuzzle puzzle, float seconds, out float used)
        {
            var time = 0f;
            while (time < seconds)
            {
                var input = puzzle.BotInput(Area);
                var step = puzzle.Tick(Step, input, Area);
                time += Step;
                if (step is HackStep.Solved or HackStep.AttemptFailed)
                {
                    used = time;
                    return step;
                }
            }
            used = time;
            return HackStep.Running;
        }

        static void Solvable(string name, float seconds, Func<int, IHackPuzzle> create, int seeds = Seeds)
        {
            var solved = 0;
            var longest = 0f;
            for (var seed = 1; seed <= seeds; seed++)
            {
                if (Run(create(seed * 7919), seconds, out var used) == HackStep.Solved)
                    solved++;
                longest = Mathf.Max(longest, used);
            }
            Check(solved == seeds, $"{name}: un jugador perfecto resuelve {solved}/{seeds} (el mas largo, {longest:0.0} s de {seconds:0} s)");
        }

        // El perseguidor puede ganar a un bot simple: lo que importa es que casi siempre se pueda
        static void Firewall(HackSettings settings)
        {
            const int seeds = 60;
            var solved = 0;
            var caught = 0;
            var timedOut = 0;
            var longest = 0f;
            for (var seed = 1; seed <= seeds; seed++)
            {
                var step = Run(new CortafuegosPuzzle(settings.cortafuegos, seed * 104729), settings.cortafuegos.seconds, out var used);
                if (step == HackStep.Solved)
                {
                    solved++;
                    longest = Mathf.Max(longest, used);
                }
                else if (step == HackStep.AttemptFailed)
                {
                    caught++;
                }
                else
                {
                    timedOut++;
                }
            }
            Check(solved >= seeds * 0.75f, $"CORTAFUEGOS: un bot sencillo escapa {solved}/{seeds}, le alcanzan {caught}, sin tiempo {timedOut}, la huida mas larga {longest:0.0} s");
        }

        static void CodeScoring(HackSettings settings)
        {
            CriptaPuzzle.Score("ABCDE", "ABCDE", out var exact, out var misplaced);
            Check(exact == 5 && misplaced == 0, "CRIPTA: todo bien");
            CriptaPuzzle.Score("ABCDE", "EDCBA", out exact, out misplaced);
            Check(exact == 1 && misplaced == 4, "CRIPTA: bien colocados y en otro sitio");
            CriptaPuzzle.Score("ABCDE", "FGHAB", out exact, out misplaced);
            Check(exact == 0 && misplaced == 2, "CRIPTA: solo cuenta lo que existe");

            var puzzle = new CriptaPuzzle(settings.cripta, 3);
            var failed = false;
            for (var guess = 0; guess < settings.cripta.guesses; guess++)
            {
                foreach (var symbol in puzzle.WrongGuess)
                    puzzle.Tick(Step, new HackInput { Typed = symbol }, Area);
                failed = puzzle.Tick(Step, new HackInput { Confirm = true }, Area) == HackStep.AttemptFailed;
            }
            Check(failed, "CRIPTA: agotar los codigos falla");
        }

        static void FingerprintFairness(HackSettings settings)
        {
            var weakest = float.MaxValue;
            for (var seed = 1; seed <= 12; seed++)
                weakest = Mathf.Min(weakest, new HuellaPuzzle(settings.huella, seed * 31).WeakestDifference);
            Check(weakest >= settings.huella.minimumDifference,
                $"HUELLA: la falsa mas parecida difiere {weakest:0.000} (minimo {settings.huella.minimumDifference:0.000})");

            var puzzle = new HuellaPuzzle(settings.huella, 5);
            var wrong = puzzle.Tick(Step, new HackInput { Click = true, Pointer = puzzle.WrongSamplePoint(Area) }, Area);
            Check(wrong == HackStep.AttemptFailed, "HUELLA: elegir mal gasta el intento");
        }

        static void PatchUniqueness(HackSettings settings)
        {
            var correct = true;
            for (var seed = 1; seed <= Seeds; seed++)
            {
                var puzzle = new ParchePuzzle(settings.parcheHard, seed * 13);
                correct &= puzzle.Pending.Count == settings.parcheHard.fragments;
                foreach (var fragment in puzzle.Pending)
                    correct &= puzzle.CountOccurrences(fragment) == 1;
            }
            Check(correct, "PARCHE: cada fragmento aparece exactamente una vez");
        }

        static void MistakesCount(HackSettings settings)
        {
            var puzzle = new ParchePuzzle(settings.parcheHard, 77);
            var bad = new HackInput { Confirm = true, Pointer = new Vector2Int(-5, -5) };
            HackStep last = HackStep.Running;
            for (var i = 0; i < settings.parcheHard.errors; i++)
                last = puzzle.Tick(Step, bad, Area);
            Check(last == HackStep.AttemptFailed, "PARCHE: tres errores terminan el intento");

            var tones = new InterferenciaPuzzle(settings.interferencia, 9);
            for (var time = 0f; !tones.WaitingForAnswer && time < 20f; time += Step)
                tones.Tick(Step, default, Area);
            Check(tones.WaitingForAnswer, "INTERFERENCIA: tras escuchar pide la respuesta");
            var wrong = tones.Tick(Step, new HackInput { Typed = tones.WrongDigit }, Area);
            Check(wrong == HackStep.AttemptFailed, "INTERFERENCIA: un tono mal falla");
        }

        /// <summary>Linea de comandos: -captureDir carpeta. Dibuja cada puzzle tal como se ve.</summary>
        public static void CapturePuzzles()
        {
            var args = Environment.GetCommandLineArgs();
            var directory = args[Array.IndexOf(args, "-captureDir") + 1];
            Directory.CreateDirectory(directory);
            var settings = ScriptableObject.CreateInstance<HackSettings>();

            var entries = new List<(IHackPuzzle Puzzle, float Seconds, int Attempts)>
            {
                (new EmpalmePuzzle(settings.empalme, 4), settings.empalme.seconds, settings.empalme.attempts),
                (new PuentePuzzle(settings.puenteDoor, 11), settings.puenteDoor.seconds, settings.puenteDoor.attempts),
                (new HuellaPuzzle(settings.huella, 8), settings.huella.seconds, settings.huella.attempts),
                (new CortafuegosPuzzle(settings.cortafuegos, 21), settings.cortafuegos.seconds, settings.cortafuegos.attempts),
                (new CriptaPuzzle(settings.cripta, 6), settings.cripta.seconds, 1),
                (new ParchePuzzle(settings.parcheHard, 12), settings.parcheHard.seconds, 1),
                (new InterferenciaPuzzle(settings.interferencia, 3), settings.interferencia.seconds, 1),
            };

            const int columns = 3;
            const int cellW = 800;
            const int cellH = 450;
            var montage = new Color32[columns * cellW * columns * cellH];
            for (var i = 0; i < entries.Count; i++)
            {
                var (puzzle, seconds, attempts) = entries[i];
                for (var t = 0; t < 40; t++)
                    puzzle.Tick(Step, default, Area);

                var surface = new ConsoleSurface(cellW, cellH);
                HackSession.DrawFrame(surface, puzzle, seconds * 0.6f, seconds, attempts, attempts);

                var row = columns - 1 - i / columns;
                var column = i % columns;
                for (var y = 0; y < cellH; y++)
                    Array.Copy(surface.Pixels, y * cellW, montage, (row * cellH + y) * columns * cellW + column * cellW, cellW);
            }

            var image = new Texture2D(columns * cellW, columns * cellH, TextureFormat.RGB24, false);
            image.SetPixels32(montage);
            image.Apply(false);
            File.WriteAllBytes(Path.Combine(directory, "puzzles.png"), image.EncodeToPNG());
            Debug.Log("[EYE] Puzzles capturados");
            EditorApplication.Exit(0);
        }
    }
}
