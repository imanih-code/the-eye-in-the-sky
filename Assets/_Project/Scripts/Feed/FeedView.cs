using System;
using UnityEngine;
using UnityEngine.UI;

namespace EyeSky.Feed
{
    /// <summary>
    /// Monitor principal: una sola camara a la vez, a baja resolucion y escalada por pixel entero.
    /// </summary>
    public sealed class FeedView : MonoBehaviour
    {
        public Camera feedCamera;
        public RawImage output;
        public Material compositeSource;
        public Vector2Int resolution = new(800, 450);
        public FeedProfile[] profiles;
        public string cameraLabel = "CAM 04  GALERIA ESTE";
        public Vector3 infraredCenter = new(6f, 0f, 4.5f);

        [Tooltip("Hora de la noche al empezar, en segundos desde medianoche.")]
        public float clockStart = 2f * 3600f + 15f * 60f;
        public float staticSeconds = 0.25f;

        /// <summary>Camara sin senal: solo nieve.</summary>
        public bool Snow { get; set; }

        public Material Composite { get; private set; }
        public RenderTexture Target { get; private set; }

        Texture2D _osd;
        Color32[] _osdPixels;
        string _lastOsd;
        int _profileIndex;
        float _staticLeft;
        float _elapsed;

        void Awake()
        {
            Prepare();
            SetProfile(0, false);
            output.texture = Target;
            output.material = Composite;
        }

        void OnDestroy()
        {
            if (Target != null)
                Target.Release();
        }

        /// <summary>Crea la imagen y el material. Publico para poder capturar fuera de Play.</summary>
        public void Prepare()
        {
            if (Target != null)
                return;

            Target = new RenderTexture(resolution.x, resolution.y, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                name = "FeedTarget",
            };
            Target.Create();
            feedCamera.targetTexture = Target;

            _osd = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "FeedOsd",
            };
            _osdPixels = new Color32[resolution.x * resolution.y];

            Composite = new Material(compositeSource);
            Composite.SetTexture("_OsdTex", _osd);
            Composite.SetVector("_LowRes", new Vector4(resolution.x, resolution.y, 0f, 0f));
        }

        public void SetProfile(int index, bool burst = true)
        {
            _profileIndex = (index % profiles.Length + profiles.Length) % profiles.Length;
            var profile = profiles[_profileIndex];
            profile.ApplyTo(Composite);
            Shader.SetGlobalFloat("_Eye_IRMode", profile.infrared ? 1f : 0f);
            Shader.SetGlobalVector("_Eye_IRCenter", new Vector4(infraredCenter.x, infraredCenter.y, infraredCenter.z, profile.infraredRange));
            if (burst)
                _staticLeft = staticSeconds;
        }

        public void NextProfile() => SetProfile(_profileIndex + 1);

        void Update()
        {
            _elapsed += Time.deltaTime;
            _staticLeft = Mathf.Max(0f, _staticLeft - Time.deltaTime);
            Composite.SetFloat("_Static", Snow ? 1f : staticSeconds > 0f ? Mathf.Clamp01(_staticLeft / staticSeconds) : 0f);
            DrawOsd(_elapsed, Mathf.Repeat(_elapsed, 1f) < 0.6f);
        }

        public void DrawOsd(float elapsedSeconds, bool recordingDot)
        {
            var clock = TimeSpan.FromSeconds(clockStart + elapsedSeconds);
            var time = $"{clock.Hours:00}:{clock.Minutes:00}:{clock.Seconds:00}";
            var signature = cameraLabel + time + recordingDot + _profileIndex;
            if (signature == _lastOsd)
                return;
            _lastOsd = signature;

            Array.Clear(_osdPixels, 0, _osdPixels.Length);
            var white = new Color32(232, 236, 226, 255);
            PixelFont.Draw(_osdPixels, resolution.x, resolution.y, cameraLabel, 10, 9, 2, white);
            PixelFont.Draw(_osdPixels, resolution.x, resolution.y, time, resolution.x - PixelFont.Measure(time, 2) - 10, 9, 2, white);
            if (recordingDot)
                PixelFont.Draw(_osdPixels, resolution.x, resolution.y, "*", 10, resolution.y - 25, 2, new Color32(226, 54, 48, 255));
            PixelFont.Draw(_osdPixels, resolution.x, resolution.y, "REC", 26, resolution.y - 25, 2, white);

            _osd.SetPixels32(_osdPixels);
            _osd.Apply(false);
        }
    }
}
