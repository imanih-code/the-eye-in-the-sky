using UnityEngine;

namespace EyeSky.Feed
{
    /// <summary>Como se ve una camara: cuanto ensucia el filtro su imagen.</summary>
    [CreateAssetMenu(menuName = "Eye In The Sky/Perfil de camara")]
    public sealed class FeedProfile : ScriptableObject
    {
        [Range(0f, 1f)] public float scanline = 0.14f;
        [Range(0f, 1f)] public float noise = 0.1f;
        [Range(0f, 0.05f)] public float noiseFloor = 0.004f;
        [Range(0f, 3f)] public float chroma = 1f;
        [Range(0f, 1f)] public float desaturate = 0.3f;
        public Color tint = Color.white;
        [Range(0f, 1f)] public float vignette = 0.35f;
        [Range(0f, 1f)] public float tracking = 0.5f;

        [Header("Infrarrojo")]
        public bool infrared;
        public float infraredRange = 6f;

        public void ApplyTo(Material material)
        {
            material.SetFloat("_Scanline", scanline);
            material.SetFloat("_Noise", noise);
            material.SetFloat("_NoiseFloor", noiseFloor);
            material.SetFloat("_Chroma", chroma);
            material.SetFloat("_Desaturate", desaturate);
            material.SetColor("_Tint", tint);
            material.SetFloat("_Vignette", vignette);
            material.SetFloat("_Tracking", tracking);
        }
    }
}
