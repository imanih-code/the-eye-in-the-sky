using UnityEngine;

namespace EyeSky.Police
{
    /// <summary>Todo lo que se afina de la policia. El jugador nunca ve ninguno de estos numeros.</summary>
    [CreateAssetMenu(menuName = "Eye In The Sky/Ajustes de policia")]
    public sealed class PoliceSettings : ScriptableObject
    {
        [Header("Umbrales de sospecha")]
        public float rumorAt = 25f;
        public float verifyAt = 50f;
        public float enRouteAt = 80f;
        [Tooltip("Segundos para que baje un punto, solo en calma y rumor.")]
        public float secondsPerDecayPoint = 20f;

        [Header("Tiempo hasta llegar")]
        public float verifySecondsMin = 180f;
        public float verifySecondsMax = 240f;
        public float enRouteSecondsMin = 90f;
        public float enRouteSecondsMax = 120f;

        [Header("Ronda programada (segundos desde el inicio)")]
        public float roundSecondsMin = 13f * 60f;
        public float roundSecondsMax = 17f * 60f;

        [Header("Lo que suma cada hecho")]
        public float hackFailed = 8f;
        public float hackLockout = 25f;
        public float firstBlackout = 10f;
        public float nextBlackout = 20f;
        public float guardReported = 5f;
        public float missedCheck = 20f;
        public float gunshot = 35f;
        public float bodyFound = 60f;

        [Header("Interferencia")]
        public float interferenceBonusSeconds = 120f;
        public float interferencePenaltySeconds = 30f;

        [Header("Llegada")]
        [Range(0f, 1f)] public float silentApproachChance = 0.35f;
        public float sirenSeconds = 20f;
        [Tooltip("Si cae toda la banda, los guardias llaman y esto es lo que tardan.")]
        public float allCrewDownSeconds = 25f;

        [Header("Charla de radio")]
        public float chatterSecondsMin = 22f;
        public float chatterSecondsMax = 48f;
        public float roundNoticeSeconds = 35f;
    }
}
