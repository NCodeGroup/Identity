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

using System.Diagnostics;
using System.Security.Cryptography;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using NCode.CryptoMemory;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence.Logic;

namespace NCode.Identity.OpenId.Clients.Handlers;

/// <summary>
/// Provides a common base for <see cref="IClientAuthenticationHandler"/> implementations.
/// </summary>
[PublicAPI]
public abstract class CommonClientAuthenticationHandler(
    IOpenIdErrorFactory errorFactory,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdClientFactory clientFactory,
    ISettingSerializer settingSerializer,
    ISecretSerializer secretSerializer
) : IClientAuthenticationHandler
{
    /// <summary>
    /// Gets the <see cref="IStoreManagerFactory"/> instance.
    /// </summary>
    protected IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    /// <summary>
    /// Gets the <see cref="IOpenIdClientFactory"/> instance.
    /// </summary>
    protected IOpenIdClientFactory ClientFactory { get; } = clientFactory;

    /// <summary>
    /// Gets the <see cref="ISettingSerializer"/> instance.
    /// </summary>
    protected ISettingSerializer SettingSerializer { get; } = settingSerializer;

    /// <summary>
    /// Gets the <see cref="ISecretSerializer"/> instance.
    /// </summary>
    protected ISecretSerializer SecretSerializer { get; } = secretSerializer;

    /// <summary>
    /// Gets the error for an invalid client.
    /// </summary>
    protected IOpenIdError ErrorInvalidClient { get; } = errorFactory
        .InvalidClient()
        .WithStatusCode(StatusCodes.Status400BadRequest);

    /// <inheritdoc />
    public abstract string AuthenticationMethod { get; }

    /// <inheritdoc />
    public abstract ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken);

    /// <summary>
    /// Authenticates the client using the specified <paramref name="clientId"/> and <paramref name="clientSecret"/>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="clientId">Contains the client identifier.</param>
    /// <param name="clientSecret">Contains the client secret.</param>
    /// <param name="hasClientSecret">Indicates whether the client secret was specified.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ClientAuthenticationResult"/>.</returns>
    protected async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdContext openIdContext,
        string clientId,
        ReadOnlyMemory<char> clientSecret,
        bool hasClientSecret,
        CancellationToken cancellationToken)
    {
        var tenantId = openIdContext.Tenant.TenantId;

        var persistedClient = await TryGetPersistedClientAsync(tenantId, clientId, cancellationToken);
        if (persistedClient is null || persistedClient.IsDisabled)
            return new ClientAuthenticationResult(ErrorInvalidClient);

        var publicClient = await CreatePublicClientAsync(openIdContext, persistedClient, cancellationToken);

        if (!hasClientSecret)
            return new ClientAuthenticationResult(publicClient);

        var clientSecretChars = clientSecret.Span;
        var byteCount = SecureEncoding.UTF8.GetByteCount(clientSecretChars);
        using var _ = CryptoPool.Rent(byteCount, isSensitive: true, out Memory<byte> clientSecretBytes);

        var decodeResult = SecureEncoding.UTF8.TryGetBytes(clientSecretChars, clientSecretBytes.Span, out var bytesWritten);
        Debug.Assert(decodeResult && bytesWritten == byteCount);

        return await AuthenticateClientAsync(
            publicClient,
            clientSecretBytes,
            cancellationToken);
    }

    /// <summary>
    /// Authenticates the client using the specified <paramref name="publicClient"/> and <paramref name="clientSecretBytes"/>.
    /// </summary>
    /// <param name="publicClient">The <see cref="OpenIdClient"/> that represents the client application.</param>
    /// <param name="clientSecretBytes">Contains the client secret as a byte buffer.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="ClientAuthenticationResult"/>.</returns>
    protected async ValueTask<ClientAuthenticationResult> AuthenticateClientAsync(
        OpenIdClient publicClient,
        ReadOnlyMemory<byte> clientSecretBytes,
        CancellationToken cancellationToken)
    {
        foreach (var secretKey in publicClient.SecretKeys.OfType<SymmetricSecretKey>())
        {
            if (!IsSecretEqual(secretKey, clientSecretBytes.Span))
                continue;

            var confidentialClient = await ClientFactory.CreateConfidentialClientAsync(
                publicClient,
                AuthenticationMethod,
                secretKey,
                confirmation: null,
                cancellationToken);

            return new ClientAuthenticationResult(confidentialClient);
        }

        // client secret was specified but failed to verify
        return new ClientAuthenticationResult(ErrorInvalidClient);
    }

    /// <summary>
    /// Attempts to get the persisted client using the specified <paramref name="tenantId"/> and <paramref name="clientId"/>.
    /// </summary>
    /// <param name="tenantId">The tenant identifier for the <see cref="PersistedClient"/> instance.</param>
    /// <param name="clientId">The natural key of the <see cref="PersistedClient"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedClient"/> instance matching the specified <paramref name="clientId"/> if it exists.</returns>
    protected async ValueTask<PersistedClient?> TryGetPersistedClientAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken)
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var persistedClient = await store.TryGetByClientIdAsync(tenantId, clientId, cancellationToken);
        return persistedClient;
    }

    /// <summary>
    /// Creates a public client using the specified <paramref name="openIdContext"/> and <paramref name="persistedClient"/>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> associated with the current HTTP request.</param>
    /// <param name="persistedClient">The <see cref="PersistedClient"/> instance to create the client from.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="OpenIdClient"/> instance.</returns>
    protected async ValueTask<OpenIdClient> CreatePublicClientAsync(
        OpenIdContext openIdContext,
        PersistedClient persistedClient,
        CancellationToken cancellationToken)
    {
        var clientSettings = SettingSerializer.DeserializeSettings(persistedClient.SettingsJson);
        var parentSettings = openIdContext.Tenant.SettingsProvider.Collection;
        var effectiveSettings = parentSettings.Merge(clientSettings);

        var secrets = SecretSerializer.DeserializeSecrets(
            persistedClient.Secrets,
            out _);

        var publicClient = await ClientFactory.CreatePublicClientAsync(
            openIdContext,
            persistedClient.ClientId,
            effectiveSettings,
            secrets,
            persistedClient.RedirectUrls,
            cancellationToken);

        return publicClient;
    }

    /// <summary>
    /// Determines whether the specified symmetric <paramref name="secretKey"/> is equal to the specified <paramref name="clientSecretBytes"/>.
    /// </summary>
    /// <param name="secretKey">The <see cref="SymmetricSecretKey"/> instance to compare.</param>
    /// <param name="clientSecretBytes">The client secret byte array to compare.</param>
    /// <returns><c>true</c> if the specified symmetric <paramref name="secretKey"/> is equal to the specified <paramref name="clientSecretBytes"/>; otherwise, <c>false</c>.</returns>
    protected static bool IsSecretEqual(SymmetricSecretKey secretKey, ReadOnlySpan<byte> clientSecretBytes)
    {
        using var _ = CryptoPool.Rent(
            secretKey.KeySizeBytes,
            isSensitive: true,
            out Span<byte> secretKeyBytes);

        var exportResult = secretKey.TryExportPrivateKey(secretKeyBytes, out var exportBytesWritten);
        Debug.Assert(exportResult && exportBytesWritten == secretKey.KeySizeBytes);

        return CryptographicOperations.FixedTimeEquals(secretKeyBytes, clientSecretBytes);
    }
}
