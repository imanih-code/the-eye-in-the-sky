using UnityEngine;

namespace EyeSky.Npc
{
    /// <summary>
    /// Hace que un modelo 3D se comporte como un sprite: gira a saltos en 8 direcciones
    /// y se anima y se coloca solo unas pocas veces por segundo.
    /// Lee el movimiento de su raiz, le da igual quien la mueva.
    /// </summary>
    public sealed class IsoPuppet : MonoBehaviour
    {
        const float SnapDegrees = 45f;
        const float CrouchHeight = 0.68f;
        const float DownedLift = 0.16f;
        const float TeleportDistance = 1.5f;

        [Header("Piezas")]
        public Transform visual;
        public Transform legLeft;
        public Transform legRight;
        public Transform armLeft;
        public Transform armRight;

        [Header("Animacion")]
        public float animationFps = 10f;
        public float strideMeters = 0.85f;
        public float swingDegrees = 34f;

        /// <summary>Agachado, escondido.</summary>
        public bool Crouched { get; set; }

        /// <summary>En el suelo, fuera de combate.</summary>
        public bool Downed { get; set; }

        Vector3 _lastRootPosition;
        Vector3 _visualPosition;
        float _accumulated;
        float _phase;
        float _yaw;

        void OnEnable()
        {
            _lastRootPosition = transform.position;
            _visualPosition = transform.position;
            _yaw = visual != null ? visual.eulerAngles.y : 0f;
        }

        void LateUpdate()
        {
            _accumulated += Time.deltaTime;
            var interval = 1f / animationFps;
            if (_accumulated >= interval)
            {
                Tick(_accumulated);
                _accumulated = 0f;
            }
            // El padre se mueve cada frame; el modelo solo salta en cada paso de animacion
            visual.position = _visualPosition;
        }

        /// <summary>Orienta el modelo sin moverlo.</summary>
        public void Face(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
                _yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        }

        /// <summary>Hacia donde mira, ya con el giro a saltos.</summary>
        public Vector3 Facing => Quaternion.Euler(0f, Mathf.Round(_yaw / SnapDegrees) * SnapDegrees, 0f) * Vector3.forward;

        void Tick(float deltaTime)
        {
            var delta = transform.position - _lastRootPosition;
            delta.y = 0f;
            _lastRootPosition = transform.position;

            // Un salto de varios metros es una recolocacion, no un paso: no debe girarlo
            var distance = delta.magnitude;
            var moving = distance < TeleportDistance && distance / Mathf.Max(deltaTime, 0.0001f) > 0.05f;
            if (moving)
            {
                _phase += distance / strideMeters;
                _yaw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            }

            ApplyPose(transform.position, _yaw, moving ? Mathf.Sin(_phase * Mathf.PI * 2f) : 0f);
        }

        /// <summary>Coloca la pose directamente. Publico para capturar fuera de Play.</summary>
        public void ApplyPose(Vector3 position, float yawDegrees, float swing)
        {
            var snapped = Mathf.Round(yawDegrees / SnapDegrees) * SnapDegrees;
            _visualPosition = Downed ? position + Vector3.up * DownedLift : position;
            visual.SetPositionAndRotation(_visualPosition, Quaternion.Euler(Downed ? -90f : 0f, snapped, 0f));
            visual.localScale = new Vector3(1f, Crouched && !Downed ? CrouchHeight : 1f, 1f);

            var legAngle = Downed ? 0f : swing * swingDegrees;
            legLeft.localRotation = Quaternion.Euler(legAngle, 0f, 0f);
            legRight.localRotation = Quaternion.Euler(-legAngle, 0f, 0f);
            armLeft.localRotation = Quaternion.Euler(-legAngle * 0.8f, 0f, 0f);
            armRight.localRotation = Quaternion.Euler(legAngle * 0.8f, 0f, 0f);
        }
    }
}
