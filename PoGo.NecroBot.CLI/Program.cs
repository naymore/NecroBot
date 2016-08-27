#region using directives

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using PoGo.NecroBot.Logic;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.Model.Settings;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Tasks;
using PoGo.NecroBot.Logic.Utils;
using System.Collections.Generic;
using PoGo.NecroBot.CLI.WebSocketHandler;

#endregion

namespace PoGo.NecroBot.CLI
{
    internal class Program
    {
        private static readonly ManualResetEvent QuitEvent = new ManualResetEvent(false);

        private static string _workingFolder = "DEFAULT";
        private static Session session = null;
        private static double Lat, Lng;
        private static bool LocUpdate = false;

        [System.Runtime.InteropServices.DllImport("Kernel32")]
        private static extern bool SetConsoleCtrlHandler(EventHandler handler, bool add);

        private delegate bool EventHandler(CtrlType sig);
        private static EventHandler _handler;

        enum CtrlType
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT = 1,
            CTRL_CLOSE_EVENT = 2,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT = 6
        }

        private static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                _workingFolder = Path.Combine(Directory.GetCurrentDirectory(), args[0]);
            }

            SetupFolders();

            // Sets the logger and the minimum log level
            Logger.SetLogger(new ConsoleLogger(LogLevel.LevelUp), _workingFolder);

            AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionEventHandler;
            AppDomain.CurrentDomain.ProcessExit += OnExitHandler;

            _handler += new EventHandler(OnExit);
            SetConsoleCtrlHandler(_handler, true);

            Console.Title = "NecroBot";
            Console.CancelKeyPress += (sender, eArgs) =>
            {
                QuitEvent.Set();
                eArgs.Cancel = true;
            };

            bool isKillSwitchActive = KillSwitch.IsKillSwitchActive();
            if (isKillSwitchActive) return;

            CultureInfo culture = CultureInfo.CreateSpecificCulture("en");
            CultureInfo.DefaultThreadCurrentCulture = culture;
            Thread.CurrentThread.CurrentCulture = culture;

            GlobalSettings settings;
            bool boolNeedsSetup = false;

            string configurationDirectory = Path.Combine(_workingFolder, "Config");
            string configFilePath = Path.Combine(configurationDirectory, "config.json");
            if (File.Exists(configFilePath))
            {
                // Load the settings from the config file
                // If the current program is not the latest version, ensure we skip saving the file after loading
                // This is to prevent saving the file with new options at their default values so we can check for differences
                settings = GlobalSettings.Load(_workingFolder, !VersionCheckState.IsLatest());
            }
            else
            {
                settings = new GlobalSettings();
                settings.ConfigurationDirectory = configurationDirectory;
                settings.WorkingDirectory = _workingFolder;
                settings.TempDataDirectory = Path.Combine(_workingFolder, "temp");

                //settings.ProfilePath = "LOL#1";
                //settings.ProfileConfigPath = "LOL#2";

                settings.ConsoleConfig.TranslationLanguageCode = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;

                boolNeedsSetup = true;
            }

            //if (args.Length > 1)
            //{
            //    string[] crds = args[1].Split(',');
            //    double lat, lng;
            //    try
            //    {
            //        lat = Double.Parse(crds[0]);
            //        lng = Double.Parse(crds[1]);
            //        settings.LocationConfig.DefaultLatitude = lat;
            //        settings.LocationConfig.DefaultLongitude = lng;
            //    }
            //    catch (Exception) { }
            //}

            string lastPosFile = Path.Combine(configurationDirectory, "LastPos.ini");
            if (File.Exists(lastPosFile) && settings.LocationConfig.StartFromLastPosition)
            {
                var text = File.ReadAllText(lastPosFile);
                string[] crds = text.Split(':');
                double lat, lng;
                try
                {
                    lat = Double.Parse(crds[0]);
                    lng = Double.Parse(crds[1]);
                    settings.LocationConfig.DefaultLatitude = lat;
                    settings.LocationConfig.DefaultLongitude = lng;
                }
                catch (Exception) { }
            }


            LogicSettings logicSettings = new LogicSettings(settings);
            Translation translation = Translation.Load(logicSettings);

            if (settings.GPXConfig.UseGpxPathing)
            {
                var xmlString = File.ReadAllText(settings.GPXConfig.GpxFile);
                var readgpx = new GpxReader(xmlString, translation);
                var nearestPt = readgpx.Tracks.SelectMany(
                    (trk, trkindex) =>
                    trk.Segments.SelectMany(
                        (seg, segindex) =>
                            seg.TrackPoints.Select(
                                (pt, ptindex) =>
                                    new
                                    {
                                        TrackPoint = pt,
                                        TrackIndex = trkindex,
                                        SegIndex = segindex,
                                        PtIndex = ptindex,
                                        Latitude = Convert.ToDouble(pt.Lat, CultureInfo.InvariantCulture),
                                        Longitude = Convert.ToDouble(pt.Lon, CultureInfo.InvariantCulture),
                                        Distance = LocationUtils.CalculateDistanceInMeters(
                                            settings.LocationConfig.DefaultLatitude,
                                            settings.LocationConfig.DefaultLongitude,
                                            Convert.ToDouble(pt.Lat, CultureInfo.InvariantCulture),
                                            Convert.ToDouble(pt.Lon, CultureInfo.InvariantCulture)
                                        )
                                    }
                            )
                    )
                ).OrderBy(pt => pt.Distance).FirstOrDefault(pt => pt.Distance <= 5000);

                if (nearestPt != null)
                {
                    settings.LocationConfig.DefaultLatitude = nearestPt.Latitude;
                    settings.LocationConfig.DefaultLongitude = nearestPt.Longitude;
                    settings.LocationConfig.ResumeTrack = nearestPt.TrackIndex;
                    settings.LocationConfig.ResumeTrackSeg = nearestPt.SegIndex;
                    settings.LocationConfig.ResumeTrackPt = nearestPt.PtIndex;
                }
            }

            session = new Session(new ClientSettings(settings), logicSettings, translation);

            //Teste.Testar(session);
            if (boolNeedsSetup)
            {
                if (GlobalSettings.PromptForSetup(session.Translation))
                {
                    session = GlobalSettings.SetupSettings(session, settings, configFilePath);

                    var fileName = Assembly.GetExecutingAssembly().Location;
                    System.Diagnostics.Process.Start(fileName);
                    Environment.Exit(0);
                }
                else
                {
                    // do we have "settings" here?
                    GlobalSettings.Load(_workingFolder);

                    Logger.Write("Press a Key to continue...",
                        LogLevel.Warning);
                    Console.ReadKey();
                    return;
                }

            }

            session.Client.ApiFailure = new ApiFailureStrategy(session);

            /*SimpleSession session = new SimpleSession
            {
                _client = new PokemonGo.RocketAPI.Client(new ClientSettings(settings)),
                _dispatcher = new EventDispatcher(),
                _localizer = new Localizer()
            };

            BotService service = new BotService
            {
                _session = session,
                _loginTask = new Login(session)
            };

            service.Run();
            */

            var machine = new StateMachine();
            var stats = new Statistics();

            string strVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3);
            stats.DirtyEvent +=
                () =>
                    Console.Title = $"[Necrobot v{strVersion}] " +
                        stats.GetTemplatedStats(
                            session.Translation.GetTranslation(TranslationString.StatsTemplateString),
                            session.Translation.GetTranslation(TranslationString.StatsXpTemplateString));

            StatisticsAggregatorEventListener statisticsAggregatorEventListenerEventListener = new StatisticsAggregatorEventListener(stats);
            ConsoleEventListener consoleEventListener = new ConsoleEventListener();
            SniperEventListener snipeEventListener = new SniperEventListener();

            session.EventDispatcher.EventReceived += evt => consoleEventListener.Listen(evt, session);
            session.EventDispatcher.EventReceived += evt => statisticsAggregatorEventListenerEventListener.Listen(evt, session);
            session.EventDispatcher.EventReceived += evt => snipeEventListener.Listen(evt, session);

            if (settings.WebsocketsConfig.UseWebsocket)
            {
                var websocket = new WebSocketInterface(settings.WebsocketsConfig.WebSocketIpAddress, settings.WebsocketsConfig.WebSocketPort, session);
                session.EventDispatcher.EventReceived += evt => websocket.Listen(evt, session);
            }

            machine.SetFailureState(new LoginState());

            Logger.SetLoggerContext(session);

            session.Navigation.WalkStrategy.UpdatePositionEvent += (lat, lng) => session.EventDispatcher.Send(new UpdatePositionEvent { Latitude = lat, Longitude = lng });
            session.Navigation.WalkStrategy.UpdatePositionEvent += (lat, lng) => { LocUpdate = true; Lat = lat; Lng = lng; };

            machine.AsyncStart(new VersionCheckState(), session);

            try
            {
                Console.Clear();
            }
            catch (IOException) { }
            
            if (settings.TelegramConfig.UseTelegramAPI)
                session.Telegram = new Logic.Service.TelegramService(settings.TelegramConfig.TelegramAPIKey, session);

            if (session.LogicSettings.UseSnipeLocationServer)
                SnipePokemonTask.AsyncStart(session);

            settings.checkProxy(session.Translation);

            QuitEvent.WaitOne();
        }

        // Sets up all the folders we need to work properly.
        private static void SetupFolders()
        {
            Directory.CreateDirectory(Path.Combine(_workingFolder, "Config"));
            Directory.CreateDirectory(Path.Combine(_workingFolder, "Temp"));
            Directory.CreateDirectory(Path.Combine(_workingFolder, "Logs"));
        }

        private static void SaveLocationToDisk(double lat, double lng)
        {
            string coordsPath = Path.Combine(session.LogicSettings.TempDataDirectory, "LastPos.ini");

            File.WriteAllText(coordsPath, $"{lat}:{lng}");
        }

        private static void SaveTimeStampsToDisk()
        {
            if (session == null) return;

            string filePath = Path.Combine(session.LogicSettings.TempDataDirectory, "PokestopTS.txt");
            List<string> fileContent = new List<string>();

            foreach (var t in session.Stats.PokeStopTimestamps)
                fileContent.Add(t.ToString());

            if (fileContent.Count > 0)
                File.WriteAllLines(filePath, fileContent.ToArray());

            filePath = Path.Combine(session.LogicSettings.TempDataDirectory, "PokemonTS.txt");
            fileContent = new List<string>();
            foreach (var t in session.Stats.PokemonTimestamps)
                fileContent.Add(t.ToString());

            if (fileContent.Count > 0)
                File.WriteAllLines(filePath, fileContent.ToArray());
        }

        private static void UnhandledExceptionEventHandler(object obj, UnhandledExceptionEventArgs args)
        {
            Logger.Write("Exception caught, writing LogBuffer.", force: true);
            throw new Exception("Unhandled Exception occured", args.ExceptionObject as Exception);
        }

        // Lets save my SSD...
        private static bool OnExit(CtrlType sig)
        {
            if (LocUpdate)
                SaveLocationToDisk(Lat, Lng);

            SaveTimeStampsToDisk();

            return true;
        }

        private static void OnExitHandler(object s, EventArgs e)
        {
            OnExit(CtrlType.CTRL_CLOSE_EVENT);
        }
    }
}