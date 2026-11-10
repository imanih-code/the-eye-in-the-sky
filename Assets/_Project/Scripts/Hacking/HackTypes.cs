using EyeSky.UI;
using UnityEngine;

namespace EyeSky.Hacking
{
    public enum HackStep
    {
        Running,
        Solved,
        /// <summary>Un error que no termina el intento, pero deja rastro.</summary>
        Mistake,
        AttemptFailed,
    }

    /// <summary>Lo que el jugador hace este frame, en coordenadas de la consola.</summary>
    public struct HackInput
    {
        public Vector2Int Pointer;
        public bool Click;
        public bool Held;
        /// <summary>Direccion mantenida.</summary>
        public Vector2Int Move;
        /// <summary>Direccion pulsada, con repeticion.</summary>
        public Vector2Int Step;
        public bool Confirm;
        public bool Backspace;
        public char Typed;
    }

    public interface IHackPuzzle
    {
        string Title { get; }
        string Hint { get; }
        HackStep Tick(float deltaTime, in HackInput input, RectInt area);
        void Draw(ConsoleSurface surface, RectInt area);

#if UNITY_EDITOR
        /// <summary>Lo que haria un jugador perfecto: solo para las pruebas.</summary>
        HackInput BotInput(RectInt area);
#endif
    }

    public static class HackPalette
    {
        public static readonly Color32 Back = new(4, 8, 7, 255);
        public static readonly Color32 Grid = new(22, 46, 38, 255);
        public static readonly Color32 Line = new(44, 92, 74, 255);
        public static readonly Color32 Text = new(150, 232, 176, 255);
        public static readonly Color32 Dim = new(72, 120, 96, 255);
        public static readonly Color32 Amber = new(236, 186, 86, 255);
        public static readonly Color32 Red = new(232, 78, 62, 255);
        public static readonly Color32 Flow = new(96, 244, 164, 255);
    }
}
