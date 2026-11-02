// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Client.Http;

namespace NCode.Identity.OpenId.Client.Tokens;

/// <summary>
/// Provides a default implementation of <see cref="ITokenRevocationClient"/>.
/// </summary>
internal class DefaultTokenRevocationClient(
    IProviderMetadataResolver metadataResolver,
    ClientRequestSender sender
) : ITokenRevocationClient
{
    private IProviderMetadataResolver MetadataResolver { get; } = metadataResolver;

    private ClientRequestSender Sender { get; } = sender;

    /// <inheritdoc />
    public async ValueTask<ProtocolError?> RevokeTokenAsync(
        ClientRegistration registration,
        TokenRevocationRequest request,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(request);

        var metadata = await MetadataResolver.GetMetadataAsync(
            registration.Authority,
            cancellationToken
        );

        var revocationEndpoint = metadata.RevocationEndpoint;
        if (string.IsNullOrEmpty(revocationEndpoint))
        {
            throw new InvalidOperationException(
                $"The OpenID Provider '{registration.Authority}' does not advertise a revocation endpoint."
            );
        }

        var parameters = new List<KeyValuePair<string, string>>
        {
            new(OpenIdProtocol.Parameters.Token, request.Token),
        };

        if (!string.IsNullOrEmpty(request.TokenTypeHint))
            parameters.Add(
                new KeyValuePair<string, string>(
                    OpenIdProtocol.Parameters.TokenTypeHint,
                    request.TokenTypeHint
                )
            );

        var response = await Sender.PostFormAsync(
            registration,
            metadata,
            new ProviderEndpoint(
                OpenIdProtocol.MetadataNames.RevocationEndpoint,
                revocationEndpoint
            ),
            parameters,
            cancellationToken
        );

        if (response.StatusCode == ProviderStatusCodes.Status200OK)
            return null;

        if (ProtocolErrorReader.TryRead(response, out var error))
            return error;

        throw new OpenIdProviderException(
            $"The revocation endpoint answered HTTP {response.StatusCode} without an OAuth error."
        );
    }
}
