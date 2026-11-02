// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEnvironmentProvider"/> abstraction.
/// </summary>
internal class DefaultOpenIdEnvironmentProvider(IOpenIdEnvironmentFactory factory)
    : IOpenIdEnvironmentProvider,
        IAsyncDisposable
{
    private IOpenIdEnvironmentFactory Factory { get; } = factory;
    private OpenIdEnvironment? InstanceOrNull { get; set; }
    private Lock SyncRoot { get; } = new();

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

        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public OpenIdEnvironment Get()
    {
        // ReSharper disable once InvertIf
        if (InstanceOrNull is null)
        {
            lock (SyncRoot)
            {
                InstanceOrNull ??= Factory.Create();
            }
        }

        return InstanceOrNull;
    }
}
