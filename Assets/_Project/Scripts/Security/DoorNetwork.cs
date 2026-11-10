using System.Collections.Generic;
using UnityEngine;

namespace EyeSky.Security
{
    /// <summary>Estado de todas las puertas. Las visuales y el plano leen de aqui.</summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-60)]
    public sealed class DoorNetwork : MonoBehaviour
    {
        public static DoorNetwork Instance { get; private set; }

        public BuildingLayout layout;

        readonly Dictionary<string, bool> _open = new();
        readonly Dictionary<string, float> _lockedUntil = new();

        void OnEnable()
        {
            Instance = this;
            Reset();
        }

        public void Reset()
        {
            _open.Clear();
            if (layout == null)
                return;
            foreach (var door in layout.doors)
                _open[door.id] = door.startsOpen;
        }

        public bool IsOpen(string doorId)
        {
            return _open.TryGetValue(doorId, out var open) && open;
        }

        public void SetOpen(string doorId, bool open)
        {
            if (_open.ContainsKey(doorId))
                _open[doorId] = open;
        }

        /// <summary>Durante ese tiempo, ni con llave.</summary>
        public void LockAgainstGuards(string doorId, float seconds) => _lockedUntil[doorId] = Time.time + seconds;

        public bool IsLockedAgainstGuards(string doorId) => _lockedUntil.TryGetValue(doorId, out var until) && Time.time < until;
    }
}
