using System.Collections.Generic;
using EyeSky.Feed;
using UnityEngine;

namespace EyeSky.Security
{
    /// <summary>
    /// Las camaras del edificio: solo una activa. Mueve la camara real a su sitio,
    /// enciende los modelos de esa sala y recorta lo que queda fuera.
    /// </summary>
    public sealed class CameraNetwork : MonoBehaviour
    {
        const int InfraredProfile = 2;
        const float CameraDistance = 32f;

        public BuildingLayout layout;
        public FeedView feed;
        [Tooltip("Una raiz por sala, en el orden del plano.")]
        public Transform[] roomRoots;
        public int startCamera = 1;

        public int Current { get; private set; } = -1;
        public bool InfraredOn { get; private set; }
        public BuildingLayout.CameraDef CurrentDef => Current >= 0 ? layout.cameras[Current] : null;

        readonly List<Renderer[]> _roomRenderers = new();

        readonly Dictionary<int, float> _loopLeft = new();
        readonly HashSet<int> _cut = new();

        void Start()
        {
            Show(startCamera, false);
        }

        void Update()
        {
            foreach (var index in new List<int>(_loopLeft.Keys))
            {
                _loopLeft[index] -= Time.deltaTime;
                if (_loopLeft[index] <= 0f)
                    _loopLeft.Remove(index);
            }
        }

        /// <summary>Mete la camara en bucle: durante ese tiempo no detecta a nadie.</summary>
        public void Loop(int index, float seconds) => _loopLeft[index] = seconds;

        public bool IsLooped(int index) => _loopLeft.ContainsKey(index);

        public float LoopSecondsLeft(int index) => _loopLeft.TryGetValue(index, out var left) ? left : 0f;

        /// <summary>Esa camara deja de emitir para siempre.</summary>
        public void Cut(int index)
        {
            _cut.Add(index);
            if (Current == index)
                Show(index, false);
        }

        public bool IsCut(int index) => _cut.Contains(index);

        void Prepare()
        {
            if (_roomRenderers.Count > 0)
                return;
            foreach (var root in roomRoots)
                _roomRenderers.Add(root.GetComponentsInChildren<Renderer>(true));
        }

        public void Show(int index, bool burst = true)
        {
            if (index < 0 || index >= layout.cameras.Count)
                return;
            Prepare();
            Current = index;
            InfraredOn = false;

            var camera = layout.cameras[index];
            for (var i = 0; i < _roomRenderers.Count; i++)
            {
                var visible = layout.rooms[i].id == camera.roomId;
                foreach (var renderer in _roomRenderers[i])
                    renderer.enabled = visible;
            }

            var rotation = Quaternion.Euler(30f, 45f, 0f);
            feed.feedCamera.transform.SetPositionAndRotation(camera.focus - rotation * Vector3.forward * CameraDistance, rotation);

            var view = camera.view;
            Shader.SetGlobalVector("_Eye_ViewRect", new Vector4(view.xMin, view.yMin, view.xMax, view.yMax));
            Shader.SetGlobalFloat("_Eye_ViewRoom", layout.rooms.FindIndex(r => r.id == camera.roomId));
            feed.infraredCenter = camera.focus;
            feed.cameraLabel = _cut.Contains(index) ? $"CAM {index:00}  SIN ENLACE" : $"CAM {index:00}  {camera.label}";
            feed.Snow = _cut.Contains(index);
            feed.SetProfile(camera.profile, burst);
        }

        /// <summary>Solo en las camaras que lo tienen.</summary>
        public void ToggleInfrared()
        {
            var camera = CurrentDef;
            if (camera == null || !camera.infrared)
                return;
            InfraredOn = !InfraredOn;
            feed.SetProfile(InfraredOn ? InfraredProfile : camera.profile);
        }
    }
}
