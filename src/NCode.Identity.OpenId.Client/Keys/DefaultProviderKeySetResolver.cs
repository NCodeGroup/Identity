#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Client.Caching;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Logging;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.Secrets.Collections;

namespace NCode.Identity.OpenId.Client.Keys;

/// <summary>
/// Provides a default implementation of <see cref="IProviderKeySetResolver"/> that fetches, imports and caches the
/// JSON Web Key Set published at the <c>jwks_uri</c> of an authority.
/// </summary>
internal class DefaultProviderKeySetResolver(
    ILogger<DefaultProviderKeySetResolver> logger,
    TimeProvider timeProvider,
    IOptions<OpenIdClientOptions> options,
    ProviderHttpClient providerHttpClient,
    IProviderMetadataResolver metadataResolver,
    ProviderKeySetImporter importer,
    ISecretKeyCollectionFactory secretKeyCollectionFactory
) : IProviderKeySetResolver
{
    private ILogger<DefaultProviderKeySetResolver> Logger { get; } = logger;

    private OpenIdClientOptions Options { get; } = options.Value;

    private ProviderHttpClient ProviderHttpClient { get; } = providerHttpClient;

    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;

    private ProviderKeySetImporter Importer { get; } = importer;

    private ISecretKeyCollectionFactory SecretKeyCollectionFactory { get; } =
        secretKeyCollectionFactory;

    private RefreshingCache<ISecretKeyCollection> Cache { get; } = new(timeProvider);

    /// <inheritdoc />
    public async ValueTask<ISecretKeyCollection> GetSigningKeysAsync(
        string authority,
        CancellationToken cancellationToken
    )
    {
        var jwksUri = await GetJwksUriAsync(authority, cancellationToken);

        return await Cache.GetAsync(
            jwksUri,
            CreatePolicy(),
            token => FetchAsync(jwksUri, token),
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<ISecretKeyCollection> RefreshSigningKeysAsync(
        string authority,
        CancellationToken cancellationToken
    )
    {
        var jwksUri = await GetJwksUriAsync(authority, cancellationToken);

        return await Cache.RefreshAsync(
            jwksUri,
            CreatePolicy(),
            token => FetchAsync(jwksUri, token),
            cancellationToken
        );
    }

    private RefreshingCachePolicy CreatePolicy() =>
        new(Options.MetadataRefreshInterval, Options.MinimumRefreshInterval, Options.StaleLifetime);

    private async ValueTask<string> GetJwksUriAsync(
        string authority,
        CancellationToken cancellationToken
    )
    {
        var metadata = await MetadataResolver.GetMetadataAsync(authority, cancellationToken);
        return metadata.JwksUri
            ?? throw new OpenIdProviderException(
                "The discovery document of the provider lacks the jwks_uri."
            );
    }

    private async ValueTask<ISecretKeyCollection> FetchAsync(
        string jwksUri,
        CancellationToken cancellationToken
    )
    {
        if (!ProviderUri.TryParseEndpoint(jwksUri, out _))
            throw Fail(jwksUri, "The jwks_uri is not an https URL (or http on a loopback host).");

        using var request = new HttpRequestMessage(HttpMethod.Get, jwksUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/jwk-set+json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        ProviderHttpResponse response;
        try
        {
            response = await ProviderHttpClient.SendAsync(request, cancellationToken);
        }
        catch (OpenIdProviderException exception)
        {
            Logger.ProviderKeySetFetchFailed(jwksUri, exception.Message, exception);
            throw;
        }

        if (response.StatusCode != StatusCodes.Status200OK)
            throw Fail(jwksUri, $"The jwks_uri returned status {response.StatusCode}.");

        if (response.Json is not { ValueKind: JsonValueKind.Object } json)
            throw Fail(jwksUri, "The jwks_uri did not return a JSON object.");

        if (!Importer.TryImport(json, out var keys, out var error))
            throw Fail(jwksUri, error);

        return SecretKeyCollectionFactory.Create(keys);
    }

    private OpenIdProviderException Fail(string jwksUri, string reason)
    {
        Logger.ProviderKeySetFetchFailed(jwksUri, reason, null);
        return new OpenIdProviderException(reason);
    }
}
