using System;
using System.Collections.Generic;
using UnityEngine;

namespace EyeSky.Security
{
    /// <summary>El plano del edificio: salas, puertas y camaras. Lo escribe el generador.</summary>
    [CreateAssetMenu(menuName = "Eye In The Sky/Plano del edificio")]
    public sealed class BuildingLayout : ScriptableObject
    {
        public enum DoorKind
        {
            Mechanical,
            Electronic,
            Biometric,
            Roller,
            Vault,
        }

        [Serializable]
        public sealed class RoomDef
        {
            public string id;
            public string label;
            [Tooltip("Como se dice por radio: 'la galeria este'.")]
            public string spoken;
            public Rect area;
            [Tooltip("Sus luces se pueden cortar desde la consola.")]
            public bool lightsHackable = true;
            public List<Vector3> hidingSpots = new();
        }

        [Serializable]
        public sealed class DoorDef
        {
            public string id;
            public Vector3 position;
            [Tooltip("La puerta esta en una pared paralela al eje X.")]
            public bool alongX;
            public string roomA;
            public string roomB;
            public DoorKind kind;
            public bool startsOpen;

            public string OtherRoom(string roomId) => roomId == roomA ? roomB : roomA;
        }

        /// <summary>El numero de camara es su posicion en la lista.</summary>
        [Serializable]
        public sealed class CameraDef
        {
            public string label;
            public string roomId;
            [Tooltip("Lo que abarca: fuera de aqui no se ve nada.")]
            public Rect view;
            public Vector3 focus;
            public int profile;
            public bool infrared;
            [Tooltip("La camara exterior es la nuestra: el operador no la ve.")]
            public bool watchedByOperator = true;
        }

        public List<RoomDef> rooms = new();
        public List<DoorDef> doors = new();
        public List<CameraDef> cameras = new();

        [Tooltip("Parte del mundo que dibuja el plano de la consola.")]
        public Rect mapBounds = new(-8f, 0f, 38f, 21f);

        public RoomDef Room(string id) => rooms.Find(r => r.id == id);

        /// <summary>La sala que contiene el punto, o null en el hueco de una puerta.</summary>
        public string RoomIdAt(Vector3 position)
        {
            var point = new Vector2(position.x, position.z);
            foreach (var room in rooms)
                if (room.area.Contains(point))
                    return room.id;
            return null;
        }

        public DoorDef DoorBetween(string roomA, string roomB)
        {
            return doors.Find(d => (d.roomA == roomA && d.roomB == roomB) || (d.roomA == roomB && d.roomB == roomA));
        }

        /// <summary>Punto a un lado de la puerta, dentro de la sala indicada.</summary>
        public Vector3 DoorSide(DoorDef door, string roomId, float distance)
        {
            var center = Room(roomId).area.center;
            var normal = door.alongX
                ? new Vector3(0f, 0f, Mathf.Sign(center.y - door.position.z))
                : new Vector3(Mathf.Sign(center.x - door.position.x), 0f, 0f);
            return door.position + normal * distance;
        }
    }
}
