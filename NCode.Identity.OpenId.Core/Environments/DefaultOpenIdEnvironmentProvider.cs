﻿#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdEnvironmentProvider"/> abstraction.
/// </summary>
public class DefaultOpenIdEnvironmentProvider(
    IOpenIdEnvironmentFactory factory
) : IOpenIdEnvironmentProvider, IAsyncDisposable
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
