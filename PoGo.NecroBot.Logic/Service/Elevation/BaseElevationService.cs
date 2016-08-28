using Caching;
using GeoCoordinatePortable;
using System;

namespace PoGo.NecroBot.Logic.Service.Elevation
{
    public abstract class BaseElevationService
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        protected LRUCache<string, double> Cache;
        protected string ApiKey;

        public abstract double GetElevationFromWebService(double lat, double lng);

        protected BaseElevationService(LRUCache<string, double> cache)
        {
            Cache = cache;
        }

        public string GetCacheKey(double lat, double lng)
        {
            return Math.Round(lat, 3) + "," + Math.Round(lng, 3);
        }

        public string GetCacheKey(GeoCoordinate position)
        {
            return GetCacheKey(position.Latitude, position.Longitude);
        }

        public double GetElevation(double lat, double lng)
        {
            string cacheKey = GetCacheKey(lat, lng);
            double elevation;

            bool success = Cache.TryGetValue(cacheKey, out elevation);

            if (success)
            {
                _logger.Trace("Got elevation for Lat, Long ({0}, {1}) :: {2}m (CACHED)", lat, lng, elevation);
            }
            else
            {
                elevation = GetElevationFromWebService(lat, lng);
                if (elevation != 0.0)
                {
                    Cache.Add(cacheKey, elevation);
                    _logger.Trace("Got elevation for Lat, Long ({0}, {1}) :: {2}m", lat, lng, elevation);
                }
            }

            // Always return a slightly random elevation.
            if (elevation != 0.0)
                elevation = ApplyRandomness(elevation);

            return elevation;
        }

        public double ApplyRandomness(double elevation)
        {
            return elevation + new Random().NextDouble() * 3;
        }
    }
}