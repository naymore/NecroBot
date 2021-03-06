﻿using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PoGo.NecroBot.Logic.Event;
using PoGo.NecroBot.Logic.Interfaces;
using PoGo.NecroBot.Logic.Logging;
using PoGo.NecroBot.Logic.Model.Settings;
using PokemonGo.RocketAPI.Exceptions;

namespace PoGo.NecroBot.Logic.State
{
    public class StateMachine
    {
        private IState _initialState;

        public Task AsyncStart(IState initialState, Session session, CancellationToken cancellationToken = default(CancellationToken))
        {
            return Task.Run(() => Start(initialState, session, cancellationToken), cancellationToken);
        }

        public void SetFailureState(IState state)
        {
            _initialState = state;
        }

        public async Task Start(IState initialState, Session session, CancellationToken cancellationToken = default(CancellationToken))
        {
            string workingDirectory = session.LogicSettings.WorkingDirectory;
            string configurationDirectory = session.LogicSettings.ConfigurationDirectory;

            FileSystemWatcher configWatcher = new FileSystemWatcher();
            configWatcher.Path = configurationDirectory;
            configWatcher.Filter = "config.json";
            configWatcher.NotifyFilter = NotifyFilters.LastWrite;
            configWatcher.EnableRaisingEvents = true;
            configWatcher.Changed += (sender, e) =>
            {
                if (e.ChangeType == WatcherChangeTypes.Changed)
                {
                    // reload LogicSettings
                    LogicSettings newLogicSettings = new LogicSettings(GlobalSettings.Load(workingDirectory));
                    session.UpdateLogicSettings(newLogicSettings);

                    configWatcher.EnableRaisingEvents = !configWatcher.EnableRaisingEvents;
                    configWatcher.EnableRaisingEvents = !configWatcher.EnableRaisingEvents;

                    Logger.Write(" ##### config.json ##### ", LogLevel.Info);
                }
            };

            IState state = initialState;

            do
            {
                try
                {
                    state = await state.Execute(session, cancellationToken);
                }
                catch (InvalidResponseException)
                {
                    session.EventDispatcher.Send(new ErrorEvent
                    {
                        Message = "Niantic Servers unstable, throttling API Calls."
                    });
                }
                catch (OperationCanceledException)
                {
                    session.EventDispatcher.Send(new ErrorEvent {Message = "Current Operation was canceled."});
                    state = _initialState;
                }
                catch (Exception ex)
                {
                    session.EventDispatcher.Send(new ErrorEvent {Message = "Pokemon Servers might be offline / unstable. Trying again..."});
                    Thread.Sleep(1000);
                    session.EventDispatcher.Send(new ErrorEvent { Message = "Error: " + ex });
                    state = _initialState;
                }
            }
            while (state != null);

            configWatcher.EnableRaisingEvents = false;
            configWatcher.Dispose();
        }
    }
}