using UnityEngine;

namespace EyeSky.Npc
{
    /// <summary>Todo lo que se afina de la banda y los guardias.</summary>
    [CreateAssetMenu(menuName = "Eye In The Sky/Ajustes de NPC")]
    public sealed class NpcSettings : ScriptableObject
    {
        [Header("Banda")]
        public float liebreSpeed = 1.75f;
        public float yunqueSpeed = 1.6f;
        public float orderDelayMin = 0.5f;
        public float orderDelayMax = 1.5f;
        public float pickSeconds = 6f;
        public float knockoutSeconds = 2.5f;
        public float knockoutReach = 1.1f;
        [Tooltip("Angulo desde el frente del guardia a partir del cual se llega por detras.")]
        public float behindAngle = 100f;
        public float shootRange = 7f;
        public float idleAskSeconds = 14f;
        public float doorNagSeconds = 15f;
        public float hearFootstepsRadius = 4.5f;
        public float autoHideSeconds = 4f;
        public float engageDistance = 4f;
        public float liebreNoiseRadius = 1.5f;
        public float yunqueNoiseRadius = 3.5f;
        public float pickNoiseRadius = 3f;

        [Header("Guardias")]
        public float patrolSpeed = 1.4f;
        public float chaseSpeed = 3f;
        public float patrolPauseMin = 1.5f;
        public float patrolPauseMax = 4f;
        public float sightRange = 9f;
        public float sightHalfAngle = 55f;
        [Tooltip("A oscuras solo se ve dentro de la linterna, con este alcance relativo.")]
        public float darkRangeFactor = 0.4f;
        public float flashlightHalfAngle = 22f;
        public float spotSeconds = 0.7f;
        public float hiddenFindDistance = 1.2f;
        public float hearSeconds = 1.5f;
        [Tooltip("Ventana de aviso: neutralizarlo antes corta la alarma.")]
        public float reportSeconds = 3f;
        public float captureDistance = 1.2f;
        public float captureSeconds = 1.5f;
        public float lookAroundSeconds = 4f;
        public float gunshotRadius = 14f;
        public float wakeSeconds = 240f;

        [Header("Vigilancia")]
        public float checkIntervalSeconds = 180f;
        public float cameraDetectSeconds = 2f;
        public float restoreDelayMin = 10f;
        public float restoreDelayMax = 25f;
        public float restoreWorkSeconds = 5f;
    }
}
