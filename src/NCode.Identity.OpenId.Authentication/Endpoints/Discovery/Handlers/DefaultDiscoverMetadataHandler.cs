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

using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Handlers;

/// <summary>
/// Provides a default implementation of a handler for the <see cref="DiscoverMetadataCommand"/> message.
/// </summary>
internal class DefaultDiscoverMetadataHandler(
    EndpointDataSource endpointDataSource,
    LinkGenerator linkGenerator
) : ICommandHandler<DiscoverMetadataCommand>, ISupportMediatorPriority
{
    private const string AuthMethodsSupportedSuffix = "_auth_methods_supported";
    private const string AuthSigningAlgValuesSupportedSuffix = "_auth_signing_alg_values_supported";

    // RFC 8414 §2 metadata names the authentication of these endpoints after the endpoint itself.
    private static string[] ClientAuthenticatingEndpointNames { get; } =
    [OpenIdConstants.EndpointNames.Introspection, OpenIdConstants.EndpointNames.Revocation];

    private EndpointDataSource EndpointDataSource { get; } = endpointDataSource;
    private LinkGenerator LinkGenerator { get; } = linkGenerator;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public ValueTask HandleAsync(
        DiscoverMetadataCommand command,
        CancellationToken cancellationToken
    )
    {
        var (openIdContext, metadata) = command;

        DiscoverSettings(openIdContext, metadata);

        DiscoverEndpoints(openIdContext, metadata);

        DiscoverEndpointAuthentication(metadata);

        return ValueTask.CompletedTask;
    }

    // The introspection and revocation endpoints authenticate clients through the same service as the token endpoint,
    // so they accept exactly its methods and assertion signing algorithms (RFC 8414 §2).
    private static void DiscoverEndpointAuthentication(IDictionary<string, object> metadata)
    {
        metadata.TryGetValue(OpenIdSettingNames.TokenEndpointAuthMethodsSupported, out var methods);
        metadata.TryGetValue(
            OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported,
            out var algorithms
        );

        foreach (var endpointName in ClientAuthenticatingEndpointNames)
        {
            if (!metadata.ContainsKey(endpointName))
                continue;

            if (methods is not null)
                metadata.TryAdd(endpointName + AuthMethodsSupportedSuffix, methods);

            if (algorithms is not null)
                metadata.TryAdd(endpointName + AuthSigningAlgValuesSupportedSuffix, algorithms);
        }
    }

    private static void DiscoverSettings(
        OpenIdContext openIdContext,
        IDictionary<string, object> metadata
    )
    {
        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        var settingsToShow = settings.Where(setting => setting.Descriptor.IsDiscoverable);
        var settingsToAdd = settingsToShow.Where(setting =>
            !metadata.ContainsKey(setting.Descriptor.Name)
        );
        foreach (var setting in settingsToAdd)
        {
            var value = setting.Descriptor.Format(setting);
            metadata[setting.Descriptor.Name] = value;
        }
    }

    private void DiscoverEndpoints(
        OpenIdContext openIdContext,
        IDictionary<string, object> metadata
    )
    {
        var httpContext = openIdContext.Http;
        var routeValues = new { };

        // RFC 8705 §5: when the tenant has a mutual-TLS alias authority, discoverable endpoints are mirrored under an
        // mtls_endpoint_aliases object with their authority rewritten, so mTLS clients target the separate host.
        var mtlsAuthority = openIdContext.Tenant.MtlsEndpointAliasAuthority;
        var mtlsAliases = string.IsNullOrEmpty(mtlsAuthority)
            ? null
            : new Dictionary<string, object>(StringComparer.Ordinal);

        foreach (var endpoint in EndpointDataSource.Endpoints)
        {
            var discoverable =
                endpoint.Metadata.GetMetadata<IOpenIdEndpointDiscoverableMetadata>()?.IsDiscoverable
                ?? false;
            if (!discoverable)
                continue;

            var suppressLinkGeneration =
                endpoint
                    .Metadata.GetMetadata<ISuppressLinkGenerationMetadata>()
                    ?.SuppressLinkGeneration
                ?? false;
            if (suppressLinkGeneration)
                continue;

            var endpointName = endpoint.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName;
            if (string.IsNullOrEmpty(endpointName))
                continue;

            var endpointUrl = LinkGenerator.GetTenantUriByName(
                openIdContext,
                endpointName,
                routeValues
            );
            if (string.IsNullOrEmpty(endpointUrl))
                continue;

            metadata.TryAdd(endpointName, endpointUrl);

            if (
                mtlsAliases is not null
                && !string.IsNullOrEmpty(mtlsAuthority)
                && TryRewriteAuthority(endpointUrl, mtlsAuthority) is { } aliasUrl
            )
                mtlsAliases[endpointName] = aliasUrl;
        }

        if (mtlsAliases is { Count: > 0 })
            metadata.TryAdd(OpenIdProtocol.MetadataNames.MtlsEndpointAliases, mtlsAliases);
    }

    private static string? TryRewriteAuthority(string endpointUrl, string authority)
    {
        if (
            !AbsoluteUri.TryParse(endpointUrl, out var endpointUri)
            || !AbsoluteUri.TryParse(authority, out var authorityUri)
        )
            return null;

        var builder = new UriBuilder(endpointUri)
        {
            Scheme = authorityUri.Scheme,
            Host = authorityUri.Host,
            Port = authorityUri.IsDefaultPort ? -1 : authorityUri.Port,
        };
        return builder.Uri.AbsoluteUri;
    }
}
