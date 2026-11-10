using System;
using EyeSky.Art;
using UnityEngine;

namespace EyeSky.EditorTools
{
    /// <summary>Lo que hay en cada sala, en coordenadas del mundo.</summary>
    public static partial class BuildingBuilder
    {
        static Transform Prop(Transform root, string objectName, Action<MeshBuilder> draw, bool castsShadow = true)
        {
            var builder = new MeshBuilder();
            draw(builder);
            return AddObject(root, objectName, builder, _prop, Vector3.zero, castsShadow);
        }

        static Transform Glow(Transform root, string objectName, Material material, Action<MeshBuilder> draw)
        {
            var builder = new MeshBuilder();
            draw(builder);
            return AddObject(root, objectName, builder, material, Vector3.zero, false);
        }

        static void Crate(MeshBuilder b, float x, float z, Vector3 size, float baseY = 0f)
        {
            var body = MeshBuilder.Hex("#a9834f");
            var strap = MeshBuilder.Hex("#7a5b34");
            var y = baseY + size.y * 0.5f;
            b.Box(new Vector3(x, y, z), size, body, 0.78f);
            b.Box(new Vector3(x, y, z), new Vector3(size.x + 0.02f, size.y * 0.16f, size.z + 0.02f), strap);
            b.Box(new Vector3(x, y, z), new Vector3(size.x * 0.14f, size.y + 0.02f, size.z + 0.02f), strap);
        }

        static void Desk(MeshBuilder b, Vector3 center, Vector3 size, Color top, Color side)
        {
            b.Box(new Vector3(center.x, size.y - 0.03f, center.z), new Vector3(size.x, 0.06f, size.z), top);
            b.Box(new Vector3(center.x, (size.y - 0.06f) * 0.5f, center.z), new Vector3(size.x - 0.1f, size.y - 0.06f, size.z - 0.14f), side, 0.76f);
        }

        static void Chair(MeshBuilder b, Vector3 position, bool backTowardX)
        {
            var dark = MeshBuilder.Hex("#26282c");
            b.Cylinder(position, 0.26f, 0.05f, 0.1f, dark, 8);
            b.Cylinder(position + Vector3.up * 0.1f, 0.04f, 0.04f, 0.32f, Steel, 6);
            b.Box(position + Vector3.up * 0.46f, new Vector3(0.46f, 0.08f, 0.46f), dark);
            var back = backTowardX ? new Vector3(0.22f, 0.76f, 0f) : new Vector3(0f, 0.76f, 0.22f);
            var size = backTowardX ? new Vector3(0.07f, 0.56f, 0.44f) : new Vector3(0.44f, 0.56f, 0.07f);
            b.Box(position + back, size, dark);
        }

