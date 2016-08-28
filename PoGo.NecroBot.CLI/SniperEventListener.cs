using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Interfaces;
using PoGo.NecroBot.Logic.State;

namespace PoGo.NecroBot.CLI
{
    internal class SniperEventListener
    {
        private static void HandleEvent(PokemonCaptureEvent pokemonCaptureEvent, ISession session)
        {
            //remove pokemon from list
            Logic.Tasks.HumanWalkSnipeTask.UpdateCatchPokemon(pokemonCaptureEvent.Latitude, pokemonCaptureEvent.Longitude, pokemonCaptureEvent.Id);
        }

        internal void Listen(IEvent event1, ISession session)
        {
            dynamic dynamicEvent = event1;

            try
            {
                HandleEvent(dynamicEvent, session);
            }
            catch
            {
                // NOTE: Missing signatures will cause exceptions to be thrown. If you add events make sure you add them to all subscribers
                // such as StatisticsAggregatorEventListener, ConsoleEventListener and WebSocketInterface (these are the ones I know of)
                // -OR- add a generic handler with dynamic signature. FWIW: IEvent to dynamic is bad design after all.
            }
        }

        private void HandleEvent(dynamic ignoredEvent, ISession session)
        {
            // Handle all events I don't care about
            // NOP.
        }
    }
}
