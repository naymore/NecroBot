using Caching;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace PoGo.NecroBot.Logic.Service.Elevation
{
    public class MapQuestElevationService : BaseElevationService
    {
        private const string MAP_QUEST_DEMO_API_KEY = "Kmjtd|luua2qu7n9,7a=o5-lzbgq";

        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public MapQuestElevationService(LRUCache<string, double> cache) : base(cache)
        {
            if (!string.IsNullOrEmpty(MAP_QUEST_DEMO_API_KEY))
                ApiKey = MAP_QUEST_DEMO_API_KEY;
        }

        public override double GetElevationFromWebService(double lat, double lng)
        {
            if (string.IsNullOrEmpty(ApiKey))
                return 0;

            double elevationInMeters = 0;

            try
            {
                string url = $"https://open.mapquestapi.com/elevation/v1/profile?key={ApiKey}&callback=handleHelloWorldResponse&shapeFormat=raw&latLngCollection={lat},{lng}";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Credentials = CredentialCache.DefaultCredentials;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2";
                request.ContentType = "application/json";
                request.Referer = "https://open.mapquestapi.com/elevation/";
                request.ReadWriteTimeout = 2000;

                using (WebResponse response = request.GetResponse())
                {
                    using (Stream dataStream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(dataStream))
                    {
                        string responseFromServer = reader.ReadToEnd();
                        responseFromServer = responseFromServer.Replace("handleHelloWorldResponse(", "");
                        responseFromServer = responseFromServer.Replace("]}});", "]}}");
                        MapQuestResponse mapQuestResponse = JsonConvert.DeserializeObject<MapQuestResponse>(responseFromServer);
                        if (mapQuestResponse.ElevationProfile != null && 0 < mapQuestResponse.ElevationProfile.Count)
                        {
                            elevationInMeters = mapQuestResponse.ElevationProfile[0].Height;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Could not get elevation/altitude. Returning 0.");
                elevationInMeters = 0;
            }

            return elevationInMeters;
        }

        protected class MapQuestResponse
        {
            public List<ElevationProfiles> ElevationProfile { get; set; }
        }

        protected struct ElevationProfiles
        {
            public double Distance { get; set; }

            public double Height { get; set; }
        }
    }
}