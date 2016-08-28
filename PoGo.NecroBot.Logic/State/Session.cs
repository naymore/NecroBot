using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Interfaces;
using PoGo.NecroBot.Logic.Interfaces.Configuration;
using PoGo.NecroBot.Logic.Service;
using PokemonGo.RocketAPI;
using POGOProtos.Networking.Responses;
using PoGo.NecroBot.Logic.Service.Elevation;

namespace PoGo.NecroBot.Logic.State
{
    public class Session : ISession
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly IClientSettings _clientSettings;
        private readonly SessionStats _stats;
        private readonly ITranslation _translation;
        private readonly ElevationService _elevationService;
        private readonly IEventDispatcher _eventDispatcher;

        private ILogicSettings _logicSettings;
        
        public Session(IClientSettings clientSettings, ILogicSettings logicSettings, ITranslation translation)
        {
            _logger.Debug("--- Starting new Session ---");

            _elevationService = new ElevationService(logicSettings);           
            _clientSettings = clientSettings;
            _stats = new SessionStats();
            _translation = translation;
            _logicSettings = logicSettings;
            _eventDispatcher = new EventDispatcher();

            // Update current altitude
            ClientSettings.DefaultAltitude = ElevationService.GetElevation(clientSettings.DefaultLatitude, clientSettings.DefaultLongitude);

            UpdateSessionConfiguration(clientSettings, logicSettings);
        }

        public IClientSettings ClientSettings => _clientSettings;

        public Inventory Inventory { get; private set; }

        public Client Client { get; private set; }

        public GetPlayerResponse Profile { get; set; }

        public Navigation Navigation { get; private set; }

        public ILogicSettings LogicSettings => _logicSettings;

        public ITranslation Translation => _translation;

        public IEventDispatcher EventDispatcher => _eventDispatcher;

        public TelegramService Telegram { get; set; }

        public SessionStats Stats => _stats;

        public ElevationService ElevationService => _elevationService;

        public void UpdateSessionConfiguration(IClientSettings settings, ILogicSettings logicSettings)
        {
            ApiFailureStrategy apiFailureStrategy = new ApiFailureStrategy(this);
            Client = new Client(ClientSettings, apiFailureStrategy);
            
            Inventory = new Inventory(Client, logicSettings);
            Navigation = new Navigation(Client, logicSettings);
        }

        public void UpdateLogicSettings(ILogicSettings newLogicSettings)
        {
            _logicSettings = newLogicSettings;
        }
    }
}