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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Client.Caching;
using NCode.Identity.OpenId.Client.Http;
using NCode.Identity.OpenId.Client.Logging;
using NCode.Identity.OpenId.Client.Options;

namespace NCode.Identity.OpenId.Client.Discovery;

/// <summary>
/// Provides a default implementation of <see cref="IProviderMetadataResolver"/> that fetches, validates and caches
/// the discovery document of an authority.
/// </summary>
internal class DefaultProviderMetadataResolver(
    ILogger<DefaultProviderMetadataResolver> logger,
    TimeProvider timeProvider,
    IOptions<OpenIdClientOptions> options,
    ProviderHttpClient providerHttpClient
) : IProviderMetadataResolver
{
    private ILogger<DefaultProviderMetadataResolver> Logger { get; } = logger;

    private OpenIdClientOptions Options { get; } = options.Value;

    private ProviderHttpClient ProviderHttpClient { get; } = providerHttpClient;

    private RefreshingCache<OpenIdProviderMetadata> Cache { get; } = new(timeProvider);

    /// <inheritdoc />
    public async ValueTask<OpenIdProviderMetadata> GetMetadataAsync(
        string authority,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(authority);

        if (!ProviderUri.TryParseEndpoint(authority, out _) || authority.Contains('?'))
            throw new ArgumentException(
                "The authority must be an absolute https URL (or http on a loopback host) without a query.",
                nameof(authority)
            );

        var policy = new RefreshingCachePolicy(
            Options.MetadataRefreshInterval,
            Options.MinimumRefreshInterval,
            Options.StaleLifetime
        );

        return await Cache.GetAsync(
            authority,
            policy,
            token => FetchAsync(authority, token),
            cancellationToken
        );
    }

    private async ValueTask<OpenIdProviderMetadata> FetchAsync(
        string authority,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            authority.TrimEnd('/') + OpenIdProtocol.WellKnownPaths.OpenIdConfiguration
        );
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue(OpenIdProtocol.MediaTypes.Json)
        );

        ProviderHttpResponse response;
        try
        {
            response = await ProviderHttpClient.SendAsync(request, cancellationToken);
        }
        catch (OpenIdProviderException exception)
        {
            Logger.ProviderMetadataFetchFailed(authority, exception.Message, exception);
            throw;
        }

        if (response.StatusCode != ProviderStatusCodes.Status200OK)
            throw Fail(authority, $"The discovery endpoint returned status {response.StatusCode}.");

        if (response.Json is not { ValueKind: JsonValueKind.Object } json)
            throw Fail(authority, "The discovery endpoint did not return a JSON object.");

        OpenIdProviderMetadata? metadata;
        try
        {
            // A JSON object never deserializes to null, so the null case is a defensive guard only.
            metadata =
                json.Deserialize<OpenIdProviderMetadata>()
                ?? throw Fail(authority, "The response is not a valid discovery document.");
        }
        catch (JsonException exception)
        {
            throw Fail(authority, "The response is not a valid discovery document.", exception);
        }

        if (!string.Equals(metadata.Issuer, authority, StringComparison.Ordinal))
            throw Fail(
                authority,
                "The issuer of the discovery document does not match the authority."
            );

        if (string.IsNullOrEmpty(metadata.TokenEndpoint) || string.IsNullOrEmpty(metadata.JwksUri))
            throw Fail(
                authority,
                "The discovery document lacks the token_endpoint or the jwks_uri."
            );

        foreach (
            var (name, value) in new (string, string?)[]
            {
                (
                    OpenIdProtocol.MetadataNames.AuthorizationEndpoint,
                    metadata.AuthorizationEndpoint
                ),
                (OpenIdProtocol.MetadataNames.TokenEndpoint, metadata.TokenEndpoint),
                (OpenIdProtocol.MetadataNames.UserInfoEndpoint, metadata.UserInfoEndpoint),
                (OpenIdProtocol.MetadataNames.JwksUri, metadata.JwksUri),
                (OpenIdProtocol.MetadataNames.EndSessionEndpoint, metadata.EndSessionEndpoint),
                (OpenIdProtocol.MetadataNames.RevocationEndpoint, metadata.RevocationEndpoint),
                (
                    OpenIdProtocol.MetadataNames.PushedAuthorizationRequestEndpoint,
                    metadata.PushedAuthorizationRequestEndpoint
                ),
            }
        )
        {
            if (value is not null && !ProviderUri.TryParseEndpoint(value, out _))
                throw Fail(
                    authority,
                    $"The {name} of the discovery document is not an https URL (or http on a loopback host)."
                );
        }

        if (metadata.MtlsEndpointAliases is not null)
        {
            foreach (var (name, value) in metadata.MtlsEndpointAliases)
            {
                if (!ProviderUri.TryParseEndpoint(value, out _))
                    throw Fail(
                        authority,
                        $"The {OpenIdProtocol.MetadataNames.MtlsEndpointAliases} entry '{name}' of the discovery document is not an https URL (or http on a loopback host)."
                    );
            }
        }

        return metadata;
    }

    private OpenIdProviderException Fail(
        string authority,
        string reason,
        Exception? innerException = null
    )
    {
        Logger.ProviderMetadataFetchFailed(authority, reason, innerException);
        return innerException is null
            ? new OpenIdProviderException(reason)
            : new OpenIdProviderException(reason, innerException);
    }
}
