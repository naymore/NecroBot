namespace PoGo.NecroBot.Logic.Model.Settings
{
    public class WebsocketsConfig
    {
        public bool UseWebsocket { get; set; }

        public int WebSocketPort { get; set; } = 14251;

        public string WebSocketIpAddress { get; set; } = "Any";
    }
}