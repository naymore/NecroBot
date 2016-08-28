using System.Net;
using PokemonGo.RocketAPI.Enums;
using PokemonGo.RocketAPI.Extensions;
using PokemonGo.RocketAPI.HttpClient;
using POGOProtos.Networking.Envelopes;

namespace PoGo.NecroBot.Logic
{
    using PokemonGo.RocketAPI;

    public class RocketApiClientWrapper
    {
        private readonly Client _rocketClient;

        public RocketApiClientWrapper(Client rocketClient)
        {
            _rocketClient = rocketClient;
        }

        public PokemonGo.RocketAPI.Rpc.Login Login;
        public PokemonGo.RocketAPI.Rpc.Player Player;
        public PokemonGo.RocketAPI.Rpc.Download Download;
        public PokemonGo.RocketAPI.Rpc.Inventory Inventory;
        public PokemonGo.RocketAPI.Rpc.Map Map;
        public PokemonGo.RocketAPI.Rpc.Fort Fort;
        public PokemonGo.RocketAPI.Rpc.Encounter Encounter;
        public PokemonGo.RocketAPI.Rpc.Misc Misc;

        public IApiFailureStrategy ApiFailure { get; set; }
        public ISettings Settings { get; }
        public string AuthToken { get; set; }

        public static WebProxy Proxy;

        public double CurrentLatitude { get; internal set; }
        public double CurrentLongitude { get; internal set; }
        public double CurrentAltitude { get; internal set; }

        public AuthType AuthType => Settings.AuthType;

        internal readonly PokemonHttpClient PokemonHttpClient;
        internal string ApiUrl { get; set; }
        internal AuthTicket AuthTicket { get; set; }

        public RocketApiClientWrapper(ISettings settings, IApiFailureStrategy apiFailureStrategy)
        {
            Settings = settings;
            ApiFailure = apiFailureStrategy;
            Proxy = InitProxy();
            PokemonHttpClient = new PokemonHttpClient();
            Login = new PokemonGo.RocketAPI.Rpc.Login(_rocketClient);
            Player = new PokemonGo.RocketAPI.Rpc.Player(_rocketClient);
            Download = new PokemonGo.RocketAPI.Rpc.Download(_rocketClient);
            Inventory = new PokemonGo.RocketAPI.Rpc.Inventory(_rocketClient);
            Map = new PokemonGo.RocketAPI.Rpc.Map(_rocketClient);
            Fort = new PokemonGo.RocketAPI.Rpc.Fort(_rocketClient);
            Encounter = new PokemonGo.RocketAPI.Rpc.Encounter(_rocketClient);
            Misc = new PokemonGo.RocketAPI.Rpc.Misc(_rocketClient);

            Player.SetCoordinates(Settings.DefaultLatitude, Settings.DefaultLongitude, Settings.DefaultAltitude);
        }

        private WebProxy InitProxy()
        {
            if (!Settings.UseProxy) return null;

            WebProxy prox = new WebProxy(new System.Uri($"http://{Settings.UseProxyHost}:{Settings.UseProxyPort}"), false, null);

            if (Settings.UseProxyAuthentication)
                prox.Credentials = new NetworkCredential(Settings.UseProxyUsername, Settings.UseProxyPassword);

            return prox;
        }
    }
}