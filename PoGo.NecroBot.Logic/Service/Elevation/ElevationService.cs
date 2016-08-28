using Caching;
using PoGo.NecroBot.Logic.Interfaces.Configuration;

namespace PoGo.NecroBot.Logic.Service.Elevation
{
    public class ElevationService
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly LRUCache<string, double> _cache = new LRUCache<string, double>(capacity: 500);
        private readonly MapQuestElevationService _mapQuestService;
        private readonly GoogleElevationService _googleService;

        public ElevationService(ILogicSettings logicSettings)
        {
            _mapQuestService = new MapQuestElevationService(_cache);
            _googleService = new GoogleElevationService(logicSettings, _cache);
        }

        public double GetElevation(double latitude, double longitude)
        {
            _logger.Trace("Elevation LRUCache contains {0}/{1} items.", _cache.Count, _cache.Capacity);

            // First try Google service
            double elevation = _googleService.GetElevation(latitude, longitude);
            if (elevation == 0)
            {
                // Fallback to MapQuest service
                elevation = _mapQuestService.GetElevation(latitude, longitude);
            }

            return elevation;
        }
    }
}