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

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization.Results;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Settings;
using NCode.Time;

namespace NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization;

/// <summary>
/// Provides a default implementation of <see cref="IPushedAuthorizationRequestService"/> that persists a pushed
/// authorization request as a single-use grant keyed by the opaque reference embedded in the issued
/// <c>request_uri</c> (RFC 9126).
/// </summary>
internal sealed class DefaultPushedAuthorizationRequestService(
    TimeProvider timeProvider,
    ICryptoService cryptoService,
    IPersistedGrantService persistedGrantService
) : IPushedAuthorizationRequestService
{
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;

    // Client-authentication secrets are never persisted as part of the pushed request; they authenticate the push
    // itself and are not replayed at the authorization endpoint.
    private static readonly HashSet<string> ExcludedParameters = new(StringComparer.Ordinal)
    {
        OpenIdConstants.Parameters.ClientSecret,
        OpenIdConstants.Parameters.ClientAssertion,
        OpenIdConstants.Parameters.ClientAssertionType,
    };

    /// <inheritdoc />
    public async ValueTask<PushedAuthorizationResult> StoreAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IRequestValues requestValues,
        CancellationToken cancellationToken
    )
    {
        var tenantId = openIdContext.Tenant.TenantId;
        var settings = openIdClient.Settings;

        var lifetime = settings.GetValue(OpenIdSettingKeys.PushedAuthorizationRequestLifetime);
        var createdWhen = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        var parameters = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var (key, value) in requestValues)
        {
            if (ExcludedParameters.Contains(key))
                continue;

            parameters[key] = value.OfType<string>().ToArray();
        }

        var reference = CryptoService.GenerateUrlSafeKey();

        var grant = new PersistedGrant<PushedAuthorizationGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = tenantId,
            ClientId = openIdClient.ClientId,
            SubjectId = null,
            SessionId = null,
            Payload = new PushedAuthorizationGrant
            {
                ClientId = openIdClient.ClientId,
                Parameters = parameters,
            },
        };

        await PersistedGrantService.AddAsync(
            openIdContext,
            PersistedGrantService.CreateGrantId(
                tenantId,
                OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
                reference
            ),
            grant,
            createdWhen,
            lifetime,
            cancellationToken
        );

        return new PushedAuthorizationResult
        {
            RequestUri = OpenIdConstants.PushedAuthorizationRequestUriPrefix + reference,
            ExpiresIn = (int)lifetime.TotalSeconds,
        };
    }

    /// <inheritdoc />
    public async ValueTask<IRequestValues> ConsumeAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string requestUri,
        CancellationToken cancellationToken
    )
    {
        const string prefix = OpenIdConstants.PushedAuthorizationRequestUriPrefix;
        if (!requestUri.StartsWith(prefix, StringComparison.Ordinal))
            throw InvalidRequestUri(
                openIdContext,
                "The specified 'request_uri' is not a pushed authorization request."
            );

        var reference = requestUri[prefix.Length..];
        if (string.IsNullOrEmpty(reference))
            throw InvalidRequestUri(openIdContext, "The specified 'request_uri' is malformed.");

        var tenantId = openIdContext.Tenant.TenantId;

        var grantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.PushedAuthorizationRequest,
            reference
        );

        var persistedGrantOrNull =
            await PersistedGrantService.ConsumeOnceOrDefault<PushedAuthorizationGrant>(
                openIdContext,
                grantId,
                cancellationToken
            );

        if (persistedGrantOrNull is not { } persistedGrant)
            throw InvalidRequestUri(
                openIdContext,
                "The specified 'request_uri' is unknown, expired, or has already been used."
            );

        var payload = persistedGrant.Payload;

        // RFC 9126 §4: a pushed request may only be used by the client that pushed it.
        if (!string.Equals(payload.ClientId, openIdClient.ClientId, StringComparison.Ordinal))
            throw InvalidRequestUri(
                openIdContext,
                "The specified 'request_uri' was not issued to the authenticated client."
            );

        var values = new Dictionary<string, StringValues>(StringComparer.Ordinal);
        foreach (var (key, value) in payload.Parameters)
        {
            values[key] = new StringValues(value.ToArray());
        }

        return new RequestValuesUsingCollection(
            RequestValuesSourceTypes.PushedAuthorizationRequest,
            values
        );
    }

    // A failed request_uri lookup cannot be safely redirected to the client (its redirect_uri lives in the request we
    // could not load), so the error is returned directly as an HTTP 400.
    private static OpenIdException InvalidRequestUri(
        OpenIdContext openIdContext,
        string description
    ) =>
        openIdContext
            .ErrorFactory.InvalidRequestUri(description)
            .WithStatusCode(StatusCodes.Status400BadRequest)
            .AsException();
}
