using System.Collections.Generic;
using UnityEngine;

namespace EyeSky.Hacking
{
    /// <summary>
    /// Genera huellas: lineas concentricas deformadas con puntos caracteristicos.
    /// Cada punto caracteristico es un par de dislocaciones que solo cambian las lineas de su zona.
    /// </summary>
    public static class Fingerprints
    {
        public const int Size = 96;
        const int MinutiaPairs = 6;
        // Unos 7 pixeles por linea: mas finas, el desenfoque las borraria y seria adivinar
        const float RidgeCycles = 7f;

        public struct Minutia
        {
            public Vector2 A;
            public Vector2 B;
        }

        public sealed class Family
        {
            public Vector2 Core;
            public float Stretch;
            public float WarpA;
            public float WarpB;
            public float Phase;
        }

        public static Family NewFamily(System.Random random)
        {
            return new Family
            {
                Core = new Vector2(Range(random, -0.2f, 0.2f), Range(random, -0.25f, 0.25f)),
                Stretch = Range(random, 0.55f, 0.9f),
                WarpA = Range(random, 0f, 6.28f),
                WarpB = Range(random, 0f, 6.28f),
                Phase = Range(random, 0f, 6.28f),
            };
        }

        static float Range(System.Random random, float min, float max) => min + (float)random.NextDouble() * (max - min);

        public static Minutia NewMinutia(System.Random random)
        {
            var center = new Vector2(Range(random, -0.55f, 0.55f), Range(random, -0.65f, 0.65f));
            var angle = Range(random, 0f, 6.28f);
            var half = Range(random, 0.09f, 0.14f);
            var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * half;
            return new Minutia { A = center - offset, B = center + offset };
        }

        public static List<Minutia> NewMinutiae(System.Random random)
        {
            var list = new List<Minutia>();
            for (var i = 0; i < MinutiaPairs; i++)
                list.Add(NewMinutia(random));
            return list;
        }

        /// <summary>Una copia con algunos puntos caracteristicos en otro sitio.</summary>
        public static List<Minutia> Altered(List<Minutia> original, int changes, System.Random random)
        {
            var copy = new List<Minutia>(original);
            var indices = new List<int>();
            for (var i = 0; i < copy.Count; i++)
                indices.Add(i);
            for (var i = 0; i < changes; i++)
            {
                var pick = random.Next(indices.Count);
                copy[indices[pick]] = NewMinutia(random);
                indices.RemoveAt(pick);
            }
            return copy;
        }

        public static float[] Render(Family family, List<Minutia> minutiae)
        {
            var pixels = new float[Size * Size];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var u = (x + 0.5f) / Size * 2f - 1f;
                var v = (y + 0.5f) / Size * 2f - 1f;
                if (u * u / 0.72f + v * v / 0.95f > 1f)
                    continue;

                var du = u - family.Core.x;
                var dv = v - family.Core.y;
                var radial = Mathf.Sqrt(du * du + dv * dv * family.Stretch);
                radial += 0.07f * Mathf.Sin(3f * u + family.WarpA) * Mathf.Cos(3f * v + family.WarpB);

                var phase = radial * RidgeCycles * Mathf.PI * 2f + family.Phase;
                foreach (var minutia in minutiae)
                {
                    phase += Mathf.Atan2(v - minutia.A.y, u - minutia.A.x);
                    phase -= Mathf.Atan2(v - minutia.B.y, u - minutia.B.x);
                }
                pixels[y * Size + x] = 0.5f + 0.5f * Mathf.Cos(phase);
            }
            return pixels;
        }

        public static float[] Blur(float[] source, int radius)
        {
            if (radius <= 0)
                return (float[])source.Clone();

            var horizontal = new float[source.Length];
            var result = new float[source.Length];
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var sum = 0f;
                for (var k = -radius; k <= radius; k++)
                    sum += source[y * Size + Mathf.Clamp(x + k, 0, Size - 1)];
                horizontal[y * Size + x] = sum / (radius * 2 + 1);
            }
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var sum = 0f;
                for (var k = -radius; k <= radius; k++)
                    sum += horizontal[Mathf.Clamp(y + k, 0, Size - 1) * Size + x];
                result[y * Size + x] = sum / (radius * 2 + 1);
            }
            return result;
        }

        public static float[] WithNoise(float[] source, float amount, System.Random random)
        {
            var result = new float[source.Length];
            for (var i = 0; i < source.Length; i++)
                result[i] = source[i] <= 0f ? 0f : Mathf.Clamp01(source[i] + (float)(random.NextDouble() - 0.5) * amount);
            return result;
        }

        /// <summary>La mayor diferencia media entre dos huellas en cualquier zona de 24x24 pixeles.</summary>
        public static float StrongestLocalDifference(float[] a, float[] b)
        {
            const int window = 24;
            const int stride = 8;
            var strongest = 0f;
            for (var y = 0; y + window <= Size; y += stride)
            for (var x = 0; x + window <= Size; x += stride)
            {
                var sum = 0f;
                for (var wy = 0; wy < window; wy++)
                for (var wx = 0; wx < window; wx++)
                    sum += Mathf.Abs(a[(y + wy) * Size + x + wx] - b[(y + wy) * Size + x + wx]);
                strongest = Mathf.Max(strongest, sum / (window * window));
            }
            return strongest;
        }
    }
}
