// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Authentication;

/// <summary>
/// Provides an implementation of <see cref="IClientAuthenticator"/> for the <c>client_secret_post</c> method, which sends
/// the client's credentials as form parameters.
/// </summary>
internal class ClientSecretPostClientAuthenticator(IClientKeyRingProvider keyRingProvider)
    : IClientAuthenticator
{
    private IClientKeyRingProvider KeyRingProvider { get; } = keyRingProvider;

    /// <inheritdoc />
    public string MethodName => OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost;

    /// <inheritdoc />
    public bool UsesMutualTls => false;

    /// <inheritdoc />
    public ValueTask AuthenticateAsync(
        ClientAuthenticationContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(context);

        var registration = context.Registration;
        var secret = SharedSecretReader.ReadSecret(KeyRingProvider, registration);

        context.Parameters.Add(
            new KeyValuePair<string, string>(
                OpenIdProtocol.Parameters.ClientId,
                registration.ClientId
            )
        );
        context.Parameters.Add(
            new KeyValuePair<string, string>(OpenIdProtocol.Parameters.ClientSecret, secret)
        );
        return ValueTask.CompletedTask;
    }
}