        static void DressStreet(Transform root)
        {
            Prop(root, "Acera", b =>
            {
                b.Box(new Vector3(-9.6f, 0.06f, 17f), new Vector3(2.8f, 0.12f, 10f), MeshBuilder.Hex("#a3a39e"), 0.7f);
                for (var z = 12.8f; z < 22f; z += 2.6f)
                    b.Box(new Vector3(-16.5f, 0.01f, z), new Vector3(0.14f, 0.02f, 1.3f), MeshBuilder.Hex("#e0d27a"));
            }, false);

            Prop(root, "Camioneta", b =>
            {
                var body = MeshBuilder.Hex("#6f8796");
                var dark = MeshBuilder.Hex("#1c2126");
                foreach (var z in new[] { 14.2f, 18f })
                foreach (var x in new[] { -13.9f, -12.1f })
                    b.Box(new Vector3(x, 0.32f, z), new Vector3(0.28f, 0.64f, 0.7f), dark);
                b.Box(new Vector3(-13f, 1.3f, 16.5f), new Vector3(2f, 1.6f, 3.4f), body, 0.82f);
                b.Box(new Vector3(-13f, 1.02f, 14.1f), new Vector3(2f, 1.05f, 1.5f), body, 0.82f);
                b.Box(new Vector3(-13f, 1.75f, 14.3f), new Vector3(1.9f, 0.5f, 1.1f), MeshBuilder.Hex("#8fb0c0"));
                b.Box(new Vector3(-12f, 1.45f, 16.5f), new Vector3(0.04f, 0.22f, 2.6f), MeshBuilder.Hex("#c9a85c"));
                b.Box(new Vector3(-13f, 2.16f, 17f), new Vector3(1.3f, 0.12f, 1.1f), dark);
            });

            var carRoot = new GameObject("Coche_Policia").transform;
            carRoot.SetParent(root, false);
            Prop(carRoot, "Carroceria", b =>
            {
                var white = MeshBuilder.Hex("#d9dce0");
                var dark = MeshBuilder.Hex("#1a2a4a");
                foreach (var z in new[] { 18.3f, 20.9f })
                foreach (var x in new[] { -17.6f, -15.8f })
                    b.Box(new Vector3(x, 0.32f, z), new Vector3(0.26f, 0.64f, 0.66f), MeshBuilder.Hex("#141416"));
                b.Box(new Vector3(-16.7f, 0.72f, 19.6f), new Vector3(1.9f, 0.7f, 4.2f), white, 0.8f);
                b.Box(new Vector3(-16.7f, 0.74f, 19.6f), new Vector3(1.94f, 0.3f, 1.4f), dark);
                b.Box(new Vector3(-16.7f, 1.32f, 19.8f), new Vector3(1.7f, 0.5f, 2f), MeshBuilder.Hex("#8fb0c0"));
                b.Box(new Vector3(-16.7f, 1.6f, 19.8f), new Vector3(1.72f, 0.08f, 2f), white);
            });
            _beaconRed = Glow(carRoot, "Sirena_Roja", _beaconRedMaterial, b => b.Box(new Vector3(-17.1f, 1.74f, 19.8f), new Vector3(0.7f, 0.2f, 0.34f), Color.white)).gameObject;
            _beaconBlue = Glow(carRoot, "Sirena_Azul", _beaconBlueMaterial, b => b.Box(new Vector3(-16.3f, 1.74f, 19.8f), new Vector3(0.7f, 0.2f, 0.34f), Color.white)).gameObject;
            _policeCar = carRoot.gameObject;
            _policeCar.SetActive(false);
            Prop(root, "Farola", b =>
            {
                b.Cylinder(new Vector3(-10f, 0.12f, 21.5f), 0.1f, 0.07f, 3.3f, MeshBuilder.Hex("#2a2d31"), 8);
                b.Box(new Vector3(-10.4f, 3.42f, 21.5f), new Vector3(0.9f, 0.1f, 0.2f), MeshBuilder.Hex("#2a2d31"));
            });
            Glow(root, "Farola_Luz", _warmBulb, b => b.Box(new Vector3(-10.7f, 3.34f, 21.5f), new Vector3(0.3f, 0.06f, 0.2f), Color.white));
        }

        static void DressDock(Transform root)
        {
            Prop(root, "Cajas", b =>
            {
                Crate(b, -6.8f, 19.8f, new Vector3(1.1f, 1f, 1.1f));
                Crate(b, -5.5f, 19.9f, new Vector3(0.9f, 0.8f, 0.9f));
                Crate(b, -6.8f, 19.8f, new Vector3(0.8f, 0.7f, 0.8f), 1f);
                Crate(b, -1.5f, 14.2f, new Vector3(1.2f, 1.1f, 1.2f));
                Crate(b, -3f, 14.1f, new Vector3(1f, 0.7f, 1f));
            });

            Prop(root, "Palet_Y_Carrito", b =>
            {
                b.Box(new Vector3(-3.6f, 0.07f, 19.8f), new Vector3(1.5f, 0.14f, 1.1f), MeshBuilder.Hex("#8c6a3f"), 0.7f);
                b.Box(new Vector3(-3.6f, 0.5f, 19.8f), new Vector3(1.2f, 0.72f, 0.9f), MeshBuilder.Hex("#c9c4b4"), 0.8f);
                b.Box(new Vector3(-1.2f, 0.05f, 19.9f), new Vector3(0.6f, 0.1f, 0.5f), MeshBuilder.Hex("#d09a2c"));
                b.Box(new Vector3(-1.2f, 0.7f, 20.16f), new Vector3(0.6f, 1.3f, 0.06f), MeshBuilder.Hex("#d09a2c"));
            });

            Prop(root, "Linea_Peligro", b =>
            {
                for (var z = 13.4f; z < 20.8f; z += 0.8f)
                    b.Box(new Vector3(-6.6f, 0.005f, z), new Vector3(0.22f, 0.01f, 0.42f), MeshBuilder.Hex("#d9b43f"));
            }, false);
        }

