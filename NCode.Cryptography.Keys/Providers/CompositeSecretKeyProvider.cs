#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Primitives;
using NCode.Cryptography.Keys.Internal;

namespace NCode.Cryptography.Keys.Providers;

/// <summary>
/// Provides an implementation of <see cref="ISecretKeyProvider"/> that aggregates multiple <see cref="ISecretKeyProvider"/> instances.
/// </summary>
public class CompositeSecretKeyProvider : SecretKeyProvider
{
    private object SyncObj { get; } = new();
    private CancellationTokenSource? ChangeTokenSource { get; set; }
    private IChangeToken? ConsumerChangeToken { get; set; }
    private List<IDisposable>? ChangeTokenRegistrations { get; set; }
    private IReadOnlyList<ISecretKeyProvider> Providers { get; }
    private ISecretKeyCollection? Collection { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSecretKeyProvider"/> class with the specified collection of <see cref="ISecretKeyProvider"/> instances.
    /// </summary>
    /// <param name="providers">A collection of <see cref="ISecretKeyProvider"/> instances to aggregate.</param>
    public CompositeSecretKeyProvider(IEnumerable<ISecretKeyProvider> providers)
    {
        Providers = providers.ToList();
    }

    /// <inheritdoc />
    public override ISecretKeyCollection SecretKeys
    {
        get
        {
            ThrowIfDisposed();
            EnsureCollectionInitialized();
            return Collection;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (IsDisposed || !disposing) return;

        List<IDisposable> disposables = new();

        lock (SyncObj)
        {
            if (IsDisposed) return;
            IsDisposed = true;

            disposables.AddRange(Providers);

            if (ChangeTokenRegistrations is { Count: > 0 })
                disposables.AddRange(ChangeTokenRegistrations);

            if (ChangeTokenSource is not null)
                disposables.Add(ChangeTokenSource);

            // we don't dispose the collection because the secret keys are managed by the composite providers
        }

        disposables.DisposeAll();
    }

    /// <inheritdoc />
    public override IChangeToken GetChangeToken()
    {
        ThrowIfDisposed();
        EnsureChangeTokenInitialized();
        return ConsumerChangeToken;
    }

    [MemberNotNull(nameof(Collection))]
    private void EnsureCollectionInitialized()
    {
        if (Collection is not null) return;
        lock (SyncObj)
        {
            if (Collection is not null) return;

            EnsureChangeTokenInitialized();
            CreateCollection();
        }
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void EnsureChangeTokenInitialized()
    {
        if (ConsumerChangeToken is not null) return;
        lock (SyncObj)
        {
            if (ConsumerChangeToken is not null) return;

            SubscribeChangeTokenProducers();
            CreateConsumerChangeToken();
        }
    }

    private void HandleChange()
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
                CreateConsumerChangeToken();
            }

            // refresh the cached collection
            if (Collection is not null)
            {
                CreateCollection();
            }
        }

        oldTokenSource?.Cancel();
        oldTokenSource?.Dispose();
    }

    private void SubscribeChangeTokenProducers()
    {
        if (IsDisposed) return;
        ChangeTokenRegistrations ??= new List<IDisposable>();
        ChangeTokenRegistrations.AddRange(Providers.Select(provider =>
            ChangeToken.OnChange(
                provider.GetChangeToken,
                HandleChange)));
    }

    [MemberNotNull(nameof(ConsumerChangeToken))]
    private void CreateConsumerChangeToken()
    {
        var tokenSource = new CancellationTokenSource();
        ChangeTokenSource = tokenSource;
        ConsumerChangeToken = new CancellationChangeToken(tokenSource.Token);
    }

    [MemberNotNull(nameof(Collection))]
    private void CreateCollection()
    {
        Collection = Providers.Count == 1 ?
            Providers[0].SecretKeys :
            new SecretKeyCollection(
                Providers.SelectMany(provider =>
                    provider.SecretKeys),
                owns: false);
    }
}
