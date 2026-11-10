// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

public partial class HostApplicationFactory
{
    public Task SeedPublicClientAsync(string clientId, params string[] redirectUris) =>
        SeedPublicClientAsync(clientId, redirectUris, CancellationToken.None).AsTask();

    public Task SeedPublicClientWithSettingsAsync(
        string clientId,
        IReadOnlyDictionary<string, object> settings,
        params string[] redirectUris
    ) =>
        SeedPublicClientWithSettingsAsync(clientId, settings, redirectUris, CancellationToken.None)
            .AsTask();

    public Task SeedPrivateKeyJwtClientWithSettingsAsync(
        string clientId,
        string jwksUri,
        IReadOnlyDictionary<string, object> settings,
        params string[] redirectUris
    ) =>
        SeedPrivateKeyJwtClientWithSettingsAsync(
                clientId,
                jwksUri,
                settings,
                redirectUris,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedPrivateKeyJwtClientWithSettingsAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyDictionary<string, object> settings,
        params string[] redirectUris
    ) =>
        SeedPrivateKeyJwtClientWithSettingsAsync(
                clientId,
                certificate,
                settings,
                redirectUris,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedConfidentialClientAsync(
        string clientId,
        string clientSecret,
        params string[] grantTypes
    ) =>
        SeedConfidentialClientAsync(clientId, clientSecret, grantTypes, CancellationToken.None)
            .AsTask();

    public Task SeedClientSecretBasicClientAsync(
        string clientId,
        string clientSecret,
        params string[] grantTypes
    ) =>
        SeedClientSecretBasicClientAsync(clientId, clientSecret, grantTypes, CancellationToken.None)
            .AsTask();

    public Task SeedClientSecretJwtClientAsync(
        string clientId,
        string clientSecret,
        params string[] grantTypes
    ) =>
        SeedClientSecretJwtClientAsync(clientId, clientSecret, grantTypes, CancellationToken.None)
            .AsTask();

    public Task SeedClientWithoutAuthenticationMethodAsync(
        string clientId,
        string clientSecret,
        params string[] grantTypes
    ) =>
        SeedClientWithoutAuthenticationMethodAsync(
                clientId,
                clientSecret,
                grantTypes,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedSharedSecretClientWithSettingsAsync(
        string clientId,
        string clientSecret,
        string tokenEndpointAuthMethod,
        IReadOnlyDictionary<string, object> settings,
        params string[] redirectUris
    ) =>
        SeedSharedSecretClientWithSettingsAsync(
                clientId,
                clientSecret,
                tokenEndpointAuthMethod,
                settings,
                redirectUris,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedConfidentialClientWithCertificateAsync(
        string clientId,
        X509Certificate2 certificate,
        params string[] grantTypes
    ) =>
        SeedConfidentialClientWithCertificateAsync(
                clientId,
                certificate,
                grantTypes,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedConfidentialClientWithJwksUriAsync(
        string clientId,
        string jwksUri,
        params string[] grantTypes
    ) =>
        SeedConfidentialClientWithJwksUriAsync(
                clientId,
                jwksUri,
                grantTypes,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedSelfSignedTlsClientWithJwksUriAsync(
        string clientId,
        string jwksUri,
        params string[] grantTypes
    ) =>
        SeedSelfSignedTlsClientWithJwksUriAsync(
                clientId,
                jwksUri,
                grantTypes,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedSelfSignedTlsClientAsync(
        string clientId,
        X509Certificate2 certificate,
        params string[] grantTypes
    ) =>
        SeedSelfSignedTlsClientAsync(clientId, certificate, grantTypes, CancellationToken.None)
            .AsTask();

    public Task SeedSelfSignedTlsClientAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyDictionary<string, object> additionalSettings,
        params string[] grantTypes
    ) =>
        SeedSelfSignedTlsClientAsync(
                clientId,
                certificate,
                additionalSettings,
                grantTypes,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedTlsClientAuthSubjectDnClientAsync(
        string clientId,
        string subjectDn,
        IReadOnlyCollection<string> trustAnchors,
        params string[] grantTypes
    ) =>
        SeedTlsClientAuthSubjectDnClientAsync(
                clientId,
                subjectDn,
                trustAnchors,
                grantTypes,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedTlsClientAuthSubjectDnClientAsync(
        string clientId,
        string subjectDn,
        IReadOnlyCollection<string> trustAnchors,
        IReadOnlyDictionary<string, object> additionalSettings,
        params string[] grantTypes
    ) =>
        SeedTlsClientAuthSubjectDnClientAsync(
                clientId,
                subjectDn,
                trustAnchors,
                additionalSettings,
                grantTypes,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedConfidentialClientWithSecurityProfilesAsync(
        string clientId,
        string clientSecret,
        IReadOnlyCollection<string> securityProfiles,
        params string[] grantTypes
    ) =>
        SeedConfidentialClientWithSecurityProfilesAsync(
                clientId,
                clientSecret,
                securityProfiles,
                grantTypes,
                CancellationToken.None
            )
            .AsTask();
}

public static partial class InteractiveLogin
{
    public static Task<string> SignInAsync(HttpClient client, string userName, string password) =>
        SignInAsync(client, userName, password, CancellationToken.None).AsTask();

    public static Task<HttpResponseMessage> SubmitLoginAsync(
        HttpClient client,
        string userName,
        string password,
        string returnUrl
    ) => SubmitLoginAsync(client, userName, password, returnUrl, CancellationToken.None).AsTask();

    public static Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string pageUrl,
        string actionPath,
        string? sessionCookie,
        IReadOnlyDictionary<string, string> fields
    ) =>
        PostFormAsync(client, pageUrl, actionPath, sessionCookie, fields, CancellationToken.None)
            .AsTask();

    public static Task<HttpResponseMessage> AnswerEndSessionConfirmationAsync(
        HttpClient client,
        string endSessionUrl,
        string sessionCookie,
        string action
    ) =>
        AnswerEndSessionConfirmationAsync(
                client,
                endSessionUrl,
                sessionCookie,
                action,
                CancellationToken.None
            )
            .AsTask();

    public static Task<HttpResponseMessage> ConfirmEndSessionAsync(
        HttpClient client,
        string endSessionUrl,
        string sessionCookie
    ) =>
        ConfirmEndSessionAsync(client, endSessionUrl, sessionCookie, CancellationToken.None)
            .AsTask();
}

public static partial class ClientAuthenticationProbe
{
    public static Task<string> GetIssuerAsync(HttpClient client) =>
        GetIssuerAsync(client, CancellationToken.None).AsTask();

    public static Task<string?> RevokeWithSecretAsync(
        HttpClient client,
        string clientId,
        string clientSecret
    ) => RevokeWithSecretAsync(client, clientId, clientSecret, CancellationToken.None).AsTask();

    public static Task<string?> RevokeWithAssertionAsync(
        HttpClient client,
        string clientId,
        string clientSecret,
        string audience
    ) =>
        RevokeWithAssertionAsync(client, clientId, clientSecret, audience, CancellationToken.None)
            .AsTask();
}