        static void DressCorridor(Transform root)
        {
            Prop(root, "Taquillas", b =>
            {
                for (var x = 5.5f; x < 8.4f; x += 0.62f)
                {
                    b.Box(new Vector3(x, 0.95f, 19.1f), new Vector3(0.58f, 1.9f, 0.5f), MeshBuilder.Hex("#52707c"), 0.8f);
                    b.Box(new Vector3(x, 1.6f, 18.84f), new Vector3(0.3f, 0.06f, 0.02f), MeshBuilder.Hex("#2f424a"));
                }
                b.Box(new Vector3(12f, 2.46f, 19.3f), new Vector3(16f, 0.1f, 0.1f), MeshBuilder.Hex("#a0683f"));
            });

            Prop(root, "Limpieza", b =>
            {
                b.Cylinder(new Vector3(11f, 0f, 19f), 0.22f, 0.27f, 0.36f, MeshBuilder.Hex("#d3b33a"), 10);
                b.Box(new Vector3(11.2f, 0.75f, 19.1f), new Vector3(0.05f, 1.5f, 0.05f), DarkWood);
                b.Cylinder(new Vector3(12.8f, 0f, 19f), 0.2f, 0.17f, 1.1f, MeshBuilder.Hex("#cfd8dc"), 10);
                b.Cylinder(new Vector3(12.8f, 1.1f, 19f), 0.16f, 0.15f, 0.34f, MeshBuilder.Hex("#7fc0e0"), 10);
            });
        }

        static void DressElectrical(Transform root)
        {
            Prop(root, "Armarios", b =>
            {
                for (var x = 17f; x < 20.8f; x += 0.95f)
                {
                    b.Box(new Vector3(x, 1f, 20.65f), new Vector3(0.86f, 2f, 0.6f), MeshBuilder.Hex("#7a8287"), 0.78f);
                    b.Box(new Vector3(x, 1.2f, 20.33f), new Vector3(0.6f, 0.8f, 0.03f), MeshBuilder.Hex("#3b4348"));
                }
                b.Box(new Vector3(19.8f, 0.6f, 17.6f), new Vector3(1.5f, 1.2f, 1f), MeshBuilder.Hex("#c28a2c"), 0.75f);
                b.Box(new Vector3(19.8f, 1.3f, 17.6f), new Vector3(0.6f, 0.2f, 0.4f), MeshBuilder.Hex("#3b4348"));
            });

            Glow(root, "Testigos", _ledRed, b =>
            {
                for (var x = 17f; x < 20.8f; x += 0.95f)
                    b.Box(new Vector3(x - 0.15f, 1.72f, 20.31f), new Vector3(0.07f, 0.07f, 0.02f), Color.white);
            });
        }

        static void DressSecurity(Transform root)
        {
            Prop(root, "Mesa_Monitores", b =>
            {
                Desk(b, new Vector3(4.3f, 0f, 12.6f), new Vector3(1f, 0.78f, 4f), MeshBuilder.Hex("#5a6066"), MeshBuilder.Hex("#3c4147"));
                for (var z = 11.1f; z < 14.4f; z += 1f)
                    b.Box(new Vector3(4.5f, 1.14f, z), new Vector3(0.14f, 0.66f, 0.92f), MeshBuilder.Hex("#1c1f23"));
                Chair(b, new Vector3(3.2f, 0f, 12.6f), false);
                b.Box(new Vector3(0.45f, 0.95f, 12f), new Vector3(0.7f, 1.9f, 1.3f), MeshBuilder.Hex("#4c5a66"), 0.78f);
                b.Cylinder(new Vector3(1.2f, 0f, 15.6f), 0.2f, 0.24f, 0.5f, MeshBuilder.Hex("#3f4246"), 10);
            });

            Glow(root, "Pantallas", _monitorGlow, b =>
            {
                for (var z = 11.1f; z < 14.4f; z += 1f)
                    b.Box(new Vector3(4.42f, 1.14f, z), new Vector3(0.02f, 0.54f, 0.8f), Color.white);
            });
        }

