using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace EyeSky.Radio
{
    /// <summary>
    /// El audio de cada frase. El archivo se llama como el resumen de su texto,
    /// asi una grabacion real sustituye a la sintetica con solo reemplazar el .wav.
    /// </summary>
    [CreateAssetMenu(menuName = "Eye In The Sky/Banco de voces")]
    public sealed class RadioClipBank : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string hash;
            public AudioClip clip;
        }

        public List<Entry> entries = new();

        Dictionary<string, AudioClip> _lookup;

        public static string HashOf(string text)
        {
            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
            var builder = new StringBuilder(16);
            for (var i = 0; i < 8; i++)
                builder.Append(bytes[i].ToString("x2"));
            return builder.ToString();
        }

        public AudioClip ClipFor(string text)
        {
            if (_lookup == null)
            {
                _lookup = new Dictionary<string, AudioClip>();
                foreach (var entry in entries)
                    _lookup[entry.hash] = entry.clip;
            }
            return _lookup.TryGetValue(HashOf(text), out var clip) ? clip : null;
        }
    }
}
