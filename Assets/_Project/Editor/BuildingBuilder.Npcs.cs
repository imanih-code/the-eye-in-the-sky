using System.Collections.Generic;
using EyeSky.Core;
using EyeSky.Dev;
using EyeSky.Feed;
using EyeSky.Hacking;
using EyeSky.Npc;
using EyeSky.Police;
using EyeSky.Radio;
using EyeSky.Security;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace EyeSky.EditorTools
{
    /// <summary>La malla de navegacion, la banda, los guardias y sus frases.</summary>
    public static partial class BuildingBuilder
    {
        const string NavMeshPath = GeneratedFolder + "/Building_NavMesh.asset";
        const string SettingsPath = GeneratedFolder + "/NpcSettings.asset";
        const string LinesPath = GeneratedFolder + "/RadioLines.asset";
        public const string LinesAssetPath = LinesPath;

        public static readonly Dictionary<string, string> SpokenRoomNames = new()
        {
            ["calle"] = "la calle",
            ["muelle"] = "el muelle",
            ["pasillo"] = "el pasillo de servicio",
            ["electrico"] = "el cuarto eléctrico",
            ["seguridad"] = "seguridad",
            ["vestibulo"] = "el vestíbulo",
            ["galeria_oeste"] = "la galería oeste",
            ["oficina"] = "la oficina del director",
            ["galeria_este"] = "la galería este",
            ["antesala"] = "la antesala",
            ["camara"] = "la cámara acorazada",
        };

        static RadioBus _radio;
        const string AmbienceClipPath = "Assets/_Project/Audio/Van_Ambience.wav";

        static DoorNetwork FindDoors() => UnityEngine.Object.FindAnyObjectByType<DoorNetwork>();

        // Un bucle fijo de lluvia suave con el zumbido grave del motor
        static void WriteAmbienceClip()
        {
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(AmbienceClipPath) != null)
                return;

            const int rate = 22050;
            const int seconds = 6;
            var random = new System.Random(5);
            var samples = new short[rate * seconds];
            var soft = 0f;
            for (var i = 0; i < samples.Length; i++)
            {
                var time = i / (float)rate;
                soft += ((float)random.NextDouble() * 2f - 1f - soft) * 0.12f;
                var hum = Mathf.Sin(time * 50f * Mathf.PI * 2f) * 0.5f + Mathf.Sin(time * 100f * Mathf.PI * 2f) * 0.2f;
                samples[i] = (short)(Mathf.Clamp(soft * 1.6f + hum * 0.18f, -1f, 1f) * short.MaxValue * 0.7f);
            }

            var path = System.IO.Path.GetFullPath(AmbienceClipPath);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            using (var writer = new System.IO.BinaryWriter(System.IO.File.Create(path)))
            {
                var bytes = samples.Length * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + bytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(bytes);
                foreach (var sample in samples)
                    writer.Write(sample);
            }
            AssetDatabase.ImportAsset(AmbienceClipPath);
        }

        /// <summary>Escribe las frases sin generar la escena: las necesita el generador de voces.</summary>
        public static void PrepareRadioBook()
        {
            System.IO.Directory.CreateDirectory(GeneratedFolder);
            FillRadioLines(LoadOrCreate<RadioLineBook>(LinesPath));
            AssetDatabase.SaveAssets();
        }

        static HeistDirector CreatePolice(CameraNetwork cameras, HackSession hacks)
        {
            WriteAmbienceClip();
            var root = new GameObject("Policia");
            var police = root.AddComponent<PoliceResponse>();
            police.settings = LoadOrCreate<PoliceSettings>(GeneratedFolder + "/PoliceSettings.asset");
            police.siren = root.AddComponent<SirenAudio>();

            var arrival = root.AddComponent<PoliceArrival>();
            arrival.car = _policeCar;
            arrival.beaconRed = _beaconRed;
            arrival.beaconBlue = _beaconBlue;
            arrival.enabled = false;

            var director = root.AddComponent<HeistDirector>();
            director.police = police;
            director.arrival = arrival;
            director.cameras = cameras;
            director.hacks = hacks;

            var objectives = root.AddComponent<HeistObjectives>();
            objectives.layout = cameras.layout;
            objectives.doors = FindDoors();
            objectives.keycardPoint = new Vector3(24.8f, 0f, 15.4f);
            objectives.lootPoint = new Vector3(26f, 0f, 6f);
            objectives.vaultDoorPoint = new Vector3(24.1f, 0f, 6f);
            objectives.keycardVisual = _keycardVisual;
            objectives.lootVisual = _lootVisual;
            director.objectives = objectives;
            hacks.objectives = objectives;

            // Lluvia y el zumbido de la camioneta. Fijos: no delatan nada
            var ambience = root.AddComponent<AudioSource>();
            ambience.playOnAwake = false;
            ambience.loop = true;
            ambience.volume = 0.05f;
            ambience.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbienceClipPath);
            director.ambience = ambience;
            return director;
        }

        static readonly Dictionary<string, Vector3[]> HidingSpots = new()
        {
            ["calle"] = new[] { new Vector3(-11.6f, 0f, 15f) },
            ["muelle"] = new[] { new Vector3(-5.5f, 0f, 18.9f), new Vector3(-2.2f, 0f, 15.3f) },
            ["pasillo"] = new[] { new Vector3(9.2f, 0f, 18.9f), new Vector3(12f, 0f, 18.7f) },
            ["electrico"] = new[] { new Vector3(18.6f, 0f, 17.6f) },
            ["seguridad"] = new[] { new Vector3(0.9f, 0f, 13.2f) },
            ["vestibulo"] = new[] { new Vector3(7f, 0f, 15.3f), new Vector3(10.7f, 0f, 11f) },
            ["galeria_oeste"] = new[] { new Vector3(15.6f, 0f, 11.6f), new Vector3(17f, 0f, 11f) },
            ["oficina"] = new[] { new Vector3(24.6f, 0f, 14.2f), new Vector3(20.5f, 0f, 15.2f) },
            ["galeria_este"] = new[] { new Vector3(2.5f, 0f, 2.6f), new Vector3(9.6f, 0f, 2.6f), new Vector3(0.8f, 0f, 7.6f) },
            ["antesala"] = new[] { new Vector3(14.6f, 0f, 7.7f), new Vector3(23.7f, 0f, 7.9f) },
            ["camara"] = new[] { new Vector3(27.1f, 0f, 7.1f) },
        };

        static readonly float[] SensorBeamsX = { 22.85f, 23.25f };
        static GameObject _sensorBeams;

        static HackSession CreateHacking(CameraNetwork cameras, RoomLighting lighting, DoorNetwork doors, out SensorGrid sensors)
        {
            sensors = new GameObject("SensorGrid").AddComponent<SensorGrid>();
            sensors.zone = Rect.MinMaxRect(22.75f, 3f, 23.3f, 9f);
            sensors.beams = _sensorBeams;

            var root = new GameObject("HackSession");
            var session = root.AddComponent<HackSession>();
            session.settings = LoadOrCreate<HackSettings>(GeneratedFolder + "/HackSettings.asset");
            session.cameras = cameras;
            session.lighting = lighting;
            session.doors = doors;
            session.sensors = sensors;
            session.audioOut = root.AddComponent<HackAudio>();
            return session;
        }

        static void IgnoreForNavigation(Transform target)
        {
            target.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        }

        static void BakeNavMesh(Transform building)
        {
            AssetDatabase.DeleteAsset(NavMeshPath);
            var surface = building.gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;
            surface.BuildNavMesh();
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
            EditorUtility.SetDirty(surface);
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void CreateNpcs(BuildingLayout layout, RoomLighting lighting, DoorNetwork doors, CameraNetwork cameras)
        {
            var world = new GameObject("NpcWorld").AddComponent<NpcWorld>();
            world.settings = LoadOrCreate<NpcSettings>(SettingsPath);
            world.layout = layout;
            world.lighting = lighting;
            world.doors = doors;
            world.cameras = cameras;
            world.fuseBoxPoint = new Vector3(19.8f, 0f, 19f);
            world.vanPoint = new Vector3(-11.4f, 0f, 16.5f);
            world.gameObject.AddComponent<GuardRoster>();
            world.gameObject.AddComponent<HeistDebugOverlay>();

            var book = LoadOrCreate<RadioLineBook>(LinesPath);
            FillRadioLines(book);
            var radio = new GameObject("RadioBus").AddComponent<RadioBus>();
            radio.book = book;
            radio.clips = RadioVoiceTool.BuildBank();
            radio.voices = new List<RadioBus.SpeakerVoice>
            {
                new() { speaker = "LIEBRE", pitch = 1.14f },
                new() { speaker = "YUNQUE", pitch = 0.72f },
                new() { speaker = "RAMOS", pitch = 0.84f },
                new() { speaker = "VEGA", pitch = 0.94f },
                new() { speaker = "SOTO", pitch = 0.78f },
                new() { speaker = "BASE", pitch = 1.04f },
                new() { speaker = "CENTRAL", pitch = 1f },
                new() { speaker = "UNIDAD 7", pitch = 0.88f },
                new() { speaker = "UNIDAD 14", pitch = 0.76f },
            };
            radio.gameObject.AddComponent<RadioAudio>().bus = radio;
            _radio = radio;

            var actors = new GameObject("Personas").transform;
            AddCrew(actors, "Liebre", "LIEBRE", ActorStyle.Liebre, new Vector3(-10.4f, 0f, 16f), true, false);
            AddCrew(actors, "Yunque", "YUNQUE", ActorStyle.Yunque, new Vector3(-10.4f, 0f, 18f), false, true);

            AddGuard(actors, "Guardia", "RAMOS", new Vector3(3f, 0f, 5.8f), new[]
            {
                new Vector3(3f, 0f, 5.8f), new Vector3(9f, 0f, 3.2f), new Vector3(10f, 0f, 11.5f),
                new Vector3(15.5f, 0f, 14.3f), new Vector3(6f, 0f, 11f),
            });
            AddGuard(actors, "Guardia_Servicio", "VEGA", new Vector3(14f, 0f, 18f), new[]
            {
                new Vector3(2f, 0f, 18f), new Vector3(14f, 0f, 18f), new Vector3(18.5f, 0f, 19f),
                new Vector3(14f, 0f, 18f), new Vector3(9f, 0f, 12f),
            });
            AddGuard(actors, "Guardia_Oficina", "SOTO", new Vector3(21.5f, 0f, 11f), new[]
            {
                new Vector3(21.5f, 0f, 11f), new Vector3(22f, 0f, 6.5f), new Vector3(14f, 0f, 5.5f),
                new Vector3(10.5f, 0f, 5f), new Vector3(14f, 0f, 5.5f), new Vector3(22f, 0f, 6.5f),
            });

            var watcher = AddGuard(actors, "Operador", "BASE", new Vector3(3.3f, 0f, 12.6f), null);
            watcher.isOperator = true;
        }

        static void AddCrew(Transform parent, string objectName, string displayName, ActorStyle style, Vector3 position, bool canPick, bool canFight)
        {
            var root = CreateActor(parent, objectName, style, position, 45f);
            ConfigureAgent(root);
            root.AddComponent<ActorMotor>();
            var crew = root.AddComponent<CrewAgent>();

            var bag = new EyeSky.Art.MeshBuilder();
            bag.Box(new Vector3(0f, 1.18f, -0.3f), new Vector3(0.46f, 0.56f, 0.32f), EyeSky.Art.MeshBuilder.Hex("#5a4a2a"), 0.8f);
            bag.Box(new Vector3(0f, 1.48f, -0.3f), new Vector3(0.2f, 0.1f, 0.2f), EyeSky.Art.MeshBuilder.Hex("#e8c04f"));
            var bagObject = AddObject(root.transform.Find("Visual"), "Saco_Botin", bag, _actor, Vector3.zero, true).gameObject;
            bagObject.SetActive(false);
            crew.carriedBag = bagObject;
            crew.displayName = displayName;
            crew.canPick = canPick;
            crew.canFight = canFight;
        }

        static GuardAgent AddGuard(Transform parent, string objectName, string displayName, Vector3 position, Vector3[] patrol)
        {
            var root = CreateActor(parent, objectName, ActorStyle.Guard, position, 225f);
            ConfigureAgent(root);
            root.AddComponent<ActorMotor>();
            var guard = root.AddComponent<GuardAgent>();
            guard.displayName = displayName;
            guard.patrol = patrol ?? new Vector3[0];
            return guard;
        }

        static void ConfigureAgent(GameObject root)
        {
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.acceleration = 30f;
            agent.angularSpeed = 0f;
            agent.stoppingDistance = 0.05f;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            agent.updateRotation = false;
        }

        static void FillRadioLines(RadioLineBook book)
        {
            book.entries.Clear();

            void Add(string key, params string[] variants)
            {
                book.entries.Add(new RadioLineBook.Entry { key = key, variants = variants });
            }

            Add("ack_go", "Voy para allá.", "Copiado, Ojo. En marcha.", "Vale, me muevo.");
            Add("ack_append", "Luego ahí, copiado.", "Anotado.");
            Add("ack_stop", "Quieto.", "Me paro.", "Alto, copiado.");
            Add("ack_hide", "Me escondo.", "Buscando dónde meterme.");
            Add("ack_knockout", "Déjamelo a mí.", "Va a dormir un rato.");
            Add("ack_kill", "¿Seguro? Vale.", "Copiado. Sin testigos.");
            Add("ack_retreat", "Nos vamos. A la camioneta.", "Retirada, copiado.");
            Add("arrived", "En {0}, Ojo.", "Ya estoy en {0}.");
            Add("idle_ask", "Ojo, ¿por dónde?", "Sigo aquí. ¿Qué hago?", "Ojo, ¿me copias? Necesito ruta.");
            Add("door_system", "Esta puerta va por sistema, Ojo. Ábremela.", "Cerrada, y es cosa tuya. Dale.");
            Add("door_cannot", "Está cerrada y no sé abrir esto.", "Aquí hace falta Liebre.");
            Add("picking", "Dame unos segundos con esta cerradura.", "Forzando. No me metas prisa.");
            Add("picked", "Abierta.", "Listo, pasamos.");
            Add("hear_steps", "Oigo pasos cerca, Ojo.", "Viene alguien. ¿Qué hago?");
            Add("spotted", "¡Me han visto!", "¡Mierda, me ha visto!");
            Add("captured", "Me han cogido, Ojo...", "Estoy fuera. Lo siento.");
            Add("takedown_done", "Dormido.", "Uno menos.");
            Add("shot_done", "Hecho. Eso se ha oído.", "Ya está. Vámonos pronto.");
            Add("escaped", "En la camioneta.", "Dentro. Arranca cuando quieras.");

            Add("check_call", "Control de las tres. Reportad.", "Base a todos, control.");
            Add("check_ok", "Todo tranquilo.", "Sin novedad.");
            Add("check_miss", "{0}, responde. {0}, ¿me copias?");
            Add("check_base_silent", "Base, ¿estás ahí? Base no contesta.");
            Add("strange", "He oído algo. Voy a mirar.", "Hay ruido por aquí, compruebo.");
            Add("alarm_cam", "¡Tengo a alguien en {0}! Aviso.");
            Add("alarm_saw", "¡Intruso en {0}! ¡Doy aviso!", "¡Alto! ¡Hay alguien en {0}!");
            Add("body", "¡Hay un compañero en el suelo en {0}!");
            Add("lights_go", "Se ha ido la luz. Voy al cuadro eléctrico.", "Voy a mirar el cuadro.");
            Add("lights_done", "Luz restaurada.", "Ya está, era el diferencial.");
            Add("woke", "Me han golpeado. Hay gente dentro.");

            Add("objective_intro", "Ojo, estamos en la calle. La persiana y la cámara del muelle son tuyas. Vamos a por la tarjeta del director.");
            Add("objective_card_taken", "Tengo la tarjeta. Ahora a la cámara acorazada, Ojo.");
            Add("objective_loot_taken", "Lo tengo. Vámonos a la camioneta.");
            Add("objective_need_person", "Ojo, necesitamos a alguien junto a esa puerta primero.");
            Add("objective_need_card", "Sin la tarjeta del director no abrimos esto.");
            Add("objective_vault_open", "Abierta. Madre mía.");

            Add("police_chatter",
                "Unidad doce, aparcamiento mal puesto en la calle Mayor. Sin urgencia.",
                "Central a unidades, se busca moto robada, color rojo, zona norte.",
                "Unidad tres, disputa vecinal resuelta. Volvemos a ruta.",
                "Unidad nueve, control de alcoholemia en la rotonda. Sin novedades.",
                "Central, aviso por ruido en un bar del centro. Unidad cinco, atienda.",
                "Unidad ocho, perro suelto en el parque. Lo tenemos.",
                "Central a unidad dos, confirme su posición. Copiado, gracias.",
                "Unidad seis, cambio de turno a las cuatro. Recibido.");
            Add("police_round_notice", "Unidad siete, recuerde la ronda por la calle Ferrer a las dos y media.");
            Add("police_rumor", "Central a unidades. Llamada rara desde la zona de Ferrer. Sin confirmar.", "Un vecino dice haber visto algo en Ferrer. De momento, nada.");
            Add("police_verify", "Unidad siete, cuando quede libre, compruebe la casa de subastas de la calle Ferrer.");
            Add("police_verify_ack", "Unidad siete, copiado. Saliendo sin prisa.");
            Add("police_enroute", "Central a todas las unidades. Robo en curso, casa de subastas, calle Ferrer. Prioridad.");
            Add("police_enroute_ack", "Unidad catorce, voy para allá.");
            Add("police_silent", "Aproximación sin sirenas. Repito, sin sirenas.");
            Add("police_route_0", "Pasando la avenida del Puerto.");
            Add("police_route_1", "Cruzando el puente Mayor.");
            Add("police_route_2", "A la altura de la plaza del Reloj.");
            Add("police_route_3", "Entrando en la calle Ferrer.");
            Add("police_arrived", "Estamos en la puerta. Entramos.");
            Add("police_interference_ok", "Central a unidades. Falsa alarma en Ferrer, cancelen. Repito, cancelen.");
            Add("police_interference_bad", "¿Quién ha emitido eso? Ignoren. Mantengan el rumbo.");
            Add("police_gunshot", "Una vecina reporta detonaciones cerca de la calle Ferrer.");
            Add("police_outage", "La compañía eléctrica avisa de un corte parcial en Ferrer.");
            EditorUtility.SetDirty(book);
        }
    }
}
