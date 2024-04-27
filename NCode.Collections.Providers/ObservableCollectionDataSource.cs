#region Copyright Preamble

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
using Microsoft.Extensions.Primitives;
using NCode.Disposables;

namespace NCode.Collections.Providers;

/// <summary>
/// Provides an implementation of <see cref="ICollectionDataSource{T}"/> that uses an <see cref="ObservableCollection{T}"/> as the underlying data source.
/// </summary>
/// <typeparam name="T">The type of items in the collection.</typeparam>
public sealed class ObservableCollectionDataSource<T> : ICollectionDataSource<T>, IDisposable
{
    private bool Owns { get; }
    private object SyncObj { get; } = new();
    private bool IsDisposed { get; set; }
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private CancellationChangeToken? ChangeToken { get; set; }
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
        return ChangeToken;
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
            ChangeToken = null;
        }

        disposables.DisposeAll();
    }

    [MemberNotNull(nameof(ChangeToken))]
    private void EnsureChangeTokenInitialized()
    {
        if (ChangeToken is not null) return;
        lock (SyncObj)
        {
            if (ChangeToken is not null) return;

            ThrowIfDisposed();
            RefreshChangeToken();
        }
    }

    private void HandleChange(object? sender, NotifyCollectionChangedEventArgs e)
    {
        CancellationTokenSource? oldTokenSource;

        if (IsDisposed) return;
        lock (SyncObj)
        {
            if (IsDisposed) return;

            oldTokenSource = ChangeTokenSource;

            // refresh the cached change token
            if (oldTokenSource is not null)
            {
                RefreshChangeToken();
            }
        }

        oldTokenSource?.Cancel();
        oldTokenSource?.Dispose();
    }

    [MemberNotNull(nameof(ChangeToken))]
    private void RefreshChangeToken()
    {
        ChangeTokenSource = new CancellationTokenSource();
        ChangeToken = new CancellationChangeToken(ChangeTokenSource.Token);
    }
}
