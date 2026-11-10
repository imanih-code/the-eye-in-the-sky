using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using EyeSky.Radio;
using UnityEditor;
using UnityEngine;

namespace EyeSky.EditorTools
{
    /// <summary>
    /// El camino de las voces: Unity escribe la lista de frases, un script genera los .wav
    /// y el banco los enlaza. Para poner una voz real basta con reemplazar el archivo.
    /// </summary>
    public static class RadioVoiceTool
    {
        public const string ClipFolder = "Assets/_Project/Audio/Radio";
        const string BankPath = "Assets/_Project/Generated/RadioClips.asset";

        static readonly string[] SpokenRooms = BuildingBuilder.SpokenRoomNames.Values.ToArray();

        static readonly string[] SpokenCameras =
        {
            "la cámara uno", "la cámara dos", "la cámara tres", "la cámara cuatro",
            "la cámara cinco", "la cámara seis", "la cámara siete", "la cámara ocho",
        };

        static readonly Dictionary<string, string[]> Arguments = new()
        {
            ["arrived"] = SpokenRooms,
            ["alarm_saw"] = SpokenRooms,
            ["body"] = SpokenRooms,
            ["alarm_cam"] = SpokenCameras,
            ["check_miss"] = new[] { "RAMOS", "VEGA", "SOTO" },
            ["check_base_silent"] = new[] { "BASE" },
        };

        /// <summary>Todas las frases que pueden llegar a decirse, ya con sus valores.</summary>
        public static IEnumerable<string> AllTexts(RadioLineBook book)
        {
            foreach (var entry in book.entries)
            foreach (var variant in entry.variants)
            {
                if (!Arguments.TryGetValue(entry.key, out var values) || !variant.Contains("{0}"))
                {
                    yield return variant;
                    continue;
                }
                foreach (var value in values)
                    yield return string.Format(variant, value);
            }
        }

        /// <summary>Linea de comandos: -manifest archivo</summary>
        public static void WriteManifest()
        {
            var args = Environment.GetCommandLineArgs();
            var path = args[Array.IndexOf(args, "-manifest") + 1];

            BuildingBuilder.PrepareRadioBook();
            var book = AssetDatabase.LoadAssetAtPath<RadioLineBook>(BuildingBuilder.LinesAssetPath);
            var lines = AllTexts(book).Distinct().Select(t => $"{RadioClipBank.HashOf(t)}\t{t}").ToList();
            File.WriteAllLines(path, lines, new UTF8Encoding(false));
            Debug.Log($"[EYE] Manifiesto: {lines.Count} frases");
            EditorApplication.Exit(0);
        }

        /// <summary>Enlaza cada .wav de la carpeta con su frase.</summary>
        public static RadioClipBank BuildBank()
        {
            var bank = AssetDatabase.LoadAssetAtPath<RadioClipBank>(BankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<RadioClipBank>();
                AssetDatabase.CreateAsset(bank, BankPath);
            }

            bank.entries.Clear();
            if (AssetDatabase.IsValidFolder(ClipFolder))
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ClipFolder }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    bank.entries.Add(new RadioClipBank.Entry
                    {
                        hash = Path.GetFileNameWithoutExtension(path),
                        clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path),
                    });
                }
            }
            EditorUtility.SetDirty(bank);
            return bank;
        }
    }
}
