using System;
using EyeSky.Feed;
using UnityEngine;

namespace EyeSky.UI
{
    /// <summary>Lienzo de baja resolucion de la consola. El origen (0, 0) es la esquina superior izquierda.</summary>
    public sealed class ConsoleSurface
    {
        readonly int _width;
        readonly int _height;
        readonly Color32[] _pixels;

        public Texture2D Texture { get; }

        public ConsoleSurface(int width, int height)
        {
            _width = width;
            _height = height;
            _pixels = new Color32[width * height];
            Texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "ConsoleSurface",
            };
        }

        public void Clear(Color32 color)
        {
            Array.Fill(_pixels, color);
        }

        public void Fill(int x, int y, int width, int height, Color32 color)
        {
            var xEnd = Mathf.Min(x + width, _width);
            var yEnd = Mathf.Min(y + height, _height);
            for (var py = Mathf.Max(y, 0); py < yEnd; py++)
            {
                var row = (_height - 1 - py) * _width;
                for (var px = Mathf.Max(x, 0); px < xEnd; px++)
                    _pixels[row + px] = color;
            }
        }

        public void Fill(RectInt rect, Color32 color) => Fill(rect.x, rect.y, rect.width, rect.height, color);

        public void Frame(RectInt rect, Color32 color)
        {
            Fill(rect.x, rect.y, rect.width, 1, color);
            Fill(rect.x, rect.y + rect.height - 1, rect.width, 1, color);
            Fill(rect.x, rect.y, 1, rect.height, color);
            Fill(rect.x + rect.width - 1, rect.y, 1, rect.height, color);
        }

        public void Text(string text, int x, int y, Color32 color, int scale = 1)
        {
            PixelFont.DrawPlain(_pixels, _width, _height, WithoutAccents(text), x, y, color, scale);
        }

        // La letra de pixeles solo tiene mayusculas sin acentos
        static string WithoutAccents(string text)
        {
            var decomposed = text.Normalize(System.Text.NormalizationForm.FormD);
            var builder = new System.Text.StringBuilder(decomposed.Length);
            foreach (var character in decomposed)
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark)
                    builder.Append(character);
            return builder.ToString();
        }

        public void Present()
        {
            Texture.SetPixels32(_pixels);
            Texture.Apply(false);
        }

        /// <summary>Solo para capturas.</summary>
        public Color32[] Pixels => _pixels;
    }
}
