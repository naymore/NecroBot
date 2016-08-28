#region using directives

using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Interfaces;
using PoGo.NecroBot.Logic.State;
using PoGo.NecroBot.Logic.Utils;
using POGOProtos.Networking.Responses;

#endregion

namespace PoGo.NecroBot.Logic
{
    public class StatisticsAggregatorEventListener
    {
        private readonly Statistics _stats;

        public StatisticsAggregatorEventListener(Statistics stats)
        {
            _stats = stats;
        }

        public void Listen(IEvent evt, ISession session)
        {
            dynamic eve = evt;

            try
            {
                HandleEvent(eve, session);
            }
            catch
            {
                // NOTE: Missing signatures will cause exceptions to be thrown. If you add events make sure you add them to all subscribers
                // such as StatisticsAggregatorEventListener, ConsoleEventListener and WebSocketInterface (these are the ones I know of)
                // -OR- add a generic handler with dynamic signature. FWIW: IEvent to dynamic is bad design after all.
            }
        }

        #region -- Event Handlers --

        public void HandleEvent(ProfileEvent evt, ISession session)
        {
            _stats.SetUsername(evt.Profile);
            _stats.Dirty(session.Inventory);
        }

        public void HandleEvent(PokemonEvolveEvent evt, ISession session)
        {
            _stats.TotalExperience += evt.Exp;
            _stats.Dirty(session.Inventory);
        }

        public void HandleEvent(TransferPokemonEvent evt, ISession session)
        {
            _stats.TotalPokemonTransferred++;
            _stats.Dirty(session.Inventory);
        }

        public void HandleEvent(ItemRecycledEvent evt, ISession session)
        {
            _stats.TotalItemsRemoved++;
            _stats.Dirty(session.Inventory);
        }

        public void HandleEvent(FortUsedEvent evt, ISession session)
        {
            _stats.TotalExperience += evt.Exp;
            _stats.Dirty(session.Inventory);
        }

        public void HandleEvent(PokemonCaptureEvent evt, ISession session)
        {
            if (evt.Status == CatchPokemonResponse.Types.CatchStatus.CatchSuccess)
            {
                _stats.TotalExperience += evt.Exp;
                _stats.TotalPokemons++;
                _stats.TotalStardust = evt.Stardust;
                _stats.Dirty(session.Inventory);
            }
        }

        private void HandleEvent(dynamic ignoredEvent, ISession session)
        {
            // Handle all events I don't care about
            // NOP.
        }

        #endregion
    }
}