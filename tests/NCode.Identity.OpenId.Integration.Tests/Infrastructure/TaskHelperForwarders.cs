// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

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
