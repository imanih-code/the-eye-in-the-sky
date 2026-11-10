using EyeSky.Npc;
using EyeSky.Radio;
using EyeSky.Security;
using UnityEngine;

namespace EyeSky.Core
{
    public enum HeistItem
    {
        Keycard,
        Loot,
    }

    /// <summary>La tarjeta del director y el botin: quien puede cogerlos y donde estan.</summary>
    public sealed class HeistObjectives : MonoBehaviour
    {
        public static HeistObjectives Instance { get; private set; }

        public BuildingLayout layout;
        public DoorNetwork doors;
        public Vector3 keycardPoint;
        public Vector3 lootPoint;
        public Vector3 vaultDoorPoint;
        public GameObject keycardVisual;
        public GameObject lootVisual;
        public float collectSeconds = 3f;
        public float vaultReach = 3f;

        Vector3 _lootPosition;

        public bool HasKeycard { get; private set; }
        public bool LootTaken => LootCarrier != null;
        public CrewAgent LootCarrier { get; private set; }
        public bool LootEscaped => LootCarrier != null && LootCarrier.State == CrewState.Escaped;
        public bool VaultHackRunning { get; set; }

        void Awake()
        {
            Instance = this;
            _lootPosition = lootPoint;
        }

        /// <summary>Si en la sala de esta persona hay algo que le toca recoger.</summary>
        public bool TryGetItem(CrewAgent crew, out HeistItem item, out Vector3 point)
        {
            item = HeistItem.Keycard;
            point = keycardPoint;
            if (!HasKeycard && InRoom(crew, keycardPoint))
                return true;

            item = HeistItem.Loot;
            point = _lootPosition;
            return !LootTaken && InRoom(crew, _lootPosition) && CanCarryLoot(crew);
        }

        bool InRoom(CrewAgent crew, Vector3 point) => crew.Motor.CurrentRoom == layout.RoomIdAt(point);

        // Lo carga Liebre; Yunque solo si ella ha caido
        static bool CanCarryLoot(CrewAgent crew)
        {
            return crew.canPick || CrewAgent.All.TrueForAll(c => c == crew || !c.canPick || c.IsOut);
        }

        public void Collected(HeistItem item, CrewAgent crew)
        {
            if (item == HeistItem.Keycard)
            {
                HasKeycard = true;
                keycardVisual.SetActive(false);
                RadioBus.Say(RadioChannel.Crew, crew.displayName, "objective_card_taken");
                return;
            }

            LootCarrier = crew;
            lootVisual.SetActive(false);
            crew.SetCarrying(true);
            RadioBus.Say(RadioChannel.Crew, crew.displayName, "objective_loot_taken");
        }

        /// <summary>Si cae quien lo lleva, el botin se queda en el suelo.</summary>
        public void OnCrewDown(CrewAgent crew)
        {
            if (crew != LootCarrier)
                return;
            LootCarrier = null;
            _lootPosition = crew.transform.position;
            crew.SetCarrying(false);
        }

        /// <summary>Hace falta la tarjeta y alguien junto a la puerta. Si no, la banda lo dice.</summary>
        public bool CanOpenVault()
        {
            var nearby = CrewAgent.All.Find(c => !c.IsOut && Vector3.Distance(c.transform.position, vaultDoorPoint) <= vaultReach);
            if (nearby == null)
            {
                RadioBus.Say(RadioChannel.Crew, CrewAgent.All[0].displayName, "objective_need_person");
                return false;
            }
            if (!HasKeycard)
            {
                RadioBus.Say(RadioChannel.Crew, nearby.displayName, "objective_need_card");
                return false;
            }
            return true;
        }

        public void OnVaultOpened()
        {
            var nearby = CrewAgent.All.Find(c => !c.IsOut && Vector3.Distance(c.transform.position, vaultDoorPoint) <= vaultReach);
            if (nearby != null)
                RadioBus.Say(RadioChannel.Crew, nearby.displayName, "objective_vault_open");
        }

#if UNITY_EDITOR
        /// <summary>Solo para las pruebas.</summary>
        public void GiveKeycardForTests() => HasKeycard = true;
#endif
    }
}