        static void DressLobby(Transform root)
        {
            Prop(root, "Recepcion", b =>
            {
                Desk(b, new Vector3(7f, 0f, 14.2f), new Vector3(2.8f, 1.05f, 0.9f), Marble, MeshBuilder.Hex("#6b4a30"));
                b.Box(new Vector3(7f, 0.55f, 13.72f), new Vector3(2.4f, 0.1f, 0.02f), Brass);
                b.Box(new Vector3(6.2f, 1.2f, 14.3f), new Vector3(0.5f, 0.32f, 0.04f), MeshBuilder.Hex("#1c1f23"));
                Chair(b, new Vector3(7f, 0f, 15.2f), false);
                b.Box(new Vector3(10.7f, 0.25f, 10.2f), new Vector3(1.7f, 0.42f, 0.6f), MeshBuilder.Hex("#3c4f63"), 0.75f);
                b.Box(new Vector3(10.7f, 0.65f, 10.46f), new Vector3(1.7f, 0.5f, 0.1f), MeshBuilder.Hex("#3c4f63"));
                b.Box(new Vector3(9.8f, 0.01f, 12.7f), new Vector3(4f, 0.02f, 2.2f), Oxblood);
            });
            AddPlant(root, new Vector3(5.9f, 0f, 15.6f));
            AddPlant(root, new Vector3(11.4f, 0f, 15.6f));
        }

        static void DressEastGallery(Transform root)
        {
            BuildCarpetAndBench(root);
            BuildExhibits(root);
            BuildWallDressing(root);
        }

        static void DressWestGallery(Transform root)
        {
            AddPedestalBust(root, new Vector3(14f, 0f, 14.7f));
            AddPedestalVase(root, new Vector3(17.4f, 0f, 14.9f));
            AddPedestalVase(root, new Vector3(14f, 0f, 10.7f));
            AddStatue(root, new Vector3(17.5f, 0f, 10.6f));
            AddPainting(root, new Vector3(14f, 1.75f, 16.16f), 180f, 1);
            AddPainting(root, new Vector3(17f, 1.75f, 16.16f), 180f, 2);
            AddPainting(root, new Vector3(18.96f, 1.75f, 10.6f), -90f, 0);
            AddPainting(root, new Vector3(18.96f, 1.75f, 15f), -90f, 1);
            Prop(root, "Banco", b =>
            {
                b.Box(new Vector3(15.6f, 0.2f, 12.7f), new Vector3(0.5f, 0.4f, 1.6f), DarkWood, 0.8f);
                b.Box(new Vector3(15.6f, 0.46f, 12.7f), new Vector3(0.56f, 0.1f, 1.7f), MeshBuilder.Hex("#3c4f63"));
            });
        }

        static void DressOffice(Transform root)
        {
            Prop(root, "Mesa_Director", b =>
            {
                b.Box(new Vector3(22.8f, 0.01f, 12.7f), new Vector3(4.4f, 0.02f, 3.4f), Oxblood);
                Desk(b, new Vector3(23.4f, 0f, 12.7f), new Vector3(1.1f, 0.8f, 2.4f), MeshBuilder.Hex("#5b3a24"), DarkWood);
                Chair(b, new Vector3(24.6f, 0f, 12.7f), true);
                b.Box(new Vector3(23.5f, 0.96f, 12.1f), new Vector3(0.42f, 0.3f, 0.5f), MeshBuilder.Hex("#1c1f23"));
                b.Box(new Vector3(23.4f, 0.84f, 13.5f), new Vector3(0.3f, 0.04f, 0.4f), MeshBuilder.Hex("#e8e2d2"));
            });

            Prop(root, "Estanteria", b =>
            {
                var colors = new[] { "#7e2b30", "#3c4f63", "#6f8f58", "#c9a85c" };
                for (var x = 20f; x < 24f; x += 1.2f)
                {
                    b.Box(new Vector3(x, 1.1f, 15.95f), new Vector3(1.14f, 2.2f, 0.42f), DarkWood, 0.8f);
                    for (var row = 0; row < 3; row++)
                        b.Box(new Vector3(x, 0.5f + row * 0.65f, 15.72f), new Vector3(0.96f, 0.4f, 0.04f), MeshBuilder.Hex(colors[(row + (int)x) % colors.Length]));
                }
            });

            _keycardVisual = Glow(root, "Tarjeta", _exitGreen, b => b.Box(new Vector3(25.55f, 1.13f, 15.6f), new Vector3(0.22f, 0.03f, 0.14f), Color.white)).gameObject;

            Prop(root, "Caja_Fuerte", b =>
            {
                b.Box(new Vector3(25.55f, 0.55f, 15.6f), new Vector3(0.8f, 1.1f, 0.8f), MeshBuilder.Hex("#4a5158"), 0.75f);
                b.Box(new Vector3(25.13f, 0.7f, 15.6f), new Vector3(0.04f, 0.24f, 0.24f), Brass);
            });
            AddPlant(root, new Vector3(25.4f, 0f, 9.9f));
        }

