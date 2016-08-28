using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Interfaces.Configuration;
using PoGo.NecroBot.Logic.Service;
using PoGo.NecroBot.Logic.Service.Elevation;
using PoGo.NecroBot.Logic.State;
using PokemonGo.RocketAPI;
using POGOProtos.Networking.Responses;

namespace PoGo.NecroBot.Logic.Interfaces
{
    public interface ISession
    {
        IClientSettings ClientSettings { get; }

        Inventory Inventory { get; }

        Client Client { get; }

        GetPlayerResponse Profile { get; set; }

        Navigation Navigation { get; }

        ILogicSettings LogicSettings { get; }

        ITranslation Translation { get; }

        IEventDispatcher EventDispatcher { get; }

        TelegramService Telegram { get; set; }

        SessionStats Stats { get; }

        ElevationService ElevationService { get; }
    }
}