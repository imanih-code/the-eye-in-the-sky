using UnityEngine;

namespace EyeSky.Security
{
    /// <summary>La hoja de una puerta: gira o sube segun su estado.</summary>
    public sealed class DoorVisual : MonoBehaviour
    {
        public string doorId;
        public Transform leaf;
        public bool rollsUp;
        public float openAngle = 82f;
        public float rollHeight = 2.3f;
        public float openSeconds = 0.5f;

        float _amount;

        void Update()
        {
            var network = DoorNetwork.Instance;
            if (network == null)
                return;
            var target = network.IsOpen(doorId) ? 1f : 0f;
            _amount = Mathf.MoveTowards(_amount, target, Time.deltaTime / Mathf.Max(openSeconds, 0.01f));
            Apply(_amount);
        }

        void Apply(float amount)
        {
            if (rollsUp)
                leaf.localPosition = Vector3.up * (amount * rollHeight);
            else
                leaf.localRotation = Quaternion.Euler(0f, amount * openAngle, 0f);
        }
    }
}