        static void DressAnteroom(Transform root)
        {
            Prop(root, "Emisores", b =>
            {
                foreach (var x in SensorBeamsX)
                {
                    b.Box(new Vector3(x, 0.55f, 3.2f), new Vector3(0.16f, 1.1f, 0.16f), Steel, 0.8f);
                    b.Box(new Vector3(x, 0.55f, 8.8f), new Vector3(0.16f, 1.1f, 0.16f), Steel, 0.8f);
                }
                b.Box(new Vector3(14.6f, 0.2f, 8.4f), new Vector3(1.8f, 0.4f, 0.5f), DarkWood, 0.8f);
                b.Box(new Vector3(14.6f, 0.46f, 8.4f), new Vector3(1.9f, 0.1f, 0.56f), MeshBuilder.Hex("#3c4f63"));
            });

            // Los sensores se apagaran con su hackeo; de momento solo se ven
            var rays = Glow(root, "Rayos", _laser, b =>
            {
                foreach (var x in SensorBeamsX)
                foreach (var y in new[] { 0.35f, 0.75f })
                    b.Box(new Vector3(x, y, 6f), new Vector3(0.03f, 0.03f, 5.5f), Color.white);
            });
            IgnoreForNavigation(rays);
            _sensorBeams = rays.gameObject;
            AddPlant(root, new Vector3(13f, 0f, 3.6f));
        }

        static void DressVault(Transform root)
        {
            Prop(root, "Cajetines", b =>
            {
                var face = MeshBuilder.Hex("#8a939a");
                var plate = MeshBuilder.Hex("#c9a85c");
                for (var x = 24.6f; x < 29.8f; x += 0.52f)
                for (var y = 0.3f; y < 2.5f; y += 0.42f)
                {
                    b.Box(new Vector3(x, y, 8.88f), new Vector3(0.48f, 0.38f, 0.22f), face);
                    b.Box(new Vector3(x, y, 8.76f), new Vector3(0.12f, 0.06f, 0.02f), plate);
                }
                for (var z = 3.4f; z < 8.6f; z += 0.52f)
                for (var y = 0.3f; y < 2.5f; y += 0.42f)
                {
                    b.Box(new Vector3(29.88f, y, z), new Vector3(0.22f, 0.38f, 0.48f), face);
                    b.Box(new Vector3(29.76f, y, z), new Vector3(0.02f, 0.06f, 0.12f), plate);
                }
            });

            Prop(root, "Botin", b =>
            {
                b.Box(new Vector3(27.1f, 0.45f, 6f), new Vector3(1.4f, 0.9f, 1f), MeshBuilder.Hex("#3f464c"), 0.75f);
            });
            _lootVisual = Prop(root, "Lingotes", b =>
            {
                var gold = MeshBuilder.Hex("#e8c04f");
                for (var x = 26.7f; x < 27.6f; x += 0.4f)
                {
                    b.Box(new Vector3(x, 0.98f, 5.75f), new Vector3(0.34f, 0.14f, 0.18f), gold);
                    b.Box(new Vector3(x, 0.98f, 6.2f), new Vector3(0.34f, 0.14f, 0.18f), gold);
                }
                b.Box(new Vector3(26.9f, 1.12f, 5.97f), new Vector3(0.34f, 0.14f, 0.18f), gold);
                b.Box(new Vector3(27.3f, 1.12f, 5.97f), new Vector3(0.34f, 0.14f, 0.18f), gold);
            }).gameObject;
        }
    }
}
