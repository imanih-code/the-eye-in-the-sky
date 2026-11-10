using System;
using System.Collections;
using System.IO;
using System.Linq;
using EyeSky.Core;
using EyeSky.Feed;
using EyeSky.Hacking;
using EyeSky.Npc;
using EyeSky.Security;
using EyeSky.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EyeSky.EditorTools
{
    /// <summary>Genera el edificio, hace capturas de cada camara y comprueba que funciona en Play.</summary>
    public static class BuildingTools
    {
        const string ScenePath = "Assets/_Project/Scenes/Building.unity";
        const int CaptureScale = 2;
        const float TestTimeScale = 4f;

        [MenuItem("Eye In The Sky/Generar edificio")]
        public static void BuildFromMenu()
        {
            ProjectSetup.Run();
            BuildingBuilder.Build(ScenePath);
        }

        /// <summary>Linea de comandos: -captureDir carpeta [-skipBuild]</summary>
        public static void BuildAndCapture()
        {
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureDir");
            if (index < 0 || index + 1 >= args.Length)
                throw new ArgumentException("Falta -captureDir");
            if (Array.IndexOf(args, "-skipBuild") < 0)
                BuildFromMenu();
            Capture(args[index + 1]);
        }

        static void Capture(string directory)
        {
            Directory.CreateDirectory(directory);
            EditorSceneManager.OpenScene(ScenePath);
            ShaderUtil.allowAsyncCompilation = false;

            var lighting = UnityEngine.Object.FindAnyObjectByType<RoomLighting>();
            var feed = UnityEngine.Object.FindAnyObjectByType<FeedView>();
            var network = UnityEngine.Object.FindAnyObjectByType<CameraNetwork>();
            var console = UnityEngine.Object.FindAnyObjectByType<ConsoleView>();
            feed.Prepare();
            console.Prepare();

            // La banda entra al muelle para la prueba de oscuridad
            GameObject.Find("Liebre").transform.position = new Vector3(-4.6f, 0f, 17.4f);
            GameObject.Find("Yunque").transform.position = new Vector3(-3.2f, 0f, 16f);
            foreach (var puppet in UnityEngine.Object.FindObjectsByType<IsoPuppet>(FindObjectsInactive.Exclude))
                puppet.ApplyPose(puppet.transform.position, puppet.visual.eulerAngles.y, 0.7f);

            // La primera imagen tras abrir la escena fuera de Play sale sin materiales listos
            network.Show(1, false);
            lighting.Push();
            feed.feedCamera.Render();

            WriteConsole(Path.Combine(directory, "consola.png"), feed, network, lighting, console, 1);
            console.Redraw();
            WriteSurface(Path.Combine(directory, "titulo.png"), console);

            lighting.SetLit("muelle", false);
            ReportHiddenPeople(feed, network, lighting);
            lighting.SetLit("muelle", true);
        }

        static Color32[] RenderCamera(FeedView feed, CameraNetwork network, RoomLighting lighting, int index)
        {
            network.Show(index, false);
            feed.DrawOsd(47f, true);
            lighting.Push();
            feed.feedCamera.Render();

            var width = feed.resolution.x * CaptureScale;
            var height = feed.resolution.y * CaptureScale;
            var output = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            output.filterMode = FilterMode.Point;
            feed.Composite.SetFloat("_Static", 0f);
            Graphics.Blit(feed.Target, output, feed.Composite);
            var pixels = ReadPixels(output);
            RenderTexture.ReleaseTemporary(output);
            return pixels;
        }

        static void WriteConsole(string path, FeedView feed, CameraNetwork network, RoomLighting lighting, ConsoleView console, int cameraIndex)
        {
            var feedPixels = RenderCamera(feed, network, lighting, cameraIndex);
            console.Redraw();

            var width = ConsoleView.Width * CaptureScale;
            var height = ConsoleView.Height * CaptureScale;
            var result = new Color32[width * height];
            var surface = console.Surface.Pixels;
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
                result[y * width + x] = surface[y / CaptureScale * ConsoleView.Width + x / CaptureScale];

            var feedWidth = feed.resolution.x * CaptureScale;
            var feedHeight = feed.resolution.y * CaptureScale;
            var offsetY = height - feedHeight;
            for (var y = 0; y < feedHeight; y++)
                Array.Copy(feedPixels, y * feedWidth, result, (y + offsetY) * width, feedWidth);

            WritePng(path, result, width, height);
        }

        static void WriteSurface(string path, ConsoleView console)
        {
            WritePng(path, console.Surface.Pixels, ConsoleView.Width, ConsoleView.Height);
        }

        static void ReportHiddenPeople(FeedView feed, CameraNetwork network, RoomLighting lighting)
        {
            network.Show(1, false);
            lighting.Push();
            feed.feedCamera.Render();
            var withPeople = ReadPixels(feed.Target);

            var hidden = new[] { GameObject.Find("Liebre"), GameObject.Find("Yunque") };
            foreach (var person in hidden)
                person.SetActive(false);
            lighting.Push();
            feed.feedCamera.Render();
            var withoutPeople = ReadPixels(feed.Target);
            foreach (var person in hidden)
                person.SetActive(true);

            var different = 0;
            for (var i = 0; i < withPeople.Length; i++)
                if (!withPeople[i].Equals(withoutPeople[i]))
                    different++;
            Debug.Log($"[EYE] Oscuridad en el muelle: {different} pixeles delatan a alguien sin linterna");
        }

        // --- Prueba en Play ----------------------------------------------------------------

        sealed class Until
        {
            public Func<bool> Condition;
            public float Timeout;
            public string Description;
        }

        static Until WaitFor(string description, float timeout, Func<bool> condition)
        {
            return new Until { Description = description, Timeout = timeout, Condition = condition };
        }

        static int _failures;

        static void Check(bool passed, string description)
        {
            Debug.Log($"[EYE] {(passed ? "OK" : "FALLO")}: {description}");
            if (!passed)
                _failures++;
        }

        /// <summary>Linea de comandos: entra en Play y dirige un paso completo del golpe.</summary>
        public static void PlaySmokeTest() => RunInPlay(Scenario);

        /// <summary>Linea de comandos: deja patrullar a todos los guardias y comprueba que recorren el edificio.</summary>
        public static void PatrolSoakTest() => RunInPlay(PatrolSoak);

        /// <summary>Linea de comandos: cada hackeo con exito, con fallo y con bloqueo.</summary>
        public static void PlayHackTest() => RunInPlay(HackScenario);

        static void RunInPlay(Func<IEnumerator> createScenario)
        {
            EditorSceneManager.OpenScene(ScenePath);
            ShaderUtil.allowAsyncCompilation = false;

            // Sin recarga de dominio para que este callback sobreviva a la entrada en Play
            var previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            var previousOptions = EditorSettings.enterPlayModeOptions;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            var errors = 0;
            Application.logMessageReceived += (message, stack, type) =>
            {
                // El indexador de busqueda del editor falla solo en modo por lotes
                if (type is LogType.Error or LogType.Exception && !stack.Contains("UnityEditor.Search"))
                {
                    errors++;
                    Debug.Log($"[EYE] Error en Play: {message}\n{stack}");
                }
            };

            IEnumerator scenario = null;
            var resumeAt = 0f;
            Until waiting = null;
            var waitStarted = 0f;

            EditorApplication.update += () =>
            {
                if (!EditorApplication.isPlaying)
                    return;
                if (scenario == null)
                {
                    UnityEngine.Object.FindAnyObjectByType<EyeSky.Core.HeistDirector>().StartHeist();
                    scenario = Flatten(createScenario());
                }

                if (Time.time < resumeAt)
                    return;
                if (waiting != null)
                {
                    var finished = waiting.Condition();
                    var timedOut = Time.time - waitStarted > waiting.Timeout;
                    if (!finished && !timedOut)
                        return;
                    Check(finished, waiting.Description + $" ({Time.time - waitStarted:0.0} s)");
                    waiting = null;
                }

                if (scenario.MoveNext())
                {
                    if (scenario.Current is float seconds)
                    {
                        resumeAt = Time.time + seconds;
                    }
                    else if (scenario.Current is Until until)
                    {
                        waiting = until;
                        waitStarted = Time.time;
                    }
                    return;
                }

                Check(errors == 0, $"sin errores en Play ({errors})");
                Debug.Log($"[EYE] Resultado: {_failures} fallos");
                Time.timeScale = 1f;
                EditorSettings.enterPlayModeOptionsEnabled = previousEnabled;
                EditorSettings.enterPlayModeOptions = previousOptions;
                EditorApplication.Exit(_failures == 0 ? 0 : 1);
            };
            EditorApplication.isPlaying = true;
        }

        // Permite que un paso de la prueba sea a su vez una secuencia de pasos
        static IEnumerator Flatten(IEnumerator root)
        {
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (!top.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                if (top.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                yield return top.Current;
            }
        }

        static void Only(params GuardAgent[] keep)
        {
            foreach (var guard in GuardAgent.All)
                guard.gameObject.SetActive(keep.Contains(guard));
        }

        static IEnumerator Scenario()
        {
            yield return 0.5f;
            Time.timeScale = TestTimeScale;
            UnityEngine.Object.FindAnyObjectByType<EyeSky.Police.PoliceResponse>().enabled = false;

            var lighting = UnityEngine.Object.FindAnyObjectByType<RoomLighting>();
            var cameras = UnityEngine.Object.FindAnyObjectByType<CameraNetwork>();
            var console = UnityEngine.Object.FindAnyObjectByType<ConsoleView>();
            var doors = UnityEngine.Object.FindAnyObjectByType<DoorNetwork>();
            var roster = UnityEngine.Object.FindAnyObjectByType<GuardRoster>();
            var hacks = UnityEngine.Object.FindAnyObjectByType<HackSession>();
            hacks.AutoSolve = true;

            var liebre = CrewAgent.All.First(c => c.canPick);
            var yunque = CrewAgent.All.First(c => c.canFight);
            var ramos = GuardAgent.All.First(g => g.displayName == "RAMOS");
            var vega = GuardAgent.All.First(g => g.displayName == "VEGA");
            var soto = GuardAgent.All.First(g => g.displayName == "SOTO");
            var watcher = GuardAgent.All.First(g => g.isOperator);
            var eastGallery = new Vector3(6f, 0f, 5.6f);

            HeistEvents.Clear();
            Only(watcher);

            // 1. Entrar: la persiana esta cerrada y no es cosa suya
            console.Click(console.CrewButtonCenter(0));
            console.Click(console.ActionCenter("IR A"));
            console.Click(console.RoomCenter("muelle"));
            Check(liebre.StatusText == "RECIBIDO", "la orden se da con clics en el plano");
            yield return WaitFor("Liebre se para ante la persiana cerrada", 15f, () => liebre.State == CrewState.Blocked);

            console.Click(console.CameraButtonCenter(1));
            console.Click(console.ActionCenter("BUCLE"));
            yield return WaitFor("se resuelve el bucle", 25f, () => !hacks.Active);
            Check(cameras.IsLooped(1), "la camara del muelle entra en bucle");
            console.Click(console.DoorCenter("calle_muelle"));
            yield return WaitFor("se resuelve el panel del muelle", 25f, () => !hacks.Active);
            Check(doors.IsOpen("calle_muelle"), "hackear la persiana la abre");
            yield return WaitFor("Liebre llega al muelle", 15f, () => liebre.State == CrewState.Waiting && liebre.Motor.CurrentRoom == "muelle");
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) == 0, "con la camara en bucle nadie da la alarma");

            // 2. Forzar una cerradura y quedarse en la zona ciega
            liebre.Issue(OrderKind.Go, new Vector3(2f, 0f, 18f));
            yield return WaitFor("Liebre fuerza la cerradura", 12f, () => liebre.State == CrewState.Picking);
            var pickStarted = Time.time;
            yield return WaitFor("la puerta queda abierta", 12f, () => doors.IsOpen("muelle_pasillo"));
            Check(Time.time - pickStarted >= 5.5f, "forzar tarda unos 6 segundos");
            yield return WaitFor("Liebre llega al pasillo", 10f, () => liebre.State == CrewState.Waiting && liebre.Motor.CurrentRoom == "pasillo");

            // 3. Una camara activa delata; el operador puede ser neutralizado a tiempo
            cameras.Loop(1, 0.001f);
            yield return 0.2f;
            HeistEvents.Clear();
            yunque.Motor.Warp(new Vector3(-4f, 0f, 16f));
            var detectStarted = Time.time;
            yield return WaitFor("el operador da la alarma por camara", 10f, () => HeistEvents.Count(HeistEventKind.AlarmConfirmed) >= 1);
            Check(Time.time - detectStarted is >= 4f and <= 6.5f, "tarda unos 2 s en verlo y 3 s en avisar");

            yield return WaitFor("el operador vuelve a verlo", 10f, () => watcher.State == GuardState.Reporting);
            HeistEvents.Clear();
            watcher.Neutralize(false);
            yield return 4f;
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) == 0, "neutralizado en la ventana de aviso, no hay alarma");

            // 4. Vision con luz
            yunque.Motor.Warp(new Vector3(-10.4f, 0f, 18f));
            Only(ramos, watcher);
            yield return 0.1f;
            ramos.StandAt(eastGallery, Vector3.left);
            liebre.Motor.Warp(new Vector3(2.5f, 0f, 5.6f));
            yield return WaitFor("el guardia la ve con luz", 5f, () => ramos.State == GuardState.Reporting);
            HeistEvents.Clear();
            yield return WaitFor("y da el aviso", 6f, () => HeistEvents.Count(HeistEventKind.AlarmConfirmed) >= 1);
            yield return WaitFor("Liebre se esconde sola", 8f, () => liebre.Hidden);

            ramos.StandAt(liebre.transform.position + new Vector3(3f, 0f, 0f), Vector3.left);
            yield return 3f;
            Check(ramos.State == GuardState.Patrol, "escondida no la ve");

            // 5. La oscuridad afecta a la simulacion
            liebre.Issue(OrderKind.Stop);
            yield return 2f;
            liebre.Motor.Warp(new Vector3(2.5f, 0f, 5.6f));
            lighting.SetLit("galeria_este", false);
            ramos.StandAt(new Vector3(8.5f, 0f, 5.6f), Vector3.left);
            HeistEvents.Clear();
            yield return 3f;
            Check(ramos.State == GuardState.Patrol, "a oscuras no la ve a 6 m");
            ramos.StandAt(new Vector3(5f, 0f, 5.6f), Vector3.left);
            yield return WaitFor("dentro de la linterna si la ve", 5f, () => ramos.State == GuardState.Reporting);
            lighting.SetLit("galeria_este", true);
            ramos.StandAt(eastGallery, Vector3.right);
            liebre.Motor.Warp(new Vector3(-10.4f, 0f, 16f));
            liebre.Issue(OrderKind.Stop);
            yield return 2f;

            // 6. Noquear por detras, senalando en la imagen
            yunque.Motor.Warp(new Vector3(4f, 0f, 5.6f));
            HeistEvents.Clear();
            console.Click(console.CameraButtonCenter(4));
            yield return 0.2f;
            console.Click(console.CrewButtonCenter(1));
            console.Click(console.ActionCenter("NOQUEAR"));
            console.Click(console.FeedPixel(ramos.transform.position + Vector3.up));
            Check(yunque.StatusText == "RECIBIDO", "se senala al guardia en la imagen");
            yield return WaitFor("Yunque lo noquea por detras", 10f, () => ramos.IsDown);
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) == 0, "por detras no hay alarma");

            // 7. Otro guardia encuentra el cuerpo
            Only(ramos, soto, watcher);
            yield return 0.1f;
            soto.StandAt(eastGallery + new Vector3(3f, 0f, 0f), Vector3.left);
            yield return WaitFor("se encuentra el cuerpo", 4f, () => HeistEvents.Count(HeistEventKind.BodyFound) >= 1);

            // 8. De frente y con luz falla
            yunque.Motor.Warp(new Vector3(1.5f, 0f, 5.6f));
            soto.StandAt(new Vector3(5f, 0f, 3.2f), Vector3.left);
            yield return 0.1f;
            HeistEvents.Clear();
            yunque.Issue(OrderKind.Knockout, default, soto);
            yield return WaitFor("Yunque lo reduce", 12f, () => soto.IsDown);
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) >= 1, "de frente y con luz da el aviso");

            // 9. De frente a oscuras funciona
            lighting.SetLit("galeria_este", false);
            Only(ramos, soto, vega, watcher);
            yield return 0.1f;
            vega.StandAt(new Vector3(7f, 0f, 3.2f), Vector3.left);
            yunque.Motor.Warp(new Vector3(2.8f, 0f, 3.2f));
            yield return 0.1f;
            HeistEvents.Clear();
            yunque.Issue(OrderKind.Knockout, default, vega);
            yield return WaitFor("Yunque lo reduce a oscuras", 12f, () => vega.IsDown);
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) == 0, "de frente a oscuras no da aviso");
            lighting.SetLit("galeria_este", true);

            // 10. Controles de radio y despertar
            HeistEvents.Clear();
            roster.RunCheckNow();
            Check(HeistEvents.Count(HeistEventKind.MissedCheck) >= 1, "un control sin respuesta se nota");
            ramos.Wake();
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) >= 1, "el que despierta da la alarma");

            // 11. Eliminar se oye
            Only(ramos, watcher);
            yield return 0.1f;
            ramos.StandAt(new Vector3(9f, 0f, 3.2f), Vector3.right);
            yunque.Motor.Warp(new Vector3(4f, 0f, 3.2f));
            yield return 0.1f;
            HeistEvents.Clear();
            yunque.Issue(OrderKind.Kill, default, ramos);
            yield return WaitFor("Yunque dispara", 8f, () => ramos.Dead);
            Check(HeistEvents.Count(HeistEventKind.Gunshot) == 1, "el disparo queda registrado");

            // 12. Un guardia arregla el apagon y cierra lo que abrio
            vega.Wake();
            Only(vega, watcher);
            yield return 0.1f;
            vega.StandAt(new Vector3(14f, 0f, 18f), Vector3.right);
            lighting.SetLit("muelle", false);
            yield return WaitFor("un guardia restaura la luz", 70f, () => lighting.IsLit("muelle"));
            Check(!doors.IsOpen("pasillo_electrico"), "el guardia cierra la puerta que abrio");

            // 13. Retirada
            vega.gameObject.SetActive(false);
            yunque.Motor.Warp(new Vector3(-3f, 0f, 16f));
            yunque.Issue(OrderKind.Retreat);
            yield return WaitFor("Yunque vuelve a la camioneta", 20f, () => yunque.State == CrewState.Escaped);
        }

        static IEnumerator HackScenario()
        {
            yield return 0.5f;
            Time.timeScale = TestTimeScale;
            UnityEngine.Object.FindAnyObjectByType<EyeSky.Police.PoliceResponse>().enabled = false;

            var lighting = UnityEngine.Object.FindAnyObjectByType<RoomLighting>();
            var cameras = UnityEngine.Object.FindAnyObjectByType<CameraNetwork>();
            var console = UnityEngine.Object.FindAnyObjectByType<ConsoleView>();
            var doors = UnityEngine.Object.FindAnyObjectByType<DoorNetwork>();
            var hacks = UnityEngine.Object.FindAnyObjectByType<HackSession>();
            var sensors = UnityEngine.Object.FindAnyObjectByType<SensorGrid>();
            var feed = UnityEngine.Object.FindAnyObjectByType<FeedView>();
            var yunque = CrewAgent.All.First(c => c.canFight);
            var vega = GuardAgent.All.First(g => g.displayName == "VEGA");

            Only();
            yield return 0.1f;
            HeistEvents.Clear();
            hacks.AutoSolve = true;
            Func<bool> finished = () => !hacks.Active;
            UnityEngine.Object.FindAnyObjectByType<EyeSky.Core.HeistObjectives>().GiveKeycardForTests();
            yunque.Motor.Warp(new Vector3(23.7f, 0f, 6f));

            // Cada hackeo con exito
            console.Click(console.CameraButtonCenter(1));
            yield return 0.2f;
            console.Click(console.ActionCenter("BUCLE"));
            Check(hacks.Active && !feed.output.enabled, "durante el hackeo no se ve ninguna camara");
            yield return WaitFor("EMPALME en partida", 25f, finished);
            Check(feed.output.enabled && cameras.IsLooped(1), "vuelve la imagen y la camara queda en bucle");

            console.Click(console.LightRowCenter("vestibulo"));
            yield return WaitFor("PUENTE en partida", 25f, finished);
            Check(!lighting.IsLit("vestibulo"), "el hackeo corta la luz");

            console.Click(console.DoorCenter("calle_muelle"));
            yield return WaitFor("PARCHE facil en partida", 25f, finished);
            Check(doors.IsOpen("calle_muelle"), "se abre la persiana");

            console.Click(console.DoorCenter("este_antesala"));
            yield return WaitFor("puerta electronica", 25f, finished);
            Check(doors.IsOpen("este_antesala"), "se abre la puerta electronica");
            console.Click(console.DoorCenter("este_antesala"));
            yield return WaitFor("bloqueo de puerta", 25f, finished);
            Check(!doors.IsOpen("este_antesala") && doors.IsLockedAgainstGuards("este_antesala"), "abierta, se cierra y bloquea a los guardias");

            console.Click(console.DoorCenter("oeste_oficina"));
            yield return WaitFor("HUELLA en partida", 25f, finished);
            Check(doors.IsOpen("oeste_oficina"), "se abre la puerta biometrica");

            console.Click(console.ActionCenter("SENSORES"));
            yield return WaitFor("CORTAFUEGOS en partida", 90f, () => !hacks.Active);
            Check(!sensors.Armed && !sensors.beams.activeSelf, "se apagan los sensores");

            console.Click(console.DoorCenter("antesala_camara"));
            Check(hacks.StageIndex == 0, "la camara acorazada empieza por la cripta");
            yield return WaitFor("camara acorazada en dos fases", 60f, finished);
            Check(doors.IsOpen("antesala_camara"), "se abre la camara acorazada");
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) == 0, "con el parche bien no hay alarma");

            console.Click(console.ActionCenter("INTERFERIR"));
            yield return WaitFor("INTERFERENCIA en partida", 40f, finished);
            Check(HeistEvents.Count(HeistEventKind.InterferenceSucceeded) == 1, "la central queda suplantada");
            console.Click(console.ActionCenter("INTERFERIR"));
            Check(!hacks.Active, "la central solo se puede suplantar una vez");

            // Fallar agota los intentos y bloquea
            hacks.AutoSolve = false;
            HeistEvents.Clear();
            console.Click(console.CameraButtonCenter(3));
            yield return 0.2f;
            console.Click(console.ActionCenter("BUCLE"));
            yield return WaitFor("sin tocar nada se agotan los intentos", 60f, finished);
            Check(HeistEvents.Count(HeistEventKind.HackFailed) == 3, "cada intento fallido deja rastro");
            Check(HeistEvents.Count(HeistEventKind.HackLockout) == 1 && hacks.LockSecondsLeft(HackSession.CameraId(3)) > 0f, "el sistema se bloquea");
            console.Click(console.ActionCenter("BUCLE"));
            Check(!hacks.Active, "bloqueado no se puede reintentar");
            yield return WaitFor("el bloqueo termina", 70f, () => hacks.LockSecondsLeft(HackSession.CameraId(3)) <= 0f);

            HeistEvents.Clear();
            console.Click(console.ActionCenter("BUCLE"));
            hacks.Abort();
            Check(!hacks.Active && HeistEvents.Count(HeistEventKind.HackFailed) == 1, "abandonar cuenta como fallo");
            Check(hacks.LockSecondsLeft(HackSession.CameraId(3)) <= 0f, "abandonar no bloquea");

            // Camara acorazada: el parche fallido abre igual, pero avisa
            doors.SetOpen("antesala_camara", false);
            HeistEvents.Clear();
            hacks.AutoSolve = true;
            console.Click(console.DoorCenter("antesala_camara"));
            yield return WaitFor("pasa a la segunda fase", 30f, () => hacks.StageIndex == 1);
            hacks.AutoSolve = false;
            yield return WaitFor("el parche se agota", 50f, finished);
            Check(doors.IsOpen("antesala_camara"), "falla el parche y la puerta se abre igual");
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) == 1 && HeistEvents.Count(HeistEventKind.HackLockout) == 0, "salta la alarma, sin bloqueo");

            // Camara acorazada: fallar la cripta si bloquea
            doors.SetOpen("antesala_camara", false);
            HeistEvents.Clear();
            console.Click(console.DoorCenter("antesala_camara"));
            yield return WaitFor("la cripta se agota", 90f, finished);
            Check(!doors.IsOpen("antesala_camara") && HeistEvents.Count(HeistEventKind.HackLockout) == 1, "sin el codigo la puerta sigue cerrada y bloqueada");

            // Sensores
            sensors.DisableFor(30f);
            HeistEvents.Clear();
            yunque.Motor.Warp(new Vector3(23f, 0f, 6f));
            yield return 2f;
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) == 0, "con los sensores apagados se puede cruzar");
            sensors.DisableFor(0.001f);
            yield return WaitFor("los sensores detectan a quien los cruza", 3f, () => HeistEvents.Count(HeistEventKind.AlarmConfirmed) >= 1);
            Check(sensors.beams.activeSelf, "los rayos vuelven a verse");

            // Bloquear una puerta frena a un guardia (sin apagones pendientes: irian a arreglarlos)
            lighting.SetLit("vestibulo", true);
            Only(vega);
            yield return 0.1f;
            vega.StandAt(new Vector3(8.5f, 0f, 17.9f), Vector3.back);
            doors.LockAgainstGuards("pasillo_vestibulo", 6f);
            vega.Investigate(new Vector3(8.5f, 0f, 12f));
            yield return 3f;
            Check(!doors.IsOpen("pasillo_vestibulo") && vega.Motor.CurrentRoom == "pasillo", "la puerta bloqueada lo detiene");
            yield return WaitFor("pasa cuando se acaba el bloqueo", 15f, () => vega.Motor.CurrentRoom == "vestibulo");
        }

        /// <summary>Linea de comandos: el camino entero de la policia, de la calma a perder la conexion.</summary>
        public static void PlayPoliceTest() => RunInPlay(PoliceScenario);

        static IEnumerator PoliceScenario()
        {
            yield return 0.5f;
            Time.timeScale = 8f;

            var radio = UnityEngine.Object.FindAnyObjectByType<EyeSky.Radio.RadioBus>();
            var police = UnityEngine.Object.FindAnyObjectByType<EyeSky.Police.PoliceResponse>();
            var arrival = UnityEngine.Object.FindAnyObjectByType<EyeSky.Police.PoliceArrival>();
            var director = UnityEngine.Object.FindAnyObjectByType<EyeSky.Core.HeistDirector>();
            var console = UnityEngine.Object.FindAnyObjectByType<ConsoleView>();
            var hacks = UnityEngine.Object.FindAnyObjectByType<HackSession>();
            var cameras = UnityEngine.Object.FindAnyObjectByType<CameraNetwork>();
            var feed = UnityEngine.Object.FindAnyObjectByType<FeedView>();
            var siren = UnityEngine.Object.FindAnyObjectByType<EyeSky.Police.SirenAudio>();
            var heard = new System.Collections.Generic.List<EyeSky.Radio.RadioBus.Line>();
            radio.LineStarted += (channel, line) =>
            {
                if (channel == EyeSky.Radio.RadioChannel.Police)
                    heard.Add(line);
            };

            Only();
            yield return 0.1f;

            Check(radio.Tuned == EyeSky.Radio.RadioChannel.Police, "el escaner empieza en la policia");
            console.Click(console.ScannerCenter);
            Check(radio.Tuned == EyeSky.Radio.RadioChannel.Security, "un clic cambia de frecuencia");
            console.Click(console.ScannerCenter);

            yield return WaitFor("en calma ya se oye a la central", 60f, () => heard.Count >= 1);
            Check(heard[0].Clip != null, "la frase tiene voz");
            Check(police.Model.State == EyeSky.Police.PoliceState.Calm, "la charla no cambia nada");

            // Alarma: en camino, y la interferencia les retrasa
            HeistEvents.Raise(HeistEventKind.AlarmConfirmed, Vector3.zero, "prueba");
            Check(police.Model.State == EyeSky.Police.PoliceState.EnRoute, "la alarma los pone en camino");
            var before = police.Model.SecondsToArrival;
            hacks.AutoSolve = true;
            console.Click(console.ActionCenter("INTERFERIR"));
            yield return WaitFor("se resuelve la interferencia", 40f, () => !hacks.Active);
            Check(police.Model.SecondsToArrival > before, "suplantar a la central les retrasa");

            yield return WaitFor("casi han llegado", 280f, () => police.Model.SecondsToArrival <= 12f);
            Check(police.Model.SilentApproach || siren.IsAudible, "en los ultimos segundos hay sirena, o se pidio silencio");
            if (police.Model.SilentApproach)
                Check(!siren.IsAudible, "si van sin sirenas, no se oyen");

            yield return WaitFor("llega la policia", 20f, () => police.Model.State == EyeSky.Police.PoliceState.Arrived);
            yield return 0.5f;
            Check(arrival.car.activeSelf, "el coche aparece en la calle");

            yield return WaitFor("se cortan todas las camaras", 30f, () => director.Finished);
            yield return 0.2f;
            var allCut = true;
            for (var i = 0; i < cameras.layout.cameras.Count; i++)
                allCut &= cameras.IsCut(i);
            Check(allCut && !feed.output.enabled, "no queda ninguna camara");

            var texts = string.Join(" | ", heard.Select(l => l.Text));
            string[] route = { "Robo en curso", "avenida del Puerto", "puente Mayor", "plaza del Reloj", "entrando en la calle Ferrer", "Estamos en la puerta" };
            var order = route.Select(r => texts.IndexOf(r, StringComparison.OrdinalIgnoreCase)).ToArray();
            Check(order.All(i => i >= 0), $"se oyen todas las etapas ({string.Join(",", order)})");
            Check(order.Zip(order.Skip(1), (a, b) => a < b).All(ok => ok), "las etapas suenan en orden");
            Check(texts.Contains("cancelen"), "se oye la falsa alarma");
            Check(heard.All(l => l.Clip != null), "todas las frases de la policia tienen voz");

            Check(director.Outcome == EyeSky.Core.HeistOutcome.Caught, "llegar con la banda dentro es derrota");
            Check(director.Report.Headline.Contains("ALARMA"), $"el informe dice que te delato: {director.Report.Headline}");
            Check(director.Report.Timeline.Any(t => t.Important), "el informe muestra la linea de tiempo");
            Debug.Log($"[EYE] Informe: {director.Report.Stats}");

            yield return 0.2f;
            console.Redraw();
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureDir");
            if (index >= 0)
            {
                Directory.CreateDirectory(args[index + 1]);
                WriteSurface(Path.Combine(args[index + 1], "informe_derrota.png"), console);
            }
        }

        /// <summary>Linea de comandos: el golpe entero, de la calle a la camioneta con el botin.</summary>
        public static void PlayFullHeistTest() => RunInPlay(FullHeist);

        /// <summary>Linea de comandos: la banda se va sin el botin.</summary>
        public static void PlayWalkAwayTest() => RunInPlay(WalkAway);

        static IEnumerator FullHeist()
        {
            yield return 0.5f;
            Time.timeScale = 6f;

            var console = UnityEngine.Object.FindAnyObjectByType<ConsoleView>();
            var doors = UnityEngine.Object.FindAnyObjectByType<DoorNetwork>();
            var hacks = UnityEngine.Object.FindAnyObjectByType<HackSession>();
            var sensors = UnityEngine.Object.FindAnyObjectByType<SensorGrid>();
            var director = UnityEngine.Object.FindAnyObjectByType<EyeSky.Core.HeistDirector>();
            var objectives = UnityEngine.Object.FindAnyObjectByType<EyeSky.Core.HeistObjectives>();
            var liebre = CrewAgent.All.First(c => c.canPick);
            var yunque = CrewAgent.All.First(c => c.canFight);
            Func<bool> finished = () => !hacks.Active;

            Only();
            yield return 0.1f;
            hacks.AutoSolve = true;

            IEnumerator Hack(string doorId, string description)
            {
                console.Click(console.DoorCenter(doorId));
                yield return WaitFor(description, 60f, finished);
                Check(doors.IsOpen(doorId), $"{description}: abierta");
            }

            IEnumerator Walk(Vector3 point, string room)
            {
                liebre.Issue(OrderKind.Go, point);
                yield return 2f;
                yield return WaitFor($"Liebre llega a {room}", 90f,
                    () => liebre.Motor.CurrentRoom == room && liebre.State is CrewState.Waiting or CrewState.Collecting);
            }

            yield return Hack("calle_muelle", "persiana");
            yield return Walk(new Vector3(2f, 0f, 18f), "pasillo");
            yield return Hack("pasillo_vestibulo", "puerta del vestibulo");
            yield return Walk(new Vector3(8.5f, 0f, 11.5f), "vestibulo");

            console.Click(console.DoorCenter("antesala_camara"));
            Check(!hacks.Active, "sin nadie en la puerta la camara acorazada no se puede atacar");

            yield return Hack("oeste_oficina", "puerta del director");
            yield return Walk(new Vector3(22.6f, 0f, 11f), "oficina");
            yield return WaitFor("Liebre coge la tarjeta sola", 15f, () => objectives.HasKeycard);
            Check(!objectives.keycardVisual.activeSelf, "la tarjeta desaparece de la caja fuerte");

            yield return Walk(new Vector3(21f, 0f, 7.5f), "antesala");
            console.Click(console.ActionCenter("SENSORES"));
            yield return WaitFor("sensores", 90f, finished);
            Check(!sensors.Armed, "sensores apagados");

            HeistEvents.Clear();
            yield return Walk(new Vector3(23.7f, 0f, 6f), "antesala");
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) == 0, "cruza con los sensores apagados");
            yield return Hack("antesala_camara", "camara acorazada");

            yield return Walk(new Vector3(26f, 0f, 7.5f), "camara");
            yield return WaitFor("Liebre coge el botin sola", 15f, () => objectives.LootTaken);
            Check(objectives.LootCarrier == liebre && liebre.carriedBag.activeSelf && !objectives.lootVisual.activeSelf, "el botin pasa a su espalda");

            yunque.Issue(OrderKind.Retreat);
            yield return WaitFor("Yunque ya esta en la camioneta", 15f, () => yunque.State == CrewState.Escaped);
            Check(!director.Finished, "falta Liebre: el golpe sigue");

            liebre.Issue(OrderKind.Retreat);
            yield return WaitFor("Liebre vuelve con el botin", 120f, () => director.Finished);
            Check(director.Outcome == EyeSky.Core.HeistOutcome.Won, "es una victoria");
            Check(director.Report.Title == "GOLPE COMPLETADO", "el informe lo dice");
            Check(director.Report.Headline.Contains("NADIE"), "nadie llamo a la policia");

            yield return 0.2f;
            console.Redraw();
            var args = Environment.GetCommandLineArgs();
            var index = Array.IndexOf(args, "-captureDir");
            if (index >= 0)
            {
                Directory.CreateDirectory(args[index + 1]);
                WriteSurface(Path.Combine(args[index + 1], "informe_victoria.png"), console);
            }
        }

        static IEnumerator WalkAway()
        {
            yield return 0.5f;
            Time.timeScale = 6f;
            var director = UnityEngine.Object.FindAnyObjectByType<EyeSky.Core.HeistDirector>();
            Only();
            yield return 0.1f;
            foreach (var crew in CrewAgent.All)
                crew.Issue(OrderKind.Retreat);
            yield return WaitFor("la banda se va sin el botin", 20f, () => director.Finished);
            Check(director.Outcome == EyeSky.Core.HeistOutcome.WalkedAway, "cuenta como golpe fallido");
            Check(director.Report.Title == "SALISTEIS SIN EL BOTIN", "el informe lo dice");
        }

        static IEnumerator PatrolSoak()
        {
            yield return 0.5f;
            Time.timeScale = 6f;
            UnityEngine.Object.FindAnyObjectByType<EyeSky.Police.PoliceResponse>().enabled = false;
            HeistEvents.Clear();

            var patrols = GuardAgent.All.Where(g => !g.isOperator).ToList();
            var visited = patrols.ToDictionary(g => g, _ => new System.Collections.Generic.HashSet<string>());
            const int SampleSeconds = 150;
            for (var i = 0; i < SampleSeconds; i++)
            {
                foreach (var guard in patrols)
                    visited[guard].Add(guard.Motor.CurrentRoom);
                yield return 1f;
            }

            foreach (var guard in patrols)
                Check(visited[guard].Count >= 3, $"{guard.displayName} recorre {visited[guard].Count} salas: {string.Join(", ", visited[guard])}");
            Check(HeistEvents.Count(HeistEventKind.AlarmConfirmed) == 0, "con la banda en la calle no salta nada");
        }

        static Color32[] ReadPixels(RenderTexture target)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply(false);
            RenderTexture.active = previous;
            var pixels = image.GetPixels32();
            UnityEngine.Object.DestroyImmediate(image);
            return pixels;
        }

        static void WritePng(string path, Color32[] pixels, int width, int height)
        {
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.SetPixels32(pixels);
            image.Apply(false);
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            Debug.Log($"[EYE] Captura: {path}");
        }
    }
}
