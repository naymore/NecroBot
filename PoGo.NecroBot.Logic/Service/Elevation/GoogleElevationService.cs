using Caching;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using PoGo.NecroBot.Logic.Interfaces.Configuration;

namespace PoGo.NecroBot.Logic.Service.Elevation
{
    public class GoogleElevationService : BaseElevationService
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        public GoogleElevationService(ILogicSettings logicSettings, LRUCache<string, double> cache) : base(cache)
        {
            ApiKey = logicSettings.GoogleApiKey;
        }

        public override double GetElevationFromWebService(double lat, double lng)
        {
            if (string.IsNullOrEmpty(ApiKey))
            {
                _logger.Trace("No Google API key set, returning elevation 0.");
                return 0;
            }

            double elevationInMeters = 0;

            try
            {
                string url = $"https://maps.googleapis.com/maps/api/elevation/json?key={ApiKey}&locations={lat},{lng}";
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.Credentials = CredentialCache.DefaultCredentials;
                request.UserAgent = "Mozilla/5.0 (Windows NT 6.1; WOW64) AppleWebKit/535.2 (KHTML, like Gecko) Chrome/15.0.874.121 Safari/535.2";
                request.ContentType = "application/json";
                request.ReadWriteTimeout = 2000;

                using (WebResponse response = request.GetResponse())
                {
                    using (Stream dataStream = response.GetResponseStream())
                    using (StreamReader reader = new StreamReader(dataStream))
                    {
                        string responseFromServer = reader.ReadToEnd();
                        GoogleResponse googleResponse = JsonConvert.DeserializeObject<GoogleResponse>(responseFromServer);

                        if (googleResponse.Status != "OK")
                        {
                            _logger.Warn("Request to Google API failed: {0}", googleResponse.ErrorMessage);
                        }
                        else
                        {
                            if (googleResponse.Results != null && googleResponse.Results.Count != 0)
                            {
                                elevationInMeters = googleResponse.Results[0].Elevation;
                            }
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

        protected class GoogleResponse
        {
            [JsonProperty("error_message")]
            public string ErrorMessage { get; set; }

            [JsonProperty("status")]
            public string Status { get; set; }

            public List<GoogleElevationResults> Results { get; set; }
        }

        protected struct GoogleElevationResults
        {
            public double Elevation { get; set; }
            public double Resolution { get; set; }
            public GoogleLocation GoogleLocation { get; set; }
        }

        protected struct GoogleLocation
        {
            public double Lat { get; set; }
            public double Long { get; set; }
        }
    }
}
