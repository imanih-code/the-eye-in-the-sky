#if UNITY_EDITOR
using System.Text;
using EyeSky.Core;
using EyeSky.Npc;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EyeSky.Dev
{
    /// <summary>F3: el estado oculto de la partida. Solo existe en el editor, nunca en el build.</summary>
    public sealed class HeistDebugOverlay : MonoBehaviour
    {
        bool _visible;
        readonly StringBuilder _text = new();

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
                _visible = !_visible;
        }

        void OnGUI()
        {
            if (!_visible)
                return;

            _text.Clear();
            var police = FindAnyObjectByType<EyeSky.Police.PoliceResponse>();
            if (police != null)
                _text.AppendLine($"POLICIA: {police.Model.State}  sospecha {police.Model.Suspicion:0}  llegada {police.Model.SecondsToArrival:0}s  silencio {police.Model.SilentApproach}");
            foreach (var crew in CrewAgent.All)
                _text.AppendLine($"{crew.displayName}: {crew.State}  sala {crew.Motor.CurrentRoom}");
            foreach (var guard in GuardAgent.All)
                _text.AppendLine($"{guard.displayName}: {guard.State}  sala {guard.Motor.CurrentRoom}");
            _text.AppendLine("--- hechos ---");
            var first = Mathf.Max(0, HeistEvents.Log.Count - 12);
            for (var i = first; i < HeistEvents.Log.Count; i++)
                _text.AppendLine($"{HeistEvents.Log[i].Time:0}s {HeistEvents.Log[i].Kind}: {HeistEvents.Log[i].Detail}");

            GUI.Box(new Rect(10f, 10f, 420f, 340f), GUIContent.none);
            GUI.Label(new Rect(16f, 14f, 410f, 335f), _text.ToString());
        }
    }
}
#endif
