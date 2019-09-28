﻿using Converto;
using SuccincT.Options;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using static ReduxSimple.Reducers;
using static ReduxSimple.Uwp.Samples.Common.EventTracking;

namespace ReduxSimple.Uwp.Samples.Components
{
    public sealed partial class HistoryComponent : UserControl
    {
        private class HistoryComponentState
        {
            public ImmutableList<object> CurrentActions { get; set; } = ImmutableList<object>.Empty;
            public ImmutableList<object> FutureActions { get; set; } = ImmutableList<object>.Empty;
            public int MaxPosition { get; set; } = 0;
            public int CurrentPosition { get; set; } = 0;
            public bool PlaySessionActive { get; set; } = false;
        }

        private static IEnumerable<On<HistoryComponentState>> CreateReducers()
        {
            return new List<On<HistoryComponentState>>
            {
                On<GoBackAction, HistoryComponentState>(
                    state =>
                    {
                        var lastAction = state.CurrentActions.Last();

                        return state.With(new
                        {
                            CurrentActions = state.CurrentActions.Remove(lastAction),
                            FutureActions = state.FutureActions.Add(lastAction),
                            CurrentPosition = state.CurrentPosition - 1
                        });
                    }
                ),
                On<GoForwardAction, HistoryComponentState>(
                    (state, action) =>
                    {
                        var futureActionOption = state.FutureActions.TryLast();

                        if (futureActionOption.HasValue && !action.BreaksTimeline)
                        {
                            // Continue on existing timeline
                            var futureAction = futureActionOption.Value;
                            return state.With(new
                            {
                                CurrentActions = state.CurrentActions.Add(futureAction),
                                FutureActions = state.FutureActions.Remove(futureAction),
                                CurrentPosition = state.CurrentPosition + 1
                            });
                        }
                        else
                        {
                            // Create a new timeline
                            return state.With(new
                            {
                                CurrentActions = state.CurrentActions.Add(action.Action),
                                FutureActions = ImmutableList<object>.Empty,
                                MaxPosition = state.CurrentActions.Count + 1,
                                CurrentPosition = state.CurrentPosition + 1
                            });
                        }
                    }
                ),
                On<ResetAction, HistoryComponentState>(
                    state => state.With(new {
                        CurrentActions = ImmutableList<object>.Empty,
                        FutureActions = ImmutableList<object>.Empty,
                        MaxPosition = 0,
                        CurrentPosition = 0
                    })
                ),
                On<TogglePlayPauseAction, HistoryComponentState>(
                    state => state.With(new { PlaySessionActive = !state.PlaySessionActive })
                )
            };
        }

        private class GoBackAction { }
        private class GoForwardAction
        {
            public object Action { get; set; }
            public bool BreaksTimeline { get; set; }
        }
        private class ResetAction { }
        private class MoveToPositionAction
        {
            public int Position { get; set; }
        }
        private class TogglePlayPauseAction { }

        private readonly ReduxStore<HistoryComponentState> _internalStore = 
            new ReduxStore<HistoryComponentState>(CreateReducers());

        public HistoryComponent()
        {
            InitializeComponent();
        }

