using System;
using System.Collections.Generic;
using UnityEngine;

namespace EyeSky.Radio
{
    /// <summary>Todas las frases de radio. Cada clave tiene varias formas de decirlo.</summary>
    [CreateAssetMenu(menuName = "Eye In The Sky/Frases de radio")]
    public sealed class RadioLineBook : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string key;
            [TextArea] public string[] variants;
        }

        public List<Entry> entries = new();

        public string Pick(string key)
        {
            var entry = entries.Find(e => e.key == key);
            if (entry == null || entry.variants.Length == 0)
                return null;
            return entry.variants[UnityEngine.Random.Range(0, entry.variants.Length)];
        }
    }
}
