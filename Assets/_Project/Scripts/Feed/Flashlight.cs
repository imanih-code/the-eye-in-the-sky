using UnityEngine;

namespace EyeSky.Feed
{
    /// <summary>Linterna de un guardia. RoomLighting la enciende cuando su sala esta a oscuras.</summary>
    public sealed class Flashlight : MonoBehaviour
    {
        public float range = 6.5f;
        public float halfAngle = 22f;
        public float power = 1f;
    }
}
