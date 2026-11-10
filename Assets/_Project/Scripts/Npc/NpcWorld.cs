using EyeSky.Feed;
using EyeSky.Security;
using UnityEngine;
using UnityEngine.AI;

namespace EyeSky.Npc
{
    /// <summary>Lo que los NPC necesitan del mundo, en un solo sitio.</summary>
    [DefaultExecutionOrder(-70)]
    public sealed class NpcWorld : MonoBehaviour
    {
        public static NpcWorld Instance { get; private set; }

        public NpcSettings settings;
        public BuildingLayout layout;
        public RoomLighting lighting;
        public DoorNetwork doors;
        public CameraNetwork cameras;
        public Vector3 fuseBoxPoint;
        public Vector3 vanPoint;

        void Awake()
        {
            Instance = this;
        }

        /// <summary>Linea de vision: misma sala o puerta abierta, y sin nada que tape.</summary>
        public bool HasLineOfSight(Vector3 from, string fromRoom, Vector3 to, string toRoom)
        {
            if (fromRoom != toRoom)
            {
                var door = layout.DoorBetween(fromRoom, toRoom);
                if (door == null || !doors.IsOpen(door.id))
                    return false;
            }
            return !NavMesh.Raycast(from, to, out _, NavMesh.AllAreas);
        }

        /// <summary>El sonido pasa a la sala contigua aunque la puerta este cerrada.</summary>
        public bool CanHear(string fromRoom, string toRoom)
        {
            return fromRoom == toRoom || layout.DoorBetween(fromRoom, toRoom) != null;
        }
    }
}
