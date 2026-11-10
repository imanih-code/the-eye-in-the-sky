using UnityEngine;

namespace EyeSky.Police
{
    /// <summary>El coche patrulla frente al edificio, con las luces alternando.</summary>
    public sealed class PoliceArrival : MonoBehaviour
    {
        const float FlashSeconds = 0.22f;

        public GameObject car;
        public GameObject beaconRed;
        public GameObject beaconBlue;

        float _clock;
        bool _red = true;

        public void Show()
        {
            car.SetActive(true);
            beaconRed.SetActive(true);
            beaconBlue.SetActive(false);
            enabled = true;
        }

        void Update()
        {
            _clock += Time.deltaTime;
            if (_clock < FlashSeconds)
                return;
            _clock = 0f;
            _red = !_red;
            beaconRed.SetActive(_red);
            beaconBlue.SetActive(!_red);
        }
    }
}
