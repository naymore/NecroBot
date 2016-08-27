using System.Threading;
using System.Threading.Tasks;
using GeoCoordinatePortable;
using PoGo.NecroBot.Logic.Common;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using PoGo.NecroBot.Logic.Logging;

namespace PoGo.NecroBot.Logic.Tasks
{
    public static class FarmPokestopsTask
    {
        private static readonly NLog.Logger _logger = NLog.LogManager.GetCurrentClassLogger();
        private static bool _firstStart = true;

        public static async Task Execute(ISession session, CancellationToken cancellationToken)
        {
            _logger.Debug("FarmPokestopsTask called.");

            cancellationToken.ThrowIfCancellationRequested();

            double distanceFromStart = LocationUtils.CalculateDistanceInMeters(
                session.Settings.DefaultLatitude, session.Settings.DefaultLongitude, session.Client.CurrentLatitude, session.Client.CurrentLongitude);

            // Edge case for when the client somehow ends up outside the defined radius
            if (session.LogicSettings.MaxTravelDistanceInMeters != 0 && _firstStart &&
                distanceFromStart > session.LogicSettings.MaxTravelDistanceInMeters)
            {
                _firstStart = false;

                Logger.Write(
                    session.Translation.GetTranslation(TranslationString.FarmPokestopsOutsideRadius, distanceFromStart),
                    LogLevel.Warning);

                var eggWalker = new EggWalker(1000, session);

                await session.Navigation.Move(new GeoCoordinate(
                    session.Settings.DefaultLatitude,
                    session.Settings.DefaultLongitude,
                    LocationUtils.getElevation(session, session.Settings.DefaultLatitude,
                    session.Settings.DefaultLongitude)),
                    null,
                    session,
                    cancellationToken);

                // we have moved this distance, so apply it immediately to the egg walker.
                await eggWalker.ApplyDistance(distanceFromStart, cancellationToken);
            }

            // initialize the variables in UseNearbyPokestopsTask here, as this is a fresh start.
            UseNearbyPokestopsTask.Initialize();
            await UseNearbyPokestopsTask.Execute(session, cancellationToken);
        }
    }
}
