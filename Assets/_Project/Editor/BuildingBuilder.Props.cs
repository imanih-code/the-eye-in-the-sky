using System.Collections.Generic;
using EyeSky.Art;
using EyeSky.Feed;
using EyeSky.Npc;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EyeSky.EditorTools
{
    /// <summary>
    /// Atrezo y personas del edificio. La estructura esta en BuildingBuilder.cs.
    /// Regenerable: borra sus mallas y materiales y vuelve a crearlos.
    /// </summary>
    public static partial class BuildingBuilder
    {
        const string GeneratedFolder = "Assets/_Project/Generated";
        const string MeshesPath = GeneratedFolder + "/Building_Meshes.asset";
        const float RoomX = 12f;
        const float RoomZ = 9f;
        const float WallHeight = 2.8f;
        const float WallThickness = 0.2f;
        const float PixelsPerMeter = 40f;
        const float FeedHeight = 450f;

        static readonly Color Wood = MeshBuilder.Hex("#6b4a30");
        static readonly Color DarkWood = MeshBuilder.Hex("#3f2b1f");
        static readonly Color Brass = MeshBuilder.Hex("#d9b25f");
        static readonly Color Marble = MeshBuilder.Hex("#e4dfd3");
        static readonly Color Stone = MeshBuilder.Hex("#a7a59c");
        static readonly Color Oxblood = MeshBuilder.Hex("#7e2b30");
        static readonly Color Steel = MeshBuilder.Hex("#58626b");

        static Material _prop;
        static Material _actor;
        static Material _glass;
        static Material _exitGreen;
        static Material _warmBulb;
        static Material _ledRed;
        static Material _monitorGlow;
        static Material _laser;
        static Material _beaconRedMaterial;
        static Material _beaconBlueMaterial;
        static GameObject _policeCar;
        static GameObject _keycardVisual;
        static GameObject _lootVisual;
        static GameObject _beaconRed;
        static GameObject _beaconBlue;
        static readonly List<Mesh> Meshes = new();

        // --- Materiales ----------------------------------------------------------------

        static Material NewSurface(string name, Color color)
        {
            var path = $"{GeneratedFolder}/M_{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("EyeSky/IsoSurface"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(material);
            return material;
        }

        static void CreateMaterials()
        {
            _prop = NewSurface("Prop", Color.white);
            _actor = NewSurface("Actor", Color.white);
            _actor.SetFloat("_Heat", 1f);

            _glass = NewSurface("Glass", new Color(0.62f, 0.84f, 0.92f, 0.28f));
            _glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            _glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            _glass.SetFloat("_ZWrite", 0f);
            _glass.renderQueue = (int)RenderQueue.Transparent;

            _exitGreen = NewSurface("ExitGreen", MeshBuilder.Hex("#0f3d24"));
            _exitGreen.SetColor("_Emission", MeshBuilder.Hex("#39e07f"));
            _warmBulb = NewSurface("WarmBulb", MeshBuilder.Hex("#6b5a3a"));
            _warmBulb.SetColor("_Emission", MeshBuilder.Hex("#ffd98a"));
            _warmBulb.SetFloat("_EmissionNeedsPower", 1f);
            _ledRed = NewSurface("LedRed", MeshBuilder.Hex("#3a0d0d"));
            _ledRed.SetColor("_Emission", MeshBuilder.Hex("#ff3b30"));
            _monitorGlow = NewSurface("MonitorGlow", MeshBuilder.Hex("#16324a"));
            _monitorGlow.SetColor("_Emission", MeshBuilder.Hex("#7ec8ff"));
            _monitorGlow.SetFloat("_EmissionNeedsPower", 1f);
            _laser = NewSurface("Laser", MeshBuilder.Hex("#5a0d0d"));
            _laser.SetColor("_Emission", MeshBuilder.Hex("#ff4436"));
            _beaconRedMaterial = NewSurface("BeaconRed", MeshBuilder.Hex("#5a0d0d"));
            _beaconRedMaterial.SetColor("_Emission", MeshBuilder.Hex("#ff2a22"));
            _beaconBlueMaterial = NewSurface("BeaconBlue", MeshBuilder.Hex("#0d1a5a"));
            _beaconBlueMaterial.SetColor("_Emission", MeshBuilder.Hex("#2a6bff"));
        }

        // --- Sala ----------------------------------------------------------------------

        static void BuildCarpetAndBench(Transform root)
        {
            var carpet = new MeshBuilder();
            carpet.Box(new Vector3(6f, 0.015f, 4.5f), new Vector3(8.2f, 0.03f, 2.8f), Brass);
            carpet.Box(new Vector3(6f, 0.025f, 4.5f), new Vector3(8f, 0.05f, 2.6f), Oxblood);
            carpet.Box(new Vector3(6f, 0.035f, 4.5f), new Vector3(7.5f, 0.07f, 2.1f), MeshBuilder.Hex("#c49a57"));
            carpet.Box(new Vector3(6f, 0.045f, 4.5f), new Vector3(7.4f, 0.09f, 2f), Oxblood);
            AddObject(root, "Alfombra", carpet, _prop, Vector3.zero, false);

            var bench = new MeshBuilder();
            foreach (var x in new[] { -0.75f, 0.75f })
            foreach (var z in new[] { -0.2f, 0.2f })
                bench.Box(new Vector3(x, 0.2f, z), new Vector3(0.08f, 0.4f, 0.08f), DarkWood);
            bench.Box(new Vector3(0f, 0.42f, 0f), new Vector3(1.8f, 0.08f, 0.55f), Wood, 0.8f);
            bench.Box(new Vector3(0f, 0.5f, 0f), new Vector3(1.7f, 0.09f, 0.48f), MeshBuilder.Hex("#3c4f63"));
            AddObject(root, "Banco", bench, _prop, new Vector3(6f, 0.09f, 4.5f), true);
        }

        static void BuildExhibits(Transform root)
        {
            AddPedestalVase(root, new Vector3(2.5f, 0f, 1.6f));
            AddGemCase(root, new Vector3(6f, 0f, 1.5f));
            AddPedestalBust(root, new Vector3(9.6f, 0f, 1.6f));
            AddStatue(root, new Vector3(9.8f, 0f, 7.4f));
            AddPedestalVase(root, new Vector3(6.6f, 0f, 7.5f));
            AddPlant(root, new Vector3(0.7f, 0f, 8.3f));
            AddPlant(root, new Vector3(11.3f, 0f, 0.7f));
        }

        static MeshBuilder Pedestal(float height)
        {
            var builder = new MeshBuilder();
            builder.Box(new Vector3(0f, 0.05f, 0f), new Vector3(0.72f, 0.1f, 0.72f), Stone, 0.75f);
            builder.Box(new Vector3(0f, 0.1f + (height - 0.1f) * 0.5f, 0f), new Vector3(0.58f, height - 0.1f, 0.58f), Marble, 0.72f);
            builder.Box(new Vector3(0f, height + 0.03f, 0f), new Vector3(0.68f, 0.06f, 0.68f), Stone);
            return builder;
        }

        static void AddPedestalVase(Transform root, Vector3 position)
        {
            const float top = 1.06f;
            var blue = MeshBuilder.Hex("#2d6fa3");
            var builder = Pedestal(1f);
            builder.Cylinder(new Vector3(0f, top, 0f), 0.09f, 0.21f, 0.2f, blue);
            builder.Cylinder(new Vector3(0f, top + 0.2f, 0f), 0.21f, 0.15f, 0.2f, MeshBuilder.Hex("#e8d9a8"));
            builder.Cylinder(new Vector3(0f, top + 0.4f, 0f), 0.15f, 0.08f, 0.12f, blue);
            builder.Cylinder(new Vector3(0f, top + 0.52f, 0f), 0.08f, 0.12f, 0.08f, blue);
            AddObject(root, "Vasija", builder, _prop, position, true);
        }

        static void AddPedestalBust(Transform root, Vector3 position)
        {
            const float top = 1.06f;
            var bronze = MeshBuilder.Hex("#9a6a3a");
            var builder = Pedestal(1f);
            builder.Cylinder(new Vector3(0f, top, 0f), 0.2f, 0.14f, 0.08f, Stone);
            builder.Box(new Vector3(0f, top + 0.22f, 0f), new Vector3(0.42f, 0.22f, 0.22f), bronze);
            builder.Box(new Vector3(0f, top + 0.37f, 0f), new Vector3(0.1f, 0.1f, 0.1f), bronze);
            builder.Box(new Vector3(0f, top + 0.53f, 0f), new Vector3(0.22f, 0.26f, 0.24f), bronze);
            builder.Box(new Vector3(0f, top + 0.68f, -0.01f), new Vector3(0.25f, 0.07f, 0.27f), MeshBuilder.Hex("#6f4a28"));
            AddObject(root, "Busto", builder, _prop, position, true);
        }

        static void AddStatue(Transform root, Vector3 position)
        {
            var builder = new MeshBuilder();
            builder.Box(new Vector3(0f, 0.2f, 0f), new Vector3(1.05f, 0.4f, 1.05f), Stone, 0.7f);
            builder.Cylinder(new Vector3(0f, 0.4f, 0f), 0.26f, 0.19f, 0.8f, Marble);
            builder.Box(new Vector3(0f, 1.42f, 0f), new Vector3(0.54f, 0.5f, 0.28f), Marble);
            builder.Box(new Vector3(-0.34f, 1.75f, 0f), new Vector3(0.12f, 0.7f, 0.12f), Marble);
            builder.Box(new Vector3(0.34f, 1.25f, 0f), new Vector3(0.12f, 0.55f, 0.12f), Marble);
            builder.Box(new Vector3(0f, 1.84f, 0f), new Vector3(0.28f, 0.3f, 0.28f), Marble);
            builder.Cylinder(new Vector3(-0.34f, 2.1f, 0f), 0.14f, 0.02f, 0.28f, Brass);
            AddObject(root, "Estatua", builder, _prop, position, true);
        }

        static void AddGemCase(Transform root, Vector3 position)
        {
            var builder = Pedestal(0.95f);
            builder.Box(new Vector3(0f, 1.02f, 0f), new Vector3(0.4f, 0.04f, 0.4f), MeshBuilder.Hex("#2a2438"));
            builder.Cylinder(new Vector3(0f, 1.04f, 0f), 0.1f, 0.18f, 0.08f, MeshBuilder.Hex("#d0314c"));
            builder.Cylinder(new Vector3(0f, 1.12f, 0f), 0.18f, 0.0f, 0.14f, MeshBuilder.Hex("#f0587a"));
            foreach (var x in new[] { -0.32f, 0.32f })
            foreach (var z in new[] { -0.32f, 0.32f })
                builder.Box(new Vector3(x, 1.27f, z), new Vector3(0.03f, 0.56f, 0.03f), Brass);
            builder.Box(new Vector3(0f, 1.56f, 0f), new Vector3(0.68f, 0.03f, 0.68f), Brass);
            AddObject(root, "Vitrina", builder, _prop, position, true);

            var glass = new MeshBuilder().Box(new Vector3(0f, 1.27f, 0f), new Vector3(0.62f, 0.54f, 0.62f), Color.white);
            AddObject(root, "Vitrina_Cristal", glass, _glass, position, false);

            var rope = new MeshBuilder();
            var posts = new[]
            {
                new Vector3(-1.05f, 0f, -0.95f), new Vector3(1.05f, 0f, -0.95f),
                new Vector3(1.05f, 0f, 0.95f), new Vector3(-1.05f, 0f, 0.95f),
            };
            foreach (var post in posts)
            {
                rope.Cylinder(post, 0.14f, 0.14f, 0.04f, Brass, 8);
                rope.Cylinder(post, 0.035f, 0.035f, 0.92f, Brass, 6);
                rope.Cylinder(post + Vector3.up * 0.92f, 0.06f, 0.03f, 0.08f, Brass, 6);
            }
            // Cuerda solo por los lados que dan a la sala: x fija o z fija
            rope.Box(new Vector3(0f, 0.82f, 0.95f), new Vector3(2.1f, 0.05f, 0.05f), Oxblood);
            rope.Box(new Vector3(-1.05f, 0.82f, 0f), new Vector3(0.05f, 0.05f, 1.9f), Oxblood);
            rope.Box(new Vector3(1.05f, 0.82f, 0f), new Vector3(0.05f, 0.05f, 1.9f), Oxblood);
            AddObject(root, "Cordon", rope, _prop, position, true);
        }

        static void AddPlant(Transform root, Vector3 position)
        {
            var builder = new MeshBuilder();
            var leaf = MeshBuilder.Hex("#3f7a4c");
            var leafLight = MeshBuilder.Hex("#5a9c5f");
            builder.Cylinder(Vector3.zero, 0.2f, 0.3f, 0.5f, MeshBuilder.Hex("#b5613a"), 10, 0.75f);
            builder.Cylinder(new Vector3(0f, 0.5f, 0f), 0.05f, 0.04f, 0.5f, DarkWood, 6);
            builder.Cylinder(new Vector3(0f, 0.75f, 0f), 0.5f, 0.05f, 0.6f, leaf, 8);
            builder.Cylinder(new Vector3(0f, 1.1f, 0f), 0.4f, 0.03f, 0.55f, leafLight, 8);
            builder.Cylinder(new Vector3(0f, 1.45f, 0f), 0.26f, 0.0f, 0.45f, leaf, 8);
            AddObject(root, "Planta", builder, _prop, position, true);
        }

        // --- Paredes del fondo ----------------------------------------------------------

        static void BuildWallDressing(Transform root)
        {
            var onZWall = new[] { 1.5f, 6.4f, 10.5f };
            for (var i = 0; i < onZWall.Length; i++)
                AddPainting(root, new Vector3(onZWall[i], 1.75f, RoomZ - 0.04f), 180f, i);

            var onXWall = new[] { 2f, 4.2f, 8f };
            for (var i = 0; i < onXWall.Length; i++)
                AddPainting(root, new Vector3(RoomX - 0.04f, 1.75f, onXWall[i]), -90f, i + 3);

            var sconces = new MeshBuilder();
            var bulbs = new MeshBuilder();
            foreach (var x in new[] { 3.9f, 11.6f })
                AddSconce(sconces, bulbs, Matrix4x4.TRS(new Vector3(x, 2f, RoomZ), Quaternion.Euler(0f, 180f, 0f), Vector3.one));
            foreach (var z in new[] { 3.1f, 7.1f })
                AddSconce(sconces, bulbs, Matrix4x4.TRS(new Vector3(RoomX, 2f, z), Quaternion.Euler(0f, -90f, 0f), Vector3.one));
            AddObject(root, "Apliques", sconces, _prop, Vector3.zero, false);
            AddObject(root, "Apliques_Bombilla", bulbs, _warmBulb, Vector3.zero, false);

            // Cuadro electrico de la sala: objetivo de hackeo mas adelante
            var panel = new MeshBuilder();
            panel.Place(new Vector3(RoomX - 0.02f, 1.3f, 0.75f), -90f);
            panel.Box(Vector3.zero, new Vector3(0.5f, 0.7f, 0.1f), Steel);
            panel.Box(new Vector3(0f, 0.12f, 0.06f), new Vector3(0.32f, 0.18f, 0.02f), MeshBuilder.Hex("#20262b"));
            AddObject(root, "Cuadro_Electrico", panel, _prop, Vector3.zero, false);

            var led = new MeshBuilder();
            led.Place(new Vector3(RoomX - 0.02f, 1.3f, 0.75f), -90f);
            led.Box(new Vector3(-0.15f, -0.2f, 0.06f), new Vector3(0.07f, 0.07f, 0.03f), Color.white);
            AddObject(root, "Cuadro_Led", led, _ledRed, Vector3.zero, false);

            // La propia camara en la esquina, para que se entienda desde donde miramos otras salas
            var securityCamera = new MeshBuilder();
            securityCamera.Place(new Vector3(RoomX - 0.25f, 2.55f, RoomZ - 0.25f), -135f);
            securityCamera.Box(new Vector3(0f, 0f, 0.18f), new Vector3(0.16f, 0.14f, 0.34f), MeshBuilder.Hex("#d6d3c8"));
            securityCamera.Box(new Vector3(0f, 0f, 0.37f), new Vector3(0.11f, 0.1f, 0.05f), MeshBuilder.Hex("#1f2226"));
            AddObject(root, "Camara_Seguridad", securityCamera, _prop, Vector3.zero, false);
        }

        static void AddSconce(MeshBuilder body, MeshBuilder bulb, Matrix4x4 place)
        {
            body.Transform = place;
            bulb.Transform = place;
            body.Box(new Vector3(0f, 0f, 0.03f), new Vector3(0.16f, 0.24f, 0.06f), Brass);
            bulb.Box(new Vector3(0f, 0.08f, 0.13f), new Vector3(0.13f, 0.17f, 0.13f), Color.white);
        }

        static void AddPainting(Transform root, Vector3 position, float yaw, int variant)
        {
            var builder = new MeshBuilder();
            builder.Place(position, yaw);

            const float width = 1.25f;
            const float height = 0.95f;
            builder.Box(Vector3.zero, new Vector3(width + 0.14f, height + 0.14f, 0.05f), Brass);
            builder.Box(new Vector3(0f, 0f, 0.03f), new Vector3(width, height, 0.02f), MeshBuilder.Hex("#efe6cf"));

            var palettes = new[]
            {
                new[] { "#8fb8cf", "#5c7f58", "#3b5a43", "#e8c26a" },
                new[] { "#e0a071", "#a4503e", "#5b2c38", "#f2d78a" },
                new[] { "#3d4f73", "#6a6f9c", "#1f2a44", "#d8dde8" },
            };
            var palette = palettes[variant % palettes.Length];
            var sky = MeshBuilder.Hex(palette[0]);
            var mid = MeshBuilder.Hex(palette[1]);
            var low = MeshBuilder.Hex(palette[2]);
            var accent = MeshBuilder.Hex(palette[3]);
            const float depth = 0.045f;
            const float artW = width - 0.14f;
            const float artH = height - 0.14f;
            builder.Box(new Vector3(0f, 0.12f, depth), new Vector3(artW, artH - 0.24f, 0.01f), sky);
            builder.Box(new Vector3(-artW * 0.18f, -0.1f, depth + 0.005f), new Vector3(artW * 0.64f, artH * 0.34f, 0.01f), mid);
            builder.Box(new Vector3(artW * 0.2f, -0.05f, depth + 0.007f), new Vector3(artW * 0.6f, artH * 0.46f, 0.01f), low);
            builder.Box(new Vector3(0f, -artH * 0.42f, depth + 0.01f), new Vector3(artW, artH * 0.16f, 0.01f), low);
            builder.Box(new Vector3(artW * (variant % 2 == 0 ? 0.3f : -0.28f), artH * 0.28f, depth + 0.01f), new Vector3(0.13f, 0.13f, 0.01f), accent);

            builder.Box(new Vector3(0f, -height * 0.5f - 0.22f, 0.012f), new Vector3(0.22f, 0.12f, 0.024f), Brass);
            AddObject(root, "Cuadro", builder, _prop, Vector3.zero, false);
        }

        // --- Personas ------------------------------------------------------------------

        enum ActorStyle
        {
            Guard,
            Liebre,
            Yunque,
        }

        static GameObject CreateActor(Transform parent, string actorName, ActorStyle style, Vector3 position, float yaw)
        {
            var skin = MeshBuilder.Hex("#d4a27f");
            Color top;
            Color legs;
            Color hat;
            Color accent;
            var width = 1f;

            switch (style)
            {
                case ActorStyle.Guard:
                    top = MeshBuilder.Hex("#33548f");
                    legs = MeshBuilder.Hex("#1c2740");
                    hat = MeshBuilder.Hex("#172036");
                    accent = MeshBuilder.Hex("#f0cf5a");
                    break;
                case ActorStyle.Liebre:
                    top = MeshBuilder.Hex("#25282e");
                    legs = MeshBuilder.Hex("#3a3e46");
                    hat = MeshBuilder.Hex("#1fb5a8");
                    accent = MeshBuilder.Hex("#1fb5a8");
                    break;
                default:
                    top = MeshBuilder.Hex("#9c3a2e");
                    legs = MeshBuilder.Hex("#26262b");
                    hat = MeshBuilder.Hex("#1b1b1f");
                    accent = MeshBuilder.Hex("#e0863a");
                    width = 1.22f;
                    break;
            }

            var root = new GameObject(actorName);
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            visual.rotation = Quaternion.Euler(0f, yaw, 0f);

            var body = new MeshBuilder();
            var headColor = style == ActorStyle.Yunque ? hat : skin;
            body.Box(new Vector3(0f, 0.9f, 0f), new Vector3(0.4f * width, 0.16f, 0.25f), legs);
            body.Box(new Vector3(0f, 1.1f, 0f), new Vector3(0.38f * width, 0.3f, 0.24f), top);
            body.Box(new Vector3(0f, 1.38f, 0f), new Vector3(0.48f * width, 0.3f, 0.27f), top);
            body.Box(new Vector3(0f, 0.99f, 0f), new Vector3(0.41f * width, 0.06f, 0.26f), style == ActorStyle.Guard ? MeshBuilder.Hex("#101522") : accent);
            body.Cylinder(new Vector3(0f, 1.52f, 0f), 0.08f, 0.08f, 0.1f, skin, 8);
            body.Cylinder(new Vector3(0f, 1.6f, 0f), 0.16f, 0.19f, 0.14f, headColor, 10);
            body.Cylinder(new Vector3(0f, 1.74f, 0f), 0.19f, 0.15f, 0.16f, headColor, 10);
            AddActorDetails(body, style, hat, accent, width);
            AddObject(visual, "Cuerpo", body, _actor, Vector3.zero, true);

            var boots = MeshBuilder.Hex("#16161a");
            var legLeft = CreateLimb(visual, "PiernaI", new Vector3(-0.105f * width, 0.86f, 0f), new Vector3(0.18f * width, 0.78f, 0.21f), legs, boots, 0.32f);
            var legRight = CreateLimb(visual, "PiernaD", new Vector3(0.105f * width, 0.86f, 0f), new Vector3(0.18f * width, 0.78f, 0.21f), legs, boots, 0.32f);
            var armLeft = CreateLimb(visual, "BrazoI", new Vector3(-0.3f * width, 1.5f, 0f), new Vector3(0.13f * width, 0.58f, 0.15f), top, skin, 0.15f);
            var armRight = CreateLimb(visual, "BrazoD", new Vector3(0.3f * width, 1.5f, 0f), new Vector3(0.13f * width, 0.58f, 0.15f), top, skin, 0.15f);

            if (style == ActorStyle.Guard)
            {
                var torch = new MeshBuilder().Box(new Vector3(0f, -0.72f, 0.14f), new Vector3(0.07f, 0.07f, 0.26f), MeshBuilder.Hex("#d8d8d0"));
                AddObject(armRight, "Linterna_Modelo", torch, _prop, Vector3.zero, false);

                var beam = new GameObject("Linterna");
                beam.transform.SetParent(visual, false);
                beam.transform.localPosition = new Vector3(0.3f, 0.85f, 0.3f);
                beam.AddComponent<Flashlight>();
            }

            var puppet = root.AddComponent<IsoPuppet>();
            puppet.visual = visual;
            puppet.legLeft = legLeft;
            puppet.legRight = legRight;
            puppet.armLeft = armLeft;
            puppet.armRight = armRight;

            return root;
        }

        static void AddActorDetails(MeshBuilder body, ActorStyle style, Color hat, Color accent, float width)
        {
            var eyes = MeshBuilder.Hex("#1a1412");
            switch (style)
            {
                case ActorStyle.Guard:
                    body.Box(new Vector3(-0.07f, 1.74f, 0.18f), new Vector3(0.05f, 0.05f, 0.02f), eyes);
                    body.Box(new Vector3(0.07f, 1.74f, 0.18f), new Vector3(0.05f, 0.05f, 0.02f), eyes);
                    body.Cylinder(new Vector3(0f, 1.84f, 0f), 0.2f, 0.23f, 0.12f, hat, 10);
                    body.Box(new Vector3(0f, 1.85f, 0.2f), new Vector3(0.28f, 0.04f, 0.16f), hat);
                    body.Box(new Vector3(-0.13f, 1.42f, 0.145f), new Vector3(0.1f, 0.1f, 0.02f), accent);
                    body.Box(new Vector3(0f, 1.3f, 0.14f), new Vector3(0.06f, 0.36f, 0.015f), MeshBuilder.Hex("#d9e0ea"));
                    break;
                case ActorStyle.Liebre:
                    body.Box(new Vector3(-0.07f, 1.74f, 0.18f), new Vector3(0.05f, 0.05f, 0.02f), eyes);
                    body.Box(new Vector3(0.07f, 1.74f, 0.18f), new Vector3(0.05f, 0.05f, 0.02f), eyes);
                    body.Cylinder(new Vector3(0f, 1.8f, 0f), 0.2f, 0.12f, 0.2f, hat, 10);
                    body.Box(new Vector3(0f, 1.22f, -0.22f), new Vector3(0.36f, 0.5f, 0.2f), MeshBuilder.Hex("#6b5a3f"));
                    body.Box(new Vector3(0f, 1.1f, -0.33f), new Vector3(0.26f, 0.18f, 0.04f), MeshBuilder.Hex("#8a7450"));
                    body.Cylinder(new Vector3(0f, 1.5f, 0f), 0.19f, 0.15f, 0.12f, accent, 10);
                    break;
                default:
                    body.Box(new Vector3(0f, 1.74f, 0.17f), new Vector3(0.24f, 0.09f, 0.03f), MeshBuilder.Hex("#d4a27f"));
                    body.Box(new Vector3(-0.07f, 1.74f, 0.19f), new Vector3(0.05f, 0.05f, 0.02f), eyes);
                    body.Box(new Vector3(0.07f, 1.74f, 0.19f), new Vector3(0.05f, 0.05f, 0.02f), eyes);
                    body.Box(new Vector3(0f, 1.4f, 0.145f), new Vector3(0.34f * width, 0.05f, 0.02f), accent);
                    body.Box(new Vector3(0f, 1.3f, 0.145f), new Vector3(0.34f * width, 0.05f, 0.02f), accent);
                    break;
            }
        }

        // Pivote en la articulacion superior: mano o bota en el extremo
        static Transform CreateLimb(Transform parent, string limbName, Vector3 pivot, Vector3 size, Color color, Color tip, float tipLength)
        {
            var limb = new GameObject(limbName).transform;
            limb.SetParent(parent, false);
            limb.localPosition = pivot;

            var builder = new MeshBuilder();
            builder.Box(new Vector3(0f, -size.y * 0.5f, 0f), size, color);
            builder.Box(new Vector3(0f, -size.y - 0.04f, tipLength * 0.2f), new Vector3(size.x * 1.05f, 0.09f, Mathf.Max(size.z, tipLength)), tip);
            AddObject(limb, "Malla", builder, _actor, Vector3.zero, true);
            return limb;
        }

        static FeedProfile Profile(string assetName, float scanline, float noise, float noiseFloor, float chroma,
            float desaturate, Color tint, float vignette, float tracking, bool infrared)
        {
            var path = $"{GeneratedFolder}/{assetName}.asset";
            var profile = AssetDatabase.LoadAssetAtPath<FeedProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FeedProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.scanline = scanline;
            profile.noise = noise;
            profile.noiseFloor = noiseFloor;
            profile.chroma = chroma;
            profile.desaturate = desaturate;
            profile.tint = tint;
            profile.vignette = vignette;
            profile.tracking = tracking;
            profile.infrared = infrared;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // --- Utilidades ----------------------------------------------------------------

        static Transform AddObject(Transform parent, string objectName, MeshBuilder builder, Material material, Vector3 position, bool castsShadow)
        {
            var gameObject = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;

            var mesh = builder.Build($"{parent.name}_{objectName}_{Meshes.Count}");
            Meshes.Add(mesh);
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = gameObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castsShadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return gameObject.transform;
        }

        static void SaveMeshes()
        {
            var holder = ScriptableObject.CreateInstance<MeshHolder>();
            holder.name = "Building_Meshes";
            AssetDatabase.CreateAsset(holder, MeshesPath);
            foreach (var mesh in Meshes)
                AssetDatabase.AddObjectToAsset(mesh, holder);
            AssetDatabase.SaveAssets();
        }
    }
}
