// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides a factory abstraction for creating instances of <see cref="OpenIdClient"/> and <see cref="OpenIdConfidentialClient"/>.
/// </summary>
[PublicAPI]
public interface IOpenIdClientFactory
{
    /// <summary>
    /// Factory method that creates a new instance of <see cref="OpenIdClient"/> that represents a public client.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="definition">The <see cref="OpenIdClientDefinition"/> that contains the client's resolved
    /// registration.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation,
    /// containing the <see cref="OpenIdClient"/> that represents a public client.</returns>
    ValueTask<OpenIdClient> CreatePublicClientAsync(
        OpenIdContext openIdContext,
        OpenIdClientDefinition definition,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Factory method that creates a new instance of <see cref="OpenIdConfidentialClient"/> that represents a confidential client.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="publicClient">The <see cref="OpenIdClient"/> that represents the public client.</param>
    /// <param name="method">The <see cref="string"/> that contains the client authentication method.</param>
    /// <param name="credentialId">The identifier of the registered credential the client authenticated with, or
    /// <c>null</c> when it has none.</param>
    /// <param name="confirmation">The <see cref="JsonElement"/> that contains the client confirmation, if any.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation,
    /// containing the <see cref="OpenIdConfidentialClient"/> that represents a confidential client.</returns>
    ValueTask<OpenIdConfidentialClient> CreateConfidentialClientAsync(
        OpenIdContext openIdContext,
        OpenIdClient publicClient,
        string method,
        string? credentialId,
        JsonElement? confirmation,
        CancellationToken cancellationToken
    );
}
