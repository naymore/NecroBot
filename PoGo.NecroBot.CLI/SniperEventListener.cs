using PoGo.NecroBot.Logic.Event;
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

        internal void Listen(IEvent evt, ISession session)
        {
            dynamic eve = evt;

            try
            {
                HandleEvent(eve, session);
            }
            catch
            { }
        }
    }
}
