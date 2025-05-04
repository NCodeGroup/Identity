#region Copyright Preamble

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
using JetBrains.Annotations;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Endpoints.Api.Secrets;

/// <summary>
/// Represents the REST resource for a <see cref="PersistedSecret"/> instance.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public class SecretResource
{
    /// <inheritdoc cref="PersistedSecret.SecretId"/>
    public required string SecretId { get; init; }

    /// <inheritdoc cref="PersistedSecret.ConcurrencyToken"/>
    public required string ConcurrencyToken { get; init; }

    /// <inheritdoc cref="PersistedSecret.Use"/>
    public required string? Use { get; init; }

    /// <inheritdoc cref="PersistedSecret.Algorithm"/>
    public required string? Algorithm { get; init; }

    /// <inheritdoc cref="PersistedSecret.CreatedWhen"/>
    public required DateTimeOffset CreatedWhen { get; init; }

    /// <inheritdoc cref="PersistedSecret.ExpiresWhen"/>
    public required DateTimeOffset ExpiresWhen { get; init; }

    /// <inheritdoc cref="PersistedSecret.SecretType"/>
    public required string SecretType { get; init; }

    /// <inheritdoc cref="PersistedSecret.KeySizeBits"/>
    public required int KeySizeBits { get; init; }
}
