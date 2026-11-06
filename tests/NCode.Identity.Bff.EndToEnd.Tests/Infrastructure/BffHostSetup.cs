// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Net.Security;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Client;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;

/// <summary>
/// Configures the backend-for-frontend (BFF) that every host of the end-to-end suite serves: its configuration, its
/// key rings and its trust of the OpenID Provider's certificate.
/// </summary>
internal static class BffHostSetup
{
    /// <summary>
    /// The name of the one frontend that the BFF serves.
    /// </summary>
    public const string FrontendName = "web";

    /// <summary>
    /// The configuration prefix of the frontend.
    /// </summary>
    public const string ConfigurationPrefix = $"Bff:Frontends:{FrontendName}";

    /// <summary>
    /// The name of the key ring that holds the client authentication key.
    /// </summary>
    public const string ClientKeyRing = "e2e-client";

    /// <summary>
    /// The name of the key ring that holds the DPoP key.
    /// </summary>
    public const string DPoPKeyRing = "e2e-dpop";

    /// <summary>
    /// Creates the configuration of the frontend.
    /// </summary>
    public static Dictionary<string, string?> CreateConfiguration(
        BffApplicationSettings settings,
        string origin
    )
    {
        var configuration = new Dictionary<string, string?>
        {
            [$"{ConfigurationPrefix}:Authority"] = settings.Authority,
            [$"{ConfigurationPrefix}:PublicOrigin"] = origin,
            [$"{ConfigurationPrefix}:ClientId"] = settings.ClientId,
            [$"{ConfigurationPrefix}:ClientAuthentication:Method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .PrivateKeyJwt,
            [$"{ConfigurationPrefix}:ClientAuthentication:KeyRing"] = ClientKeyRing,
            [$"{ConfigurationPrefix}:DPoP:KeyRing"] = DPoPKeyRing,
            [$"{ConfigurationPrefix}:Jwks:Enabled"] = "true",
            [$"{ConfigurationPrefix}:IdToken:SigningAlgorithms:0"] = AlgorithmCodes
                .DigitalSignature
                .RsaSsaPssSha256,
            [$"{ConfigurationPrefix}:ReturnUrls:0"] = "/",
            [$"{ConfigurationPrefix}:UserClaims:0"] = "email",
            [$"{ConfigurationPrefix}:Session:SameSite"] = settings.SameSite.ToString(),
            [$"{ConfigurationPrefix}:Session:RevalidationInterval"] =
                settings.RevalidationInterval.ToString("c", CultureInfo.InvariantCulture),
        };
        for (var i = 0; i < settings.TrustedOrigins.Count; i++)
        {
            configuration[$"{ConfigurationPrefix}:TrustedOrigins:{i}"] = settings.TrustedOrigins[i];
        }

        return configuration;
    }

    /// <summary>
    /// Registers the key rings of the frontend and the trust of the OpenID Provider's certificate. Call it after
    /// <c>AddIdentityBff</c>, which configures its own primary handler for the named client.
    /// </summary>
    public static void AddServices(
        IServiceCollection serviceCollection,
        BffApplicationSettings settings,
        ICollection<ECDsa> keys
    )
    {
        AddKeyRing(serviceCollection, keys, ClientKeyRing);
        AddKeyRing(serviceCollection, keys, DPoPKeyRing);

        serviceCollection
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(
                (handler, _) =>
                    ((SocketsHttpHandler)handler).SslOptions.RemoteCertificateValidationCallback = (
                        _,
                        certificate,
                        _,
                        errors
                    ) =>
                        errors == SslPolicyErrors.None
                        || settings.Certificate.IsTrusted(certificate)
            );
    }

    private static void AddKeyRing(
        IServiceCollection serviceCollection,
        ICollection<ECDsa> keys,
        string keyRing
    )
    {
        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        keys.Add(key);
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            keyRing,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        serviceProvider
                            .GetRequiredService<ISecretKeyFactory>()
                            .CreateEcc(new KeyMetadata { KeyId = keyRing }, key),
                    ])
        );
    }
}
