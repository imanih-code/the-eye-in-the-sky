using System;
using System.Collections.Generic;
using UnityEngine;

namespace EyeSky.Feed
{
    /// <summary>
    /// Fuente unica de la luz: el shader de la imagen y la vision de los NPC leen estos datos.
    /// Asi la oscuridad no puede ser solo estetica.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-50)]
    public sealed class RoomLighting : MonoBehaviour
    {
        public const int MaxRooms = 16;
        public const int MaxLamps = 48;
        public const int MaxSpots = 8;

        [Serializable]
        public sealed class Room
        {
            public string id;
            public Rect area;
            public bool lit = true;
            [Range(0f, 1f)] public float brightness = 1f;
        }

        [Serializable]
        public sealed class Lamp
        {
            public Vector3 position;
            public float radius = 3f;
            public float power = 0.5f;
            public string roomId;
            [Tooltip("Sigue encendida aunque se corte la luz de la sala.")]
            public bool emergency;
        }

        public static RoomLighting Instance { get; private set; }

        /// <summary>Cuando se corta o vuelve la luz de una sala.</summary>
        public event Action<string, bool> LitChanged;

        [Header("Luz")]
        [Range(0f, 1.5f)] public float ambient = 0.86f;
        [Range(0f, 1f)] public float outsideLevel = 0f;
        [Range(2f, 8f)] public float bands = 6f;
        [Range(0f, 1f)] public float dither = 0.16f;
        [Range(0f, 1f)] public float shadowStrength = 0.36f;
        public Color darkColor = new(0.002f, 0.0025f, 0.004f, 1f);

        [Header("Mapa")]
        public List<Room> rooms = new();
        public List<Lamp> lamps = new();

        readonly Vector4[] _roomRects = new Vector4[MaxRooms];
        readonly float[] _roomLevels = new float[MaxRooms];
        readonly Vector4[] _lampPositions = new Vector4[MaxLamps];
        readonly Vector4[] _lampInfo = new Vector4[MaxLamps];
        readonly Vector4[] _spotPositions = new Vector4[MaxSpots];
        readonly Vector4[] _spotDirections = new Vector4[MaxSpots];
        readonly Vector4[] _spotRects = new Vector4[MaxSpots];
        readonly float[] _spotPowers = new float[MaxSpots];

        void OnEnable()
        {
            Instance = this;
            Push();
        }

        void LateUpdate()
        {
            Push();
        }

        public void SetLit(string roomId, bool lit)
        {
            var room = rooms.Find(r => r.id == roomId);
            if (room == null || room.lit == lit)
                return;
            room.lit = lit;
            LitChanged?.Invoke(roomId, lit);
        }

        public bool IsLit(string roomId)
        {
            var room = rooms.Find(r => r.id == roomId);
            return room == null || room.lit;
        }

        /// <summary>Lo que preguntara la simulacion: hay luz en este punto.</summary>
        public bool IsLitAt(Vector3 position)
        {
            var index = RoomIndexAt(position);
            return index < 0 || rooms[index].lit;
        }

        int RoomIndexAt(Vector3 position)
        {
            for (var i = 0; i < rooms.Count; i++)
                if (rooms[i].area.Contains(new Vector2(position.x, position.z)))
                    return i;
            return -1;
        }

        public void Push()
        {
            var roomCount = Mathf.Min(rooms.Count, MaxRooms);
            for (var i = 0; i < roomCount; i++)
            {
                var area = rooms[i].area;
                _roomRects[i] = new Vector4(area.xMin, area.yMin, area.xMax, area.yMax);
                _roomLevels[i] = rooms[i].lit ? rooms[i].brightness : 0f;
            }

            var lampCount = Mathf.Min(lamps.Count, MaxLamps);
            for (var i = 0; i < lampCount; i++)
            {
                var lamp = lamps[i];
                _lampPositions[i] = new Vector4(lamp.position.x, lamp.position.y, lamp.position.z, lamp.radius);
                _lampInfo[i] = new Vector4(rooms.FindIndex(r => r.id == lamp.roomId), lamp.power, lamp.emergency ? 1f : 0f, 0f);
            }

            var spotCount = PushFlashlights();

            Shader.SetGlobalVectorArray("_Eye_RoomRects", _roomRects);
            Shader.SetGlobalFloatArray("_Eye_RoomLevels", _roomLevels);
            Shader.SetGlobalFloat("_Eye_RoomCount", roomCount);
            Shader.SetGlobalVectorArray("_Eye_Lamps", _lampPositions);
            Shader.SetGlobalVectorArray("_Eye_LampInfo", _lampInfo);
            Shader.SetGlobalFloat("_Eye_LampCount", lampCount);
            Shader.SetGlobalVectorArray("_Eye_SpotPos", _spotPositions);
            Shader.SetGlobalVectorArray("_Eye_SpotDir", _spotDirections);
            Shader.SetGlobalVectorArray("_Eye_SpotRect", _spotRects);
            Shader.SetGlobalFloatArray("_Eye_SpotPower", _spotPowers);
            Shader.SetGlobalFloat("_Eye_SpotCount", spotCount);
            Shader.SetGlobalFloat("_Eye_Ambient", ambient);
            Shader.SetGlobalFloat("_Eye_OutsideLevel", outsideLevel);
            Shader.SetGlobalFloat("_Eye_Bands", bands);
            Shader.SetGlobalFloat("_Eye_Dither", dither);
            Shader.SetGlobalFloat("_Eye_ShadowStrength", shadowStrength);
            Shader.SetGlobalColor("_Eye_DarkColor", darkColor);
        }

        // Las linternas solo se encienden a oscuras y no atraviesan las paredes de su sala
        int PushFlashlights()
        {
            var count = 0;
            var all = FindObjectsByType<Flashlight>(FindObjectsInactive.Exclude);
            foreach (var flashlight in all)
            {
                if (count >= MaxSpots)
                    break;
                if (!flashlight.enabled)
                    continue;
                var origin = flashlight.transform.position;
                var index = RoomIndexAt(origin);
                if (index < 0 || rooms[index].lit)
                    continue;

                var direction = flashlight.transform.forward;
                var area = rooms[index].area;
                _spotPositions[count] = new Vector4(origin.x, origin.y, origin.z, flashlight.range);
                _spotDirections[count] = new Vector4(direction.x, direction.y, direction.z,
                    Mathf.Cos(flashlight.halfAngle * Mathf.Deg2Rad));
                _spotRects[count] = new Vector4(area.xMin, area.yMin, area.xMax, area.yMax);
                _spotPowers[count] = flashlight.power;
                count++;
            }
            return count;
        }
    }
}
