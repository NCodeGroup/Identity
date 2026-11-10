// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

public partial class HostApplicationFactory
{
    public Task<string> SeedLocalAccountAsync(
        string userName,
        string password,
        string? email = null,
        bool emailVerified = false
    ) =>
        SeedLocalAccountAsync(userName, password, email, emailVerified, CancellationToken.None)
            .AsTask();

    public Task<IReadOnlyCollection<PersistedSecret>> GetClientSecretsAsync(string clientId) =>
        GetClientSecretsAsync(clientId, CancellationToken.None).AsTask();

    public Task ExpireClientSecretsAsync(string clientId) =>
        ExpireClientSecretsAsync(clientId, CancellationToken.None).AsTask();

    public Task SeedResourceServerWithClientGrantAsync(
        string clientId,
        string resourceServerId,
        string identifier,
        params string[] scopes
    ) =>
        SeedResourceServerWithClientGrantAsync(
                clientId,
                resourceServerId,
                identifier,
                scopes,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedResourceServerWithClientGrantAsync(
        string clientId,
        string resourceServerId,
        string identifier,
        IReadOnlyDictionary<string, object> settings,
        params string[] scopes
    ) =>
        SeedResourceServerWithClientGrantAsync(
                clientId,
                resourceServerId,
                identifier,
                settings,
                scopes,
                CancellationToken.None
            )
            .AsTask();

    public Task SeedClientDirectlyAsync(string clientId, string storedSecret) =>
        SeedClientDirectlyAsync(clientId, storedSecret, CancellationToken.None).AsTask();

    public Task SeedTenantSettingsAsync(IReadOnlyDictionary<string, object> settings) =>
        SeedTenantSettingsAsync(settings, CancellationToken.None).AsTask();

    public Task ReplaceTenantSettingsAsync(IReadOnlyDictionary<string, object> settings) =>
        ReplaceTenantSettingsAsync(settings, CancellationToken.None).AsTask();

    public Task<HttpClient> CreateManagementUserClientAsync(
        string clientId,
        string redirectUri,
        string userName,
        string password,
        string scope
    ) =>
        CreateManagementUserClientAsync(
                clientId,
                redirectUri,
                userName,
                password,
                scope,
                CancellationToken.None
            )
            .AsTask();

    public Task EnsureTeamAsync() => EnsureTeamAsync(CancellationToken.None).AsTask();

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
