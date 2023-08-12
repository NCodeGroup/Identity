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

using Microsoft.Extensions.Primitives;
using NCode.Cryptography.Keys.DataSources;
using NCode.Cryptography.Keys.Internal;

namespace NCode.Cryptography.Keys;

/// <summary>
/// Provides the root (i.e. top-level) collection of <see cref="SecretKey"/> instances by aggregating multiple <see cref="ISecretKeyDataSource"/> instances.
/// </summary>
public interface ISecretKeyProvider : IDisposable
{
    /// <summary>
    /// Gets a read-only collection of <see cref="SecretKey"/> instances.
    /// </summary>
    ISecretKeyCollection SecretKeys { get; }

    /// <summary>
    /// Gets a <see cref="IChangeToken"/> that provides notifications when changes occur.
    /// </summary>
    IChangeToken GetChangeToken();
}

/// <summary>
/// Provides a default implementation for <see cref="ISecretKeyProvider"/>.
/// </summary>
public class SecretKeyProvider : BaseDisposable, ISecretKeyProvider
{
    private object SyncObj { get; } = new();
    private CompositeSecretKeyDataSource DataSource { get; }
    private CancellationTokenSource ChangeTokenSource { get; set; }
    private CancellationChangeToken ConsumerChangeToken { get; set; }
    private IDisposable ProviderRegistration { get; }

    /// <inheritdoc />
    public ISecretKeyCollection SecretKeys { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SecretKeyProvider"/> class with the specified collection of <see cref="ISecretKeyDataSource"/> instances.
    /// </summary>
    /// <param name="dataSources">A collection of <see cref="ISecretKeyDataSource"/> instances to aggregate.</param>
    public SecretKeyProvider(IEnumerable<ISecretKeyDataSource> dataSources)
    {
        DataSource = new CompositeSecretKeyDataSource(dataSources);
        ChangeTokenSource = new CancellationTokenSource();
        ConsumerChangeToken = new CancellationChangeToken(ChangeTokenSource.Token);
        SecretKeys = new SecretKeyCollection(DataSource.SecretKeys, owns: false);
        ProviderRegistration = ChangeToken.OnChange(DataSource.GetChangeToken, HandleChange);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (!disposing || IsDisposed) return;
        lock (SyncObj)
        {
            if (IsDisposed) return;
            IsDisposed = true;
        }

        ProviderRegistration.Dispose();
        SecretKeys.Dispose();
        ChangeTokenSource.Dispose();
        DataSource.Dispose();
    }

    /// <inheritdoc />
    public IChangeToken GetChangeToken()
    {
        lock (SyncObj)
        {
            ThrowIfDisposed();
            return ConsumerChangeToken;
        }
    }

    private void HandleChange()
    {
        if (IsDisposed) return;
        lock (SyncObj)
        {
            if (IsDisposed) return;

            var oldTokenSource = ChangeTokenSource;
            ChangeTokenSource = new CancellationTokenSource();
            ConsumerChangeToken = new CancellationChangeToken(ChangeTokenSource.Token);
            SecretKeys = new SecretKeyCollection(DataSource.SecretKeys, owns: false);

            oldTokenSource.Cancel();
            oldTokenSource.Dispose();
        }
    }
}
