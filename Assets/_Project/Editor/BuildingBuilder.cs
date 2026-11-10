using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DoorKind = EyeSky.Security.BuildingLayout.DoorKind;
using EyeSky.Art;
using EyeSky.Feed;
using EyeSky.Security;
using EyeSky.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace EyeSky.EditorTools
{
    /// <summary>
    /// Genera la casa de subastas. Cada sala construye solo sus dos paredes del fondo y un muro cortado
    /// en los lados cercanos: asi cada camara ve su sala abierta, sin compartir geometria entre salas.
    /// </summary>
    public static partial class BuildingBuilder
    {
        const string LayoutPath = GeneratedFolder + "/Building_Layout.asset";
        const float RoomMargin = 0.22f;
        const float ViewMargin = 0.25f;
        const float StubHeight = 0.22f;
        const float DoorHeight = 2.25f;

        enum Finish
        {
            Gallery,
            Lobby,
            Service,
            Office,
            Vault,
            Street,
        }

        sealed class RoomSpec
        {
            public string Id;
            public string Label;
            public Rect Area;
            public Finish Finish;
            public float Brightness = 1f;
            public bool Hackable = true;
            public bool HasNorthWall = true;
            public Action<Transform> Dress;
            public Transform Root;
        }

        sealed class DoorSpec
        {
            public string Id;
            public bool AlongX;
            public float At;
            public float Along;
            public float Width = 1.4f;
            public float Height = DoorHeight;
            public string RoomA;
            public string RoomB;
            public DoorKind Kind = DoorKind.Mechanical;
            public bool StartsOpen;
            public bool ExitSign;

            public float Min => Along - Width * 0.5f;
            public float Max => Along + Width * 0.5f;
            public Vector3 Position => AlongX ? new Vector3(Along, 0f, At) : new Vector3(At, 0f, Along);
        }

        static List<RoomSpec> PlanRooms()
        {
            return new List<RoomSpec>
            {
                new() { Id = "calle", Label = "CALLE", Area = Rect.MinMaxRect(-20f, 12f, -8.2f, 22f), Finish = Finish.Street, Brightness = 0.66f, Hackable = false, HasNorthWall = false, Dress = DressStreet },
                new() { Id = "muelle", Label = "MUELLE", Area = Rect.MinMaxRect(-8f, 13f, -0.2f, 21f), Finish = Finish.Service, Brightness = 0.8f, Dress = DressDock },
                new() { Id = "pasillo", Label = "PASILLO SERVICIO", Area = Rect.MinMaxRect(0f, 16.4f, 16f, 19.4f), Finish = Finish.Service, Brightness = 0.72f, Dress = DressCorridor },
                new() { Id = "electrico", Label = "CUARTO ELECTRICO", Area = Rect.MinMaxRect(16.2f, 16.4f, 21f, 21f), Finish = Finish.Service, Brightness = 0.7f, Dress = DressElectrical },
                new() { Id = "seguridad", Label = "SEGURIDAD", Area = Rect.MinMaxRect(0f, 9.2f, 5f, 16.2f), Finish = Finish.Service, Brightness = 0.85f, Dress = DressSecurity },
                new() { Id = "vestibulo", Label = "VESTIBULO", Area = Rect.MinMaxRect(5.2f, 9.2f, 12f, 16.2f), Finish = Finish.Lobby, Dress = DressLobby },
                new() { Id = "galeria_oeste", Label = "GALERIA OESTE", Area = Rect.MinMaxRect(12.2f, 9.2f, 19f, 16.2f), Finish = Finish.Gallery, Dress = DressWestGallery },
                new() { Id = "oficina", Label = "OFICINA DIRECTOR", Area = Rect.MinMaxRect(19.2f, 9.2f, 26f, 16.2f), Finish = Finish.Office, Brightness = 0.9f, Dress = DressOffice },
                new() { Id = "galeria_este", Label = "GALERIA ESTE", Area = Rect.MinMaxRect(0f, 0f, 12f, 9f), Finish = Finish.Gallery, Dress = DressEastGallery },
                new() { Id = "antesala", Label = "ANTESALA", Area = Rect.MinMaxRect(12.2f, 3f, 24f, 9f), Finish = Finish.Lobby, Brightness = 0.85f, Dress = DressAnteroom },
                new() { Id = "camara", Label = "CAMARA ACORAZADA", Area = Rect.MinMaxRect(24.2f, 3f, 30f, 9f), Finish = Finish.Vault, Brightness = 0.85f, Dress = DressVault },
            };
        }

        static List<DoorSpec> PlanDoors()
        {
            return new List<DoorSpec>
            {
                new() { Id = "calle_muelle", AlongX = false, At = -8.1f, Along = 17f, Width = 2.8f, Height = 2.4f, RoomA = "calle", RoomB = "muelle", Kind = DoorKind.Roller },
                new() { Id = "muelle_pasillo", AlongX = false, At = -0.1f, Along = 18f, RoomA = "muelle", RoomB = "pasillo" },
                new() { Id = "pasillo_seguridad", AlongX = true, At = 16.3f, Along = 2.5f, RoomA = "seguridad", RoomB = "pasillo" },
                new() { Id = "pasillo_vestibulo", AlongX = true, At = 16.3f, Along = 8.5f, RoomA = "vestibulo", RoomB = "pasillo", Kind = DoorKind.Electronic },
                new() { Id = "pasillo_electrico", AlongX = false, At = 16.1f, Along = 18f, RoomA = "pasillo", RoomB = "electrico" },
                new() { Id = "vestibulo_oeste", AlongX = false, At = 12.1f, Along = 12.7f, RoomA = "vestibulo", RoomB = "galeria_oeste", StartsOpen = true },
                new() { Id = "vestibulo_este", AlongX = true, At = 9.1f, Along = 8.5f, RoomA = "galeria_este", RoomB = "vestibulo", ExitSign = true, StartsOpen = true },
                new() { Id = "oeste_oficina", AlongX = false, At = 19.1f, Along = 12.7f, RoomA = "galeria_oeste", RoomB = "oficina", Kind = DoorKind.Biometric },
                new() { Id = "oficina_antesala", AlongX = true, At = 9.1f, Along = 22f, RoomA = "antesala", RoomB = "oficina" },
                new() { Id = "este_antesala", AlongX = false, At = 12.1f, Along = 6f, RoomA = "galeria_este", RoomB = "antesala", Kind = DoorKind.Electronic },
                new() { Id = "antesala_camara", AlongX = false, At = 24.1f, Along = 6f, Width = 1.6f, RoomA = "antesala", RoomB = "camara", Kind = DoorKind.Vault },
            };
        }

        public static void Build(string scenePath)
        {
            Directory.CreateDirectory(GeneratedFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            AssetDatabase.DeleteAsset(MeshesPath);
            Meshes.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CreateMaterials();

            var rooms = PlanRooms();
            var doors = PlanDoors();
            var building = new GameObject("Edificio").transform;
            foreach (var room in rooms)
            {
                room.Root = new GameObject($"Sala_{room.Id}").transform;
                room.Root.SetParent(building, false);
                BuildRoomShell(room, doors);
                room.Dress?.Invoke(room.Root);
            }
            foreach (var door in doors)
                BuildFarDoor(door, rooms);

            BakeNavMesh(building);

            var layout = WriteLayout(rooms, doors);

            var lighting = CreateLighting(rooms);
            var feed = CreateFeed(out var console);

            var doorNetwork = new GameObject("DoorNetwork").AddComponent<DoorNetwork>();
            doorNetwork.layout = layout;

            var cameraNetwork = new GameObject("CameraNetwork").AddComponent<CameraNetwork>();
            cameraNetwork.layout = layout;
            cameraNetwork.feed = feed;
            cameraNetwork.roomRoots = rooms.Select(r => r.Root).ToArray();
            cameraNetwork.startCamera = 0;

            CreateNpcs(layout, lighting, doorNetwork, cameraNetwork);

            console.feed = feed;
            console.cameras = cameraNetwork;
            console.lighting = lighting;
            console.doors = doorNetwork;
            console.layout = layout;
            console.hacks = CreateHacking(cameraNetwork, lighting, doorNetwork, out var sensors);
            console.sensors = sensors;
            console.radio = _radio;
            console.director = CreatePolice(cameraNetwork, console.hacks);

            SaveMeshes();
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(scenePath, true) };
            AssetDatabase.SaveAssets();
        }

        // --- Estructura ----------------------------------------------------------------

        static Material FloorFor(Finish finish)
        {
            var (name, a, b, seam, pattern) = finish switch
            {
                Finish.Gallery => ("FloorGallery", "#a9763f", "#8e5f33", "#553a22", 1f),
                Finish.Lobby => ("FloorLobby", "#d9d4c8", "#b4afa4", "#8f8b83", 3f),
                Finish.Office => ("FloorOffice", "#6f4a30", "#5d3d27", "#3a2619", 1f),
                Finish.Vault => ("FloorVault", "#596067", "#50575e", "#33383d", 3f),
                Finish.Street => ("FloorStreet", "#3c3f44", "#383b40", "#2e3034", 3f),
                _ => ("FloorService", "#808384", "#7a7d7e", "#5c5f60", 3f),
            };
            var material = NewSurface(name, MeshBuilder.Hex(a));
            material.SetColor("_Color2", MeshBuilder.Hex(b));
            material.SetColor("_Color3", MeshBuilder.Hex(seam));
            material.SetFloat("_Pattern", pattern);
            return material;
        }

        static Material WallFor(Finish finish)
        {
            var (name, top, panel, trim) = finish switch
            {
                Finish.Gallery => ("WallGallery", "#6f8f86", "#7a5a3e", "#d3b779"),
                Finish.Lobby => ("WallLobby", "#b9b2a1", "#6b5a48", "#c9a85c"),
                Finish.Office => ("WallOffice", "#7d5a48", "#4a3325", "#b08a52"),
                Finish.Vault => ("WallVault", "#6a7278", "#454c52", "#8a939a"),
                Finish.Street => ("WallStreet", "#7a4a3a", "#5a3a2e", "#3c3c40"),
                _ => ("WallService", "#8d9293", "#5f6465", "#444a4c"),
            };
            var material = NewSurface(name, MeshBuilder.Hex(top));
            material.SetColor("_Color2", MeshBuilder.Hex(panel));
            material.SetColor("_Color3", MeshBuilder.Hex(trim));
            material.SetFloat("_Pattern", 2f);
            return material;
        }

        static void BuildRoomShell(RoomSpec room, List<DoorSpec> doors)
        {
            var area = room.Area;
            const float t = WallThickness;

            var floor = new MeshBuilder().Box(new Vector3(area.center.x, -0.1f, area.center.y),
                new Vector3(area.width + t * 2f, 0.2f, area.height + t * 2f), Color.white);
            AddObject(room.Root, "Suelo", floor, FloorFor(room.Finish), Vector3.zero, false);

            var mine = doors.Where(d => d.RoomA == room.Id || d.RoomB == room.Id).ToList();
            var north = mine.Where(d => d.AlongX && Mathf.Abs(d.At - (area.yMax + t * 0.5f)) < 0.05f).ToList();
            var east = mine.Where(d => !d.AlongX && Mathf.Abs(d.At - (area.xMax + t * 0.5f)) < 0.05f).ToList();
            var south = mine.Where(d => d.AlongX && Mathf.Abs(d.At - (area.yMin - t * 0.5f)) < 0.05f).ToList();
            var west = mine.Where(d => !d.AlongX && Mathf.Abs(d.At - (area.xMin - t * 0.5f)) < 0.05f).ToList();

            var walls = new MeshBuilder();
            if (room.HasNorthWall)
                AddWall(walls, area.xMin - t, area.xMax + t, area.yMax + t * 0.5f, true, north);
            AddWall(walls, area.yMin - t, area.yMax, area.xMax + t * 0.5f, false, east);
            AddObject(room.Root, "Paredes", walls, WallFor(room.Finish), Vector3.zero, false);

            var stubs = new MeshBuilder();
            var cut = MeshBuilder.Hex("#2b2830");
            AddStub(stubs, area.xMin, area.xMax, area.yMin - t * 0.5f, true, south, cut);
            AddStub(stubs, area.yMin - t, area.yMax + t, area.xMin - t * 0.5f, false, west, cut);
            IgnoreForNavigation(AddObject(room.Root, "Muros_Cortados", stubs, _prop, Vector3.zero, false));
        }

        static IEnumerable<(float from, float to)> SolidSpans(float start, float end, List<DoorSpec> holes)
        {
            var cursor = start;
            foreach (var hole in holes.OrderBy(h => h.Along))
            {
                if (hole.Min > cursor)
                    yield return (cursor, hole.Min);
                cursor = hole.Max;
            }
            if (end > cursor)
                yield return (cursor, end);
        }

        static void AddWall(MeshBuilder walls, float start, float end, float across, bool alongX, List<DoorSpec> holes)
        {
            foreach (var (from, to) in SolidSpans(start, end, holes))
                AddWallBox(walls, from, to, across, alongX, 0f, WallHeight);
            foreach (var hole in holes)
                AddWallBox(walls, hole.Min, hole.Max, across, alongX, hole.Height, WallHeight);
        }

        static void AddWallBox(MeshBuilder walls, float from, float to, float across, bool alongX, float bottom, float top)
        {
            var middle = (from + to) * 0.5f;
            var y = (bottom + top) * 0.5f;
            var height = top - bottom;
            if (alongX)
                walls.Box(new Vector3(middle, y, across), new Vector3(to - from, height, WallThickness), Color.white);
            else
                walls.Box(new Vector3(across, y, middle), new Vector3(WallThickness, height, to - from), Color.white);
        }

        static void AddStub(MeshBuilder stubs, float start, float end, float across, bool alongX, List<DoorSpec> holes, Color color)
        {
            foreach (var (from, to) in SolidSpans(start, end, holes))
            {
                var middle = (from + to) * 0.5f;
                if (alongX)
                    stubs.Box(new Vector3(middle, StubHeight * 0.5f, across), new Vector3(to - from, StubHeight, WallThickness), color);
                else
                    stubs.Box(new Vector3(across, StubHeight * 0.5f, middle), new Vector3(WallThickness, StubHeight, to - from), color);
            }
        }

        // La puerta solo se dibuja en la sala donde cae en una pared del fondo
        static void BuildFarDoor(DoorSpec door, List<RoomSpec> rooms)
        {
            const float t = WallThickness;
            var owner = rooms.First(r => r.Id == door.RoomA || r.Id == door.RoomB);
            foreach (var room in rooms.Where(r => r.Id == door.RoomA || r.Id == door.RoomB))
            {
                var isFar = door.AlongX
                    ? Mathf.Abs(door.At - (room.Area.yMax + t * 0.5f)) < 0.05f
                    : Mathf.Abs(door.At - (room.Area.xMax + t * 0.5f)) < 0.05f;
                if (isFar)
                    owner = room;
            }

            var holder = new GameObject($"Puerta_{door.Id}").transform;
            holder.SetParent(owner.Root, false);
            IgnoreForNavigation(holder);
            var hinge = door.AlongX
                ? new Vector3(door.Min, 0f, door.At)
                : new Vector3(door.At, 0f, door.Max);
            holder.SetPositionAndRotation(hinge, Quaternion.Euler(0f, door.AlongX ? 0f : 90f, 0f));

            var w = door.Width;
            var h = door.Height;
            var trim = MeshBuilder.Hex("#2c2520");
            var frame = new MeshBuilder();
            frame.Box(new Vector3(-0.06f, h * 0.5f, -0.12f), new Vector3(0.14f, h + 0.1f, 0.06f), trim);
            frame.Box(new Vector3(w + 0.06f, h * 0.5f, -0.12f), new Vector3(0.14f, h + 0.1f, 0.06f), trim);
            frame.Box(new Vector3(w * 0.5f, h + 0.04f, -0.12f), new Vector3(w + 0.26f, 0.12f, 0.06f), trim);
            AddObject(holder, "Marco", frame, _prop, Vector3.zero, false);

            var leaf = new MeshBuilder();
            switch (door.Kind)
            {
                case DoorKind.Roller:
                    leaf.Box(new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(w, h, 0.08f), MeshBuilder.Hex("#6b7278"));
                    for (var y = 0.3f; y < h; y += 0.3f)
                        leaf.Box(new Vector3(w * 0.5f, y, -0.05f), new Vector3(w, 0.05f, 0.02f), MeshBuilder.Hex("#4c5358"));
                    break;
                case DoorKind.Vault:
                    leaf.Box(new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(w, h, 0.16f), MeshBuilder.Hex("#8a939a"));
                    leaf.Box(new Vector3(w * 0.5f, h * 0.55f, -0.1f), new Vector3(0.7f, 0.7f, 0.04f), MeshBuilder.Hex("#646d74"));
                    leaf.Box(new Vector3(w * 0.5f, h * 0.55f, -0.14f), new Vector3(0.5f, 0.08f, 0.05f), Brass);
                    leaf.Box(new Vector3(w * 0.5f, h * 0.55f, -0.14f), new Vector3(0.08f, 0.5f, 0.05f), Brass);
                    break;
                default:
                    leaf.Box(new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(w, h, 0.08f), MeshBuilder.Hex("#46525c"));
                    leaf.Box(new Vector3(w * 0.5f, h - 0.55f, -0.05f), new Vector3(0.6f, 0.5f, 0.02f), MeshBuilder.Hex("#1a2530"));
                    leaf.Box(new Vector3(w - 0.14f, 1.05f, -0.08f), new Vector3(0.1f, 0.06f, 0.1f), Brass);
                    break;
            }
            var leafTransform = AddObject(holder, "Hoja", leaf, _prop, Vector3.zero, false);

            var visual = holder.gameObject.AddComponent<DoorVisual>();
            visual.doorId = door.Id;
            visual.leaf = leafTransform;
            visual.rollsUp = door.Kind == DoorKind.Roller;

            if (door.ExitSign)
            {
                var sign = new MeshBuilder().Box(new Vector3(w * 0.5f, h + 0.32f, -0.15f), new Vector3(0.7f, 0.24f, 0.08f), Color.white);
                AddObject(holder, "Cartel_Salida", sign, _exitGreen, Vector3.zero, false);
            }
        }

        // --- Datos, luz y consola -------------------------------------------------------

        static BuildingLayout WriteLayout(List<RoomSpec> rooms, List<DoorSpec> doors)
        {
            var layout = AssetDatabase.LoadAssetAtPath<BuildingLayout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<BuildingLayout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
            }

            layout.rooms = rooms.Select(r => new BuildingLayout.RoomDef
            {
                id = r.Id,
                label = r.Label,
                spoken = SpokenRoomNames[r.Id],
                area = r.Area,
                lightsHackable = r.Hackable,
                hidingSpots = HidingSpots.TryGetValue(r.Id, out var spots) ? spots.ToList() : new List<Vector3>(),
            }).ToList();

            layout.doors = doors.Select(d => new BuildingLayout.DoorDef
            {
                id = d.Id,
                position = d.Position,
                alongX = d.AlongX,
                kind = d.Kind,
                startsOpen = d.StartsOpen,
                roomA = d.RoomA,
                roomB = d.RoomB,
            }).ToList();

            layout.cameras = new List<BuildingLayout.CameraDef>
            {
                CameraFor(rooms, "calle", 0),
                CameraFor(rooms, "muelle", 0),
                CameraFor(rooms, "pasillo", 1, Rect.MinMaxRect(5f, 16.15f, 13f, 19.65f)),
                CameraFor(rooms, "seguridad", 0),
                CameraFor(rooms, "galeria_este", 0, null, true),
                CameraFor(rooms, "vestibulo", 0),
                CameraFor(rooms, "galeria_oeste", 0),
                CameraFor(rooms, "antesala", 0, null, true),
                CameraFor(rooms, "camara", 1),
            };
            layout.cameras[0].watchedByOperator = false;
            EditorUtility.SetDirty(layout);
            return layout;
        }

        static BuildingLayout.CameraDef CameraFor(List<RoomSpec> rooms, string roomId, int profile, Rect? partialView = null, bool infrared = false)
        {
            var room = rooms.First(r => r.Id == roomId);
            var area = room.Area;
            var view = partialView ?? Rect.MinMaxRect(area.xMin - ViewMargin, area.yMin - ViewMargin, area.xMax + ViewMargin, area.yMax + ViewMargin);
            return new BuildingLayout.CameraDef
            {
                label = room.Label,
                roomId = roomId,
                view = view,
                focus = new Vector3(view.center.x, 0.9f, view.center.y),
                profile = profile,
                infrared = infrared,
            };
        }

        static RoomLighting CreateLighting(List<RoomSpec> rooms)
        {
            var lightObject = new GameObject("Luz_Sombras");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 1f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            light.transform.rotation = Quaternion.LookRotation(new Vector3(0.55f, -1f, -0.4f).normalized);

            var lighting = new GameObject("RoomLighting").AddComponent<RoomLighting>();
            foreach (var room in rooms)
            {
                var area = room.Area;
                lighting.rooms.Add(new RoomLighting.Room
                {
                    id = room.Id,
                    area = Rect.MinMaxRect(area.xMin - RoomMargin, area.yMin - RoomMargin, area.xMax + RoomMargin, area.yMax + RoomMargin),
                    brightness = room.Brightness,
                });

                var columns = Mathf.Max(1, Mathf.CeilToInt(area.width / 5f));
                var rows = Mathf.Max(1, Mathf.CeilToInt(area.height / 5f));
                for (var column = 0; column < columns; column++)
                for (var row = 0; row < rows; row++)
                {
                    lighting.lamps.Add(new RoomLighting.Lamp
                    {
                        position = new Vector3(area.xMin + (column + 0.5f) * area.width / columns, 3f,
                            area.yMin + (row + 0.5f) * area.height / rows),
                        radius = 3.4f,
                        power = 0.34f,
                        roomId = room.Id,
                    });
                }
            }

            // El cartel de salida sigue dando un poco de luz con la sala a oscuras
            lighting.lamps.Add(new RoomLighting.Lamp
            {
                position = new Vector3(8.5f, 2.5f, 8.9f),
                radius = 1.3f,
                power = 0.12f,
                roomId = "galeria_este",
                emergency = true,
            });
            return lighting;
        }

        static FeedView CreateFeed(out ConsoleView console)
        {
            var cameraObject = new GameObject("Feed_Camera");
            cameraObject.AddComponent<AudioListener>();
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = FeedHeight * 0.5f / PixelsPerMeter;
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.009f, 0.012f, 1f);
            camera.allowHDR = false;
            camera.allowMSAA = false;

            var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.requiresDepthTexture = true;
            cameraData.renderPostProcessing = false;
            cameraData.antialiasing = AntialiasingMode.None;

            var canvasObject = new GameObject("Consola", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var backdrop = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
            backdrop.transform.SetParent(canvasObject.transform, false);
            backdrop.GetComponent<Image>().color = MeshBuilder.Hex("#050608");
            var backdropRect = backdrop.GetComponent<RectTransform>();
            backdropRect.anchorMin = Vector2.zero;
            backdropRect.anchorMax = Vector2.one;
            backdropRect.offsetMin = Vector2.zero;
            backdropRect.offsetMax = Vector2.zero;

            var surfaceObject = new GameObject("Superficie", typeof(RectTransform), typeof(RawImage));
            surfaceObject.transform.SetParent(canvasObject.transform, false);
            var surface = surfaceObject.GetComponent<RawImage>();
            surface.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            surface.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            surface.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            surface.rectTransform.sizeDelta = new Vector2(ConsoleView.Width * 2f, ConsoleView.Height * 2f);

            var imageObject = new GameObject("Imagen_Camara", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(surfaceObject.transform, false);
            var image = imageObject.GetComponent<RawImage>();

            var compositePath = $"{GeneratedFolder}/M_CctvComposite.mat";
            var composite = AssetDatabase.LoadAssetAtPath<Material>(compositePath);
            if (composite == null)
            {
                composite = new Material(Shader.Find("EyeSky/CctvComposite"));
                AssetDatabase.CreateAsset(composite, compositePath);
            }

            var feed = new GameObject("FeedView").AddComponent<FeedView>();
            feed.feedCamera = camera;
            feed.output = image;
            feed.compositeSource = composite;
            feed.profiles = new[]
            {
                Profile("Feed_Color", 0.13f, 0.07f, 0.003f, 1f, 0.14f, Color.white, 0.26f, 0.45f, false),
                Profile("Feed_BN", 0.2f, 0.16f, 0.004f, 0.6f, 1f, new Color(0.95f, 1f, 0.97f), 0.42f, 0.7f, false),
                Profile("Feed_IR", 0.22f, 0.32f, 0.006f, 0f, 1f, new Color(0.62f, 1f, 0.7f), 0.5f, 0.6f, true),
            };

            console = surfaceObject.AddComponent<ConsoleView>();
            console.surfaceImage = surface;
            return feed;
        }
    }
}
