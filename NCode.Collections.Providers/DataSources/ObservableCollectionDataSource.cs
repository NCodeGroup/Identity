﻿#region Copyright Preamble

// Copyright @ 2024 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Disposables;

namespace NCode.Collections.Providers.DataSources;

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> that uses an <see cref="ObservableCollection{T}"/> as the underlying data source.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
[PublicAPI]
public sealed class ObservableCollectionDataSource<T> : IDisposableCollectionDataSource<T>
{
    private bool Owns { get; }
    private object SyncObj { get; } = new();
    private bool IsDisposed { get; set; }
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private CancellationChangeToken? ConsumerChangeToken { get; set; }
    private ObservableCollection<T> ObservableCollection { get; }

    /// <inheritdoc />
    public IEnumerable<T> Collection
    {
        get
        {
            ThrowIfDisposed();
            return ObservableCollection;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ObservableCollectionDataSource{T}"/> class with the specified <paramref name="observableCollection"/>.
    /// </summary>
    /// <param name="observableCollection">The collection of <typeparamref name="T"/> items.</param>
    /// <param name="owns">Indicates whether this collection will own the items and dispose of them
    /// when this class is disposed. The default is <c>true</c>.</param>
    public ObservableCollectionDataSource(ObservableCollection<T> observableCollection, bool owns = true)
    {
        Owns = owns;
        ObservableCollection = observableCollection;
        ObservableCollection.CollectionChanged += HandleChange;
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        EnsureChangeTokenInitialized();
        return ConsumerChangeToken;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(IsDisposed, this);

    /// <inheritdoc />
    public void Dispose()
    {
        if (IsDisposed) return;

        List<IDisposable>? disposables;

        lock (SyncObj)
        {
            if (IsDisposed) return;
            IsDisposed = true;

            ObservableCollection.CollectionChanged -= HandleChange;

            disposables = [];

            if (Owns && ObservableCollection is IEnumerable<IDisposable> disposableCollection)
            {
                disposables.AddRange(disposableCollection);
            }

            if (ChangeTokenSource is not null)
            {
                disposables.Add(ChangeTokenSource);
            }

            ChangeTokenSource = null;
            ConsumerChangeToken = null;
        }

        disposables.DisposeAll();
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void EnsureChangeTokenInitialized()
    {
        if (ConsumerChangeToken is not null) return;
        lock (SyncObj)
        {
            if (ConsumerChangeToken is not null) return;

            ThrowIfDisposed();
            RefreshChangeToken();
        }
    }

    private void HandleChange(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (IsDisposed) return;

        CancellationTokenSource? oldTokenSource = null;
        try
        {
            lock (SyncObj)
            {
                if (IsDisposed) return;

                oldTokenSource = ChangeTokenSource;

                ChangeTokenSource = null;
                ConsumerChangeToken = null;

                // refresh the cached change token
                if (oldTokenSource is not null)
                {
                    RefreshChangeToken();
                }
            }

            oldTokenSource?.Cancel();
        }
        finally
        {
            oldTokenSource?.Dispose();
        }
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void RefreshChangeToken()
    {
        ChangeTokenSource = new CancellationTokenSource();
        ConsumerChangeToken = new CancellationChangeToken(ChangeTokenSource.Token);
    }
}
