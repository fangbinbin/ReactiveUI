﻿// Copyright (c) 2019 .NET Foundation and Contributors. All rights reserved.
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using Splat;

namespace ReactiveUI
{
    /// <summary>
    /// A set of extension methods to help wire up View and ViewModel activation.
    /// </summary>
    public static class ViewForMixins
    {
        private static readonly MemoizingMRUCache<Type, IActivationForViewFetcher> activationFetcherCache =
            new MemoizingMRUCache<Type, IActivationForViewFetcher>(
               (t, _) =>
                   Locator.Current
                          .GetServices<IActivationForViewFetcher>()
                          .Aggregate(Tuple.Create(0, default(IActivationForViewFetcher)), (acc, x) =>
                          {
                              int score = x.GetAffinityForView(t);
                              return (score > acc.Item1) ? Tuple.Create(score, x) : acc;
                          }).Item2, RxApp.SmallCacheLimit);

        static ViewForMixins()
        {
            RxApp.EnsureInitialized();
        }

        /// <summary>
        /// WhenActivated allows you to register a Func to be called when a
        /// ViewModel's View is Activated.
        /// </summary>
        /// <param name="this">Object that supports activation.</param>
        /// <param name="block">
        /// The method to be called when the corresponding
        /// View is activated. It returns a list of Disposables that will be
        /// cleaned up when the View is deactivated.
        /// </param>
        public static void WhenActivated(this ISupportsActivation @this, Func<IEnumerable<IDisposable>> block)
        {
            @this.Activator.AddActivationBlock(block);
        }

        /// <summary>
        /// WhenActivated allows you to register a Func to be called when a
        /// ViewModel's View is Activated.
        /// </summary>
        /// <param name="this">Object that supports activation.</param>
        /// <param name="block">
        /// The method to be called when the corresponding
        /// View is activated. The Action parameter (usually called 'd') allows
        /// you to register Disposables to be cleaned up when the View is
        /// deactivated (i.e. "d(someObservable.Subscribe());").
        /// </param>
        public static void WhenActivated(this ISupportsActivation @this, Action<Action<IDisposable>> block)
        {
            @this.Activator.AddActivationBlock(() =>
            {
                var ret = new List<IDisposable>();
                block(ret.Add);
                return ret;
            });
        }

        /// <summary>
        /// WhenActivated allows you to register a Func to be called when a
        /// ViewModel's View is Activated.
        /// </summary>
        /// <param name="this">Object that supports activation.</param>
        /// <param name="block">
        /// The method to be called when the corresponding
        /// View is activated. The Action parameter (usually called 'disposables') allows
        /// you to collate all the disposables to be cleaned up during deactivation.
        /// </param>
        public static void WhenActivated(this ISupportsActivation @this, Action<CompositeDisposable> block)
        {
            @this.Activator.AddActivationBlock(() =>
            {
                var d = new CompositeDisposable();
                block(d);
                return new[] { d };
            });
        }

        /// <summary>
        /// WhenActivated allows you to register a Func to be called when a
        /// View is Activated.
        /// </summary>
        /// <param name="this">Object that supports activation.</param>
        /// <param name="block">
        /// The method to be called when the corresponding
        /// View is activated. It returns a list of Disposables that will be
        /// cleaned up when the View is deactivated.
        /// </param>
        /// <returns>A Disposable that deactivates this registration.</returns>
        public static IDisposable WhenActivated(this IActivatable @this, Func<IEnumerable<IDisposable>> block)
        {
            return @this.WhenActivated(block, null);
        }

        /// <summary>
        /// WhenActivated allows you to register a Func to be called when a
        /// View is Activated.
        /// </summary>
        /// <param name="this">Object that supports activation.</param>
        /// <param name="block">
        /// The method to be called when the corresponding
        /// View is activated. It returns a list of Disposables that will be
        /// cleaned up when the View is deactivated.
        /// </param>
        /// <param name="view">
        /// The IActivatable will ordinarily also host the View
        /// Model, but in the event it is not, a class implementing <see cref="IViewFor" />
        /// can be supplied here.
        /// </param>
        /// <returns>A Disposable that deactivates this registration.</returns>
        public static IDisposable WhenActivated(this IActivatable @this, Func<IEnumerable<IDisposable>> block, IViewFor view)
        {
            var activationFetcher = activationFetcherCache.Get(@this.GetType());
            if (activationFetcher == null)
            {
                const string msg = "Don't know how to detect when {0} is activated/deactivated, you may need to implement IActivationForViewFetcher";
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, msg, @this.GetType().FullName));
            }