        public void Initialize<TState>(ReduxStore<TState> store) where TState : class, new()
        {
            if (store.TimeTravelEnabled)
            {
                // TODO : Cannot activate History component
            }

            // Observe UI events
            UndoButton.Events().Click
                .Subscribe(_ => store.Undo());
            RedoButton.Events().Click
                .Subscribe(_ => store.Redo());
            ResetButton.Events().Click
                .Subscribe(_ => store.Reset());

            PlayPauseButton.Events().Click
                .Subscribe(_ => _internalStore.Dispatch(new TogglePlayPauseAction()));

            Slider.Events().ValueChanged
                .Where(_ => Slider.FocusState != FocusState.Unfocused)
                .Subscribe(e =>
                {
                    int newPosition = (int)e.NewValue;
                    _internalStore.Dispatch(new MoveToPositionAction { Position = newPosition });
                });

            // Observe changes on internal state
            _internalStore.Select(state => state.MaxPosition)
                .Subscribe(maxPosition =>
                {
                    Slider.Maximum = maxPosition;
                });

            Observable.CombineLatest(
                _internalStore.Select(state => state.CurrentPosition),
                _internalStore.Select(state => state.PlaySessionActive),
                _internalStore.Select(state => state.MaxPosition),
                store.ObserveCanUndo(),
                store.ObserveCanRedo(),
                (value1, value2, value3, value4, value5) => Tuple.Create(value1, value2, value3, value4, value5)
            )
                .ObserveOnDispatcher()
                .Subscribe(x =>
                {
                    var (currentPosition, playSessionActive, maxPosition, canUndo, canRedo) = x;

                    Slider.Value = currentPosition;

                    if (playSessionActive)
                    {
                        UndoButton.IsEnabled = false;
                        RedoButton.IsEnabled = false;
                        ResetButton.IsEnabled = false;
                        PlayPauseButton.IsEnabled = true;

                        Slider.IsEnabled = false;

                        PlayPauseButton.Content = "\xE769";
                    }
                    else
                    {
                        UndoButton.IsEnabled = canUndo;
                        RedoButton.IsEnabled = canRedo;
                        ResetButton.IsEnabled = canUndo || canRedo;
                        PlayPauseButton.IsEnabled = canRedo;

                        Slider.IsEnabled = maxPosition > 0;

                        PlayPauseButton.Content = "\xE768";
                    }
                });

            _internalStore.ObserveAction<MoveToPositionAction>()
                .Subscribe(a =>
                {
                    if (a.Position < _internalStore.State.CurrentPosition)
                    {
                        for (int i = 0; i < _internalStore.State.CurrentPosition - a.Position; i++)
                        {
                            store.Undo();
                        }
                    }
                    if (a.Position > _internalStore.State.CurrentPosition)
                    {
                        for (int i = 0; i < a.Position - _internalStore.State.CurrentPosition; i++)
                        {
                            store.Redo();
                        }
                    }
                });

            // Observe changes on listened state
            var goForwardNormalActionOrigin = store.ObserveAction()
                .Select(action => new { Action = action, BreaksTimeline = true });
            var goForwardRedoneActionOrigin = store.ObserveAction(ActionOriginFilter.Redone)
                .Select(action => new { Action = action, BreaksTimeline = false });

            goForwardNormalActionOrigin.Merge(goForwardRedoneActionOrigin)
                .ObserveOnDispatcher()
                .Subscribe(x =>
                {
                    _internalStore.Dispatch(new GoForwardAction { Action = x.Action, BreaksTimeline = x.BreaksTimeline });
                    if (_internalStore.State.PlaySessionActive && !store.CanRedo)
                    {
                        _internalStore.Dispatch(new TogglePlayPauseAction());
                    }
                });

            store.ObserveUndoneAction()
                .ObserveOnDispatcher()
                .Subscribe(_ => _internalStore.Dispatch(new GoBackAction()));

            store.ObserveReset()
                .ObserveOnDispatcher()
                .Subscribe(_ => _internalStore.Dispatch(new ResetAction()));

            _internalStore.Select(state => state.PlaySessionActive)
                .Select(playSessionActive =>
                    playSessionActive ? Observable.Interval(TimeSpan.FromSeconds(1)) : Observable.Empty<long>()
                )
                .Switch()
                .ObserveOnDispatcher()
                .Subscribe(_ =>
                {
                    bool canRedo = store.Redo();
                    if (!canRedo)
                    {
                        _internalStore.Dispatch(new TogglePlayPauseAction());
                    }
                });

            // Track redux actions
            _internalStore.ObserveAction()
                .Subscribe(action =>
                {
                    TrackReduxAction(action);
                });
        }
    }
}
