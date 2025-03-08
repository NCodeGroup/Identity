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

using System.Diagnostics.CodeAnalysis;
using NCode.Identity.OpenId.Environments;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdServerProvider"/> abstraction.
/// </summary>
public class DefaultOpenIdServerProvider(
    IOpenIdServerFactory factory
) : IOpenIdServerProvider, IAsyncDisposable
{
    private IOpenIdServerFactory Factory { get; } = factory;
    private OpenIdServer? InstanceOrNull { get; set; }
    private SemaphoreSlim SyncRoot { get; } = new(initialCount: 0, maxCount: 1);

    /// <inheritdoc />
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    public async ValueTask DisposeAsync()
    {
        switch (InstanceOrNull)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;

            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        SyncRoot.Dispose();

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask<OpenIdServer> GetAsync(
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    )
    {
        // ReSharper disable once InvertIf
        if (InstanceOrNull is null)
        {
            await SyncRoot.WaitAsync(cancellationToken);
            try
            {
                InstanceOrNull ??= await Factory.CreateAsync(openIdEnvironment, cancellationToken);
            }
            finally
            {
                SyncRoot.Release();
            }
        }

        return InstanceOrNull;
    }
}
