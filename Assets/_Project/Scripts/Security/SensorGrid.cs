using EyeSky.Core;
using EyeSky.Npc;
using UnityEngine;

namespace EyeSky.Security
{
    /// <summary>Los rayos de la antesala: cruzarlos activados es alarma directa.</summary>
    public sealed class SensorGrid : MonoBehaviour
    {
        const float AlarmCooldownSeconds = 15f;

        public Rect zone;
        public GameObject beams;

        float _offLeft;
        float _cooldown;

        public bool Armed => _offLeft <= 0f;
        public float OffSecondsLeft => Mathf.Max(0f, _offLeft);

        public void DisableFor(float seconds)
        {
            _offLeft = seconds;
            beams.SetActive(false);
        }

        void Update()
        {
            _cooldown -= Time.deltaTime;
            if (_offLeft > 0f)
            {
                _offLeft -= Time.deltaTime;
                if (_offLeft <= 0f)
                    beams.SetActive(true);
                return;
            }

            if (_cooldown > 0f)
                return;

            foreach (var crew in CrewAgent.All)
            {
                if (crew.IsOut || !zone.Contains(new Vector2(crew.transform.position.x, crew.transform.position.z)))
                    continue;
                _cooldown = AlarmCooldownSeconds;
                HeistEvents.Raise(HeistEventKind.AlarmConfirmed, crew.transform.position, $"sensores: {crew.displayName}");
                return;
            }
        }
    }
}
