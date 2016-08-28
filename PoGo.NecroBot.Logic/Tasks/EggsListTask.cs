#region using directives

using System.Linq;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Interfaces;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using POGOProtos.Inventory.Item;

#endregion

namespace PoGo.NecroBot.Logic.Tasks
{
    using POGOProtos.Data.Player;

    public class EggsListTask
    {
        public static async Task Execute(ISession session)
        {
            PlayerStats playerStats = await session.Inventory.GetPlayerStats(refreshCache: true);
            if (playerStats == null)
                return;

            var kmWalked = playerStats.KmWalked;

            var incubators = (await session.Inventory.GetEggIncubators())
                .Where(x => x.UsesRemaining > 0 || x.ItemId == ItemId.ItemIncubatorBasicUnlimited)
                .OrderByDescending(x => x.ItemId == ItemId.ItemIncubatorBasicUnlimited)
                .ToList();

            var unusedEggs = (await session.Inventory.GetEggs())
                .Where(x => string.IsNullOrEmpty(x.EggIncubatorId))
                .OrderBy(x => x.EggKmWalkedTarget - x.EggKmWalkedStart)
                .ToList();

            session.EventDispatcher.Send(
                new EggsListEvent
                {
                    PlayerKmWalked = kmWalked,
                    Incubators = incubators,
                    UnusedEggs = unusedEggs
                });

            DelayingUtils.Delay(session.LogicSettings.DelayBetweenPlayerActions, 0);
        }
    }
}