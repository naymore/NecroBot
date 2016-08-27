using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.PoGoUtils;
using PoGo.NecroBot.Logic.State;
using POGOProtos.Inventory.Item;
using POGOProtos.Data;
using POGOProtos.Data.Player;
using POGOProtos.Inventory;
using POGOProtos.Networking.Responses;
using NLog;

namespace PoGo.NecroBot.Logic.Tasks
{   
    public static class UseIncubatorsTask
    {
        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        private static string _rememberedIncubatorsFilePath;
        
        public static async Task Execute(ISession session, CancellationToken cancellationToken)
        {
            _logger.Trace("UseIncubatorsTask called");

            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(_rememberedIncubatorsFilePath))
                _rememberedIncubatorsFilePath = Path.Combine(session.LogicSettings.TempDataDirectory, "incubators.json");

            List<IncubatorUsage> rememberedIncubatorUsages = await CheckForHatchedEggs(session);

            List<IncubatorUsage> newIncubatorUsages = await HatchNewEgg(session);

            if (!newIncubatorUsages.SequenceEqual(rememberedIncubatorUsages))
                SaveRememberedIncubators(newIncubatorUsages);

            _logger.Trace("UseIncubatorsTask completed");
        }

        private static async Task<List<IncubatorUsage>> HatchNewEgg(ISession session)
        {
            _logger.Debug("Hatching new egg?");

            PlayerStats playerStats = await session.Inventory.GetPlayerStats(refreshCache: true);
            //if (playerStats == null) return null; // can this ever be null?

            List<EggIncubator> incubators =
                (await session.Inventory.GetEggIncubators()).Where(incubator => incubator.UsesRemaining > 0 || incubator.ItemId == ItemId.ItemIncubatorBasicUnlimited).ToList();

            _logger.Debug("Incubators :: Count {0}, Total Remaining Usages {1}", incubators.Count, incubators.Sum(incubator => incubator.UsesRemaining));

            List<PokemonData> unusedEggs =
                (await session.Inventory.GetEggs()).Where(egg => string.IsNullOrEmpty(egg.EggIncubatorId))
                    .OrderBy(egg => egg.EggKmWalkedTarget - egg.EggKmWalkedStart)
                    .ToList();

            List<IncubatorUsage> newRememberedIncubators = new List<IncubatorUsage>();

            foreach (EggIncubator incubator in incubators)
            {
                if (incubator.PokemonId == 0)
                {
                    // Unlimited incubators prefer short eggs, limited incubators prefer long eggs
                    // Special case: If only one incubator is available at all, it will prefer long eggs
                    PokemonData egg = incubator.ItemId == ItemId.ItemIncubatorBasicUnlimited && incubators.Count > 1
                        ? unusedEggs.FirstOrDefault()
                        : unusedEggs.LastOrDefault();

                    if (egg == null)
                        continue;

                    // Skip (save) limited incubators depending on user choice in config
                    if (!session.LogicSettings.UseLimitedEggIncubators 
                        && incubator.ItemId != ItemId.ItemIncubatorBasicUnlimited)
                        continue;

                    UseItemEggIncubatorResponse response = await session.Client.Inventory.UseItemEggIncubator(incubator.Id, egg.Id);
                    unusedEggs.Remove(egg);

                    newRememberedIncubators.Add(new IncubatorUsage { IncubatorId = incubator.Id, PokemonId = egg.Id });

                    session.EventDispatcher.Send(new EggIncubatorStatusEvent
                    {
                        IncubatorId = incubator.Id,
                        WasAddedNow = true,
                        PokemonId = egg.Id,
                        KmToWalk = egg.EggKmWalkedTarget,
                        KmRemaining = response.EggIncubator.TargetKmWalked - playerStats.KmWalked
                    });
                }
                else
                {
                    newRememberedIncubators.Add(new IncubatorUsage
                    {
                        IncubatorId = incubator.Id,
                        PokemonId = incubator.PokemonId
                    });

                    session.EventDispatcher.Send(new EggIncubatorStatusEvent
                    {
                        IncubatorId = incubator.Id,
                        PokemonId = incubator.PokemonId,
                        KmToWalk = incubator.TargetKmWalked - incubator.StartKmWalked,
                        KmRemaining = incubator.TargetKmWalked - playerStats.KmWalked
                    });
                }
            }

            return newRememberedIncubators;
        }

        private static async Task<List<IncubatorUsage>> CheckForHatchedEggs(ISession session)
        {
            _logger.Debug("Checking for previously hatched eggs...");

            // Check if eggs in remembered incubator usages have since hatched
            // (instead of calling session.Client.Inventory.GetHatchedEgg(), which doesn't seem to work properly)
            List<IncubatorUsage> rememberedIncubators = GetRememberedIncubators(_rememberedIncubatorsFilePath);
            List<PokemonData> pokemons = (await session.Inventory.GetPokemons()).ToList();

            foreach (IncubatorUsage incubatorUsage in rememberedIncubators)
            {
                PokemonData hatchedPkmn = pokemons.FirstOrDefault(pkmn => !pkmn.IsEgg && pkmn.Id == incubatorUsage.PokemonId);
                if (hatchedPkmn == null)
                {
                    _logger.Trace("Incubator {0}. Previously remembered pokemon {1} has not hatched yet.", incubatorUsage.IncubatorId, incubatorUsage.PokemonId);
                    continue;
                }

                session.EventDispatcher.Send(new EggHatchedEvent
                {
                    Id = hatchedPkmn.Id,
                    PokemonId = hatchedPkmn.PokemonId,
                    Level = PokemonInfo.GetLevel(hatchedPkmn),
                    Cp = hatchedPkmn.Cp,
                    MaxCp = PokemonInfo.CalculateMaxCp(hatchedPkmn),
                    Perfection = Math.Round(PokemonInfo.CalculatePokemonPerfection(hatchedPkmn), 2)
                });
            }

            return rememberedIncubators;
        }

        private static List<IncubatorUsage> GetRememberedIncubators(string filePath)
        {
            _logger.Trace("Loading incubator usages from local file");

            string directoryName = Path.GetDirectoryName(filePath);

            if (!string.IsNullOrEmpty(directoryName))
            {
                Directory.CreateDirectory(directoryName);
            }

            if (File.Exists(filePath))
                return JsonConvert.DeserializeObject<List<IncubatorUsage>>(File.ReadAllText(filePath, Encoding.UTF8));

            return new List<IncubatorUsage>(0);
        }

        private static void SaveRememberedIncubators(List<IncubatorUsage> incubators)
        {
            _logger.Trace("Saving incubator usages to local file");
            if (string.IsNullOrEmpty(_rememberedIncubatorsFilePath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(_rememberedIncubatorsFilePath));

            File.WriteAllText(_rememberedIncubatorsFilePath, JsonConvert.SerializeObject(incubators), Encoding.UTF8);
        }

        private class IncubatorUsage : IEquatable<IncubatorUsage>
        {
            public string IncubatorId;
            public ulong PokemonId;

            public bool Equals(IncubatorUsage other)
            {
                return other != null && other.IncubatorId == IncubatorId && other.PokemonId == PokemonId;
            }
        }
    }
}