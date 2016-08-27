#region using directives

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Tasks;
using SuperSocket.SocketBase;
using SuperSocket.SocketBase.Config;
using SuperSocket.WebSocket;
using System.Threading.Tasks;
using PoGo.NecroBot.CLI.Utils;

#endregion

namespace PoGo.NecroBot.CLI.WebSocketHandler
{
    public class WebSocketInterface : IDisposable
    {
        private readonly WebSocketEventManager _websocketHandler;
        private readonly WebSocketServer _server;
        private readonly Session _session;
        private PokeStopListEvent _lastPokeStopList;
        private ProfileEvent _lastProfile;

        private readonly JsonSerializerSettings _jsonSerializerSettings;

        public WebSocketInterface(string ipAddress, int port, Session session)
        {
            _session = session;
            _server = new WebSocketServer();
            _websocketHandler = WebSocketEventManager.CreateInstance();

            // Add custom seriaizer to convert ulong to string (ulong shoud not appear to json according to json specs)
            _jsonSerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All };
            _jsonSerializerSettings.Converters.Add(new LongToStringJsonConverter());

            ITranslation translations = session.Translation;

            ServerConfig config = new ServerConfig
            {
                Name = "NecroWebSocket",
                Mode = SocketMode.Tcp,
                Certificate = new CertificateConfig { FilePath = @"cert.pfx", Password = "necro" },
                Listeners =
                                     new List<ListenerConfig>
                                         {
                                             new ListenerConfig { Ip = ipAddress, Port = port, Security = "tls" },
                                             new ListenerConfig { Ip = ipAddress, Port = port + 1, Security = "none" }
                                         },
            };

            bool setupComplete = _server.Setup(config);

            if (setupComplete == false)
            {
                Logger.Write(translations.GetTranslation(TranslationString.WebSocketFailStart, port), LogLevel.Error);
                return;
            }

            _server.NewMessageReceived += HandleMessage;
            _server.NewSessionConnected += HandleSession;

            _server.Start();
        }

        private void Broadcast(string message)
        {
            foreach (var session in _server.GetAllSessions())
            {
                try
                {
                    session.Send(message);
                }
                catch
                {
                    // ignored
                }
            }
        }

        private async void HandleMessage(WebSocketSession session, string message)
        {
            switch (message)
            {
                case "PokemonList":
                    await PokemonListTask.Execute(_session);
                    break;
                case "EggsList":
                    await EggsListTask.Execute(_session);
                    break;
                case "InventoryList":
                    await InventoryListTask.Execute(_session);
                    break;
            }

            // Setup to only send data back to the session that requested it. 
            try
            {
                dynamic decodedMessage = JObject.Parse(message);
                Task handle = _websocketHandler?.Handle(_session, session, decodedMessage);
                if (handle != null)
                    await handle;
            }
            catch
            {
                // ignored
            }
        }

        private void HandleSession(WebSocketSession session)
        {
            if (_lastProfile != null)
                session.Send(Serialize(_lastProfile));

            if (_lastPokeStopList != null)
                session.Send(Serialize(_lastPokeStopList));

            try
            {
                session.Send(
                    Serialize(new UpdatePositionEvent() { Latitude = _session.Client.CurrentLatitude, Longitude = _session.Client.CurrentLongitude }));
            }
            catch
            {
                // ignnored
            }
        }

        public void Listen(IEvent evt, Session session)
        {
            dynamic eve = evt;

            try
            {
                HandleEvent(eve);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;

                // NOTE: Missing signatures will cause exceptions to be thrown. If you add events make sure you add them to all subscribers
                // such as StatisticsAggregator, ConsoleEventListener and WebSocketInterface (these are the ones I know of)
                // -OR- add a generic handler with dynamic signature. FWIW: IEvent to dynamic is bad design after all.
            }

            Broadcast(Serialize(eve));
        }

        #region -- Event Handlers --

        private void HandleEvent(PokeStopListEvent evt)
        {
            _lastPokeStopList = evt;
        }

        private void HandleEvent(ProfileEvent evt)
        {
            _lastProfile = evt;
        }

        private void HandleEvent(dynamic ignoredEvent)
        {
            // Handle all events I don't care about
            // NOP.
        }

        #endregion

        private string Serialize(dynamic evt)
        {
            return JsonConvert.SerializeObject(evt, Formatting.None, _jsonSerializerSettings);
        }

        #region -- IDisposable --

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _server?.Dispose();
            }
        }

        #endregion
    }
}