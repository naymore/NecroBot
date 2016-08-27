#region using directives

#region using directives

using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.State;
using POGOProtos.Inventory.Item;
using POGOProtos.Networking.Responses;

#endregion

// ReSharper disable CyclomaticComplexity

#endregion

namespace PoGo.NecroBot.Logic.Utils
{
    using POGOProtos.Data.Player;

    public delegate void StatisticsDirtyDelegate();

    public class Statistics
    {
        private readonly DateTime _initSessionDateTime = DateTime.Now;

        private StatsExport _exportStats;
        private string _playerName;
        public int TotalExperience;
        public int TotalItemsRemoved;
        public int TotalPokemons;
        public int TotalPokemonTransferred;
        public int TotalStardust;
        public int LevelForRewards = -1;

        public void Dirty(Inventory inventory)
        {
            _exportStats = GetCurrentInfo(inventory);
            DirtyEvent?.Invoke();
        }

        public event StatisticsDirtyDelegate DirtyEvent;

        private string FormatRuntime()
        {
            return (DateTime.Now - _initSessionDateTime).ToString(@"dd\.hh\:mm\:ss");
        }

        public StatsExport GetCurrentInfo(Inventory inventory)
        {
            PlayerStats playerStats = inventory.GetPlayerStats().Result;

            if (playerStats == null) return null;

            var ep = playerStats.NextLevelXp - playerStats.PrevLevelXp - (playerStats.Experience - playerStats.PrevLevelXp);
            var time = Math.Round(ep / (TotalExperience / GetRuntime()), 2);
            var hours = 0.00;
            var minutes = 0.00;
            if (double.IsInfinity(time) == false && time > 0)
            {
                hours = Math.Truncate(TimeSpan.FromHours(time).TotalHours);
                minutes = TimeSpan.FromHours(time).Minutes;
            }

            if (LevelForRewards == -1 || playerStats.Level >= LevelForRewards)
            {
                LevelUpRewardsResponse result = Execute(inventory).Result;

                if (result.ToString().ToLower().Contains("awarded_already"))
                    LevelForRewards = playerStats.Level + 1;

                if (result.ToString().ToLower().Contains("success"))
                {
                    Logger.Write("Leveled up: " + playerStats.Level, LogLevel.Info);

                    RepeatedField<ItemAward> items = result.ItemsAwarded;

                    if (items.Any<ItemAward>())
                    {
                        Logger.Write("- Received Items -", LogLevel.Info);
                        foreach (ItemAward item in items)
                        {
                            Logger.Write($"[ITEM] {item.ItemId} x {item.ItemCount} ", LogLevel.Info);
                        }
                    }
                }
            }

            LevelUpRewardsResponse result2 = Execute(inventory).Result;
            LevelForRewards = playerStats.Level;
            if (result2.ToString().ToLower().Contains("success"))
            {
                //string[] tokens = result2.Result.ToString().Split(new[] { "itemId" }, StringSplitOptions.None);
                Logger.Write("Items Awarded:" + result2.ItemsAwarded);
            }

            StatsExport output = new StatsExport
                                     {
                                         Level = playerStats.Level,
                                         HoursUntilLvl = hours,
                                         MinutesUntilLevel = minutes,
                                         CurrentXp = playerStats.Experience - playerStats.PrevLevelXp - GetXpDiff(playerStats.Level),
                                         LevelupXp = playerStats.NextLevelXp - playerStats.PrevLevelXp - GetXpDiff(playerStats.Level)
                                     };

            return output;
        }

        public async Task<LevelUpRewardsResponse> Execute(ISession ctx)
        {
            var result = await ctx.Inventory.GetLevelUpRewards(LevelForRewards);
            return result;
        }

        public async Task<LevelUpRewardsResponse> Execute(Inventory inventory)
        {
            var result = await inventory.GetLevelUpRewards(inventory);
            return result;
        }

        public double GetRuntime()
        {
            return (DateTime.Now - _initSessionDateTime).TotalSeconds / 3600;
        }

        public string GetTemplatedStats(string template, string xpTemplate)
        {
            var xpStats = string.Format(xpTemplate, _exportStats.Level, _exportStats.HoursUntilLvl,
                _exportStats.MinutesUntilLevel, _exportStats.CurrentXp, _exportStats.LevelupXp);

            return string.Format(template, _playerName, FormatRuntime(), xpStats, TotalExperience / GetRuntime(),
                TotalPokemons / GetRuntime(),
                TotalStardust, TotalPokemonTransferred, TotalItemsRemoved);
        }

        public static int GetXpDiff(int level)
        {
            if (level > 0 && level <= 40)
            {
                int[] xpTable =
                {
                    0, 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000,
                    10000, 10000, 10000, 10000, 15000, 20000, 20000, 20000, 25000, 25000,
                    50000, 75000, 100000, 125000, 150000, 190000, 200000, 250000, 300000, 350000,
                    500000, 500000, 750000, 1000000, 1250000, 1500000, 2000000, 2500000, 3000000, 5000000
                };
                return xpTable[level - 1];
            }
            return 0;
        }

        public void SetUsername(GetPlayerResponse profile)
        {
            _playerName = profile.PlayerData.Username ?? "";
        }
    }

    public class StatsExport
    {
        public long CurrentXp;
        public double HoursUntilLvl;
        public int Level;
        public long LevelupXp;
        public double MinutesUntilLevel;
    }
}