            var activationEvents = activationFetcher.GetActivationForView(@this);

            var vmDisposable = Disposable.Empty;
            if ((view ?? @this) is IViewFor v)
            {
                vmDisposable = HandleViewModelActivation(v, activationEvents);
            }

            var viewDisposable = HandleViewActivation(block, activationEvents);
            return new CompositeDisposable(vmDisposable, viewDisposable);
        }

        /// <summary>
        /// WhenActivated allows you to register a Func to be called when a
        /// View is Activated.
        /// </summary>
        /// <param name="this">Object that supports activation.</param>
        /// <param name="block">
        /// The method to be called when the corresponding
        /// View is activated. The Action parameter (usually called 'd') allows
        /// you to register Disposables to be cleaned up when the View is
        /// deactivated (i.e. "d(someObservable.Subscribe());").
        /// </param>
        /// <returns>A Disposable that deactivates this registration.</returns>
        public static IDisposable WhenActivated(this IActivatable @this, Action<Action<IDisposable>> block)
        {
            return @this.WhenActivated(block, null);
        }

        /// <summary>
        /// WhenActivated allows you to register a Func to be called when a
        /// View is Activated.
        /// </summary>
        /// <param name="this">Object that supports activation.</param>
        /// <param name="block">
        /// The method to be called when the corresponding
        /// View is activated. The Action parameter (usually called 'd') allows
        /// you to register Disposables to be cleaned up when the View is
        /// deactivated (i.e. "d(someObservable.Subscribe());").
        /// </param>
        /// <param name="view">
        /// The IActivatable will ordinarily also host the View
        /// Model, but in the event it is not, a class implementing <see cref="IViewFor" />
        /// can be supplied here.
        /// </param>
        /// <returns>A Disposable that deactivates this registration.</returns>
        public static IDisposable WhenActivated(this IActivatable @this, Action<Action<IDisposable>> block, IViewFor view)
        {
            return @this.WhenActivated(
                () =>
            {
                var ret = new List<IDisposable>();
                block(ret.Add);
                return ret;
            }, view);
        }

        /// <summary>
        /// WhenActivated allows you to register a Func to be called when a
        /// View is Activated.
        /// </summary>
        /// <param name="this">Object that supports activation.</param>
        /// <param name="block">
        /// The method to be called when the corresponding
        /// View is activated. The Action parameter (usually called 'disposables') allows
        /// you to collate all disposables that should be cleaned up during deactivation.
        /// </param>
        /// <param name="view">
        /// The IActivatable will ordinarily also host the View
        /// Model, but in the event it is not, a class implementing <see cref="IViewFor" />
        /// can be supplied here.
        /// </param>
        /// <returns>A Disposable that deactivates this registration.</returns>
        public static IDisposable WhenActivated(this IActivatable @this, Action<CompositeDisposable> block, IViewFor view = null)
        {
            return @this.WhenActivated(
                () =>
            {
                var d = new CompositeDisposable();
                block(d);
                return new[] { d };
            }, view);
        }

        private static IDisposable HandleViewActivation(Func<IEnumerable<IDisposable>> block, IObservable<bool> activation)
        {
            var viewDisposable = new SerialDisposable();

            return new CompositeDisposable(
                activation.Subscribe(activated =>
                {
                    // NB: We need to make sure to respect ordering so that the cleanup
                    // happens before we invoke block again
                    viewDisposable.Disposable = Disposable.Empty;
                    if (activated)
                    {
                        viewDisposable.Disposable = new CompositeDisposable(block());
                    }
                }),
                viewDisposable);
        }

        private static IDisposable HandleViewModelActivation(IViewFor view, IObservable<bool> activation)
        {
            var vmDisposable = new SerialDisposable();
            var viewVmDisposable = new SerialDisposable();

            return new CompositeDisposable(
                activation.Subscribe(activated =>
                {
                    if (activated)
                    {
                        viewVmDisposable.Disposable = view.WhenAnyValue(x => x.ViewModel)
                            .Select(x => x as ISupportsActivation)
                            .Subscribe(x =>
                            {
                                // NB: We need to make sure to respect ordering so that the cleanup
                                // happens before we activate again
                                vmDisposable.Disposable = Disposable.Empty;
                                if (x != null)
                                {
                                    vmDisposable.Disposable = x.Activator.Activate();
                                }
                            });
                    }
                    else
                    {
                        viewVmDisposable.Disposable = Disposable.Empty;
                        vmDisposable.Disposable = Disposable.Empty;
                    }
                }),
                vmDisposable,
                viewVmDisposable);
        }
    }
}
