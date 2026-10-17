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

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Logic;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Results;
using NCode.Identity.OpenId.Authentication.Logic;
using NCode.Identity.OpenId.Authentication.Models;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logic;
using NCode.Json;
using NCode.Persistence.Stores;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration;

/// <summary>
/// Provides a default implementation of the required services and handlers used by the dynamic client registration
/// endpoint (RFC 7591) and the per-client registration management endpoint (RFC 7592).
/// </summary>
internal class DefaultClientRegistrationEndpointHandler(
    TimeProvider timeProvider,
    LinkGenerator linkGenerator,
    ICryptoService cryptoService,
    ISecretGenerator secretGenerator,
    ISecretImporter secretImporter,
    IStoreManagerFactory storeManagerFactory,
    IPersistedGrantService persistedGrantService,
    IInitialAccessTokenValidator initialAccessTokenValidator,
    ISectorIdentifierValidator sectorIdentifierValidator
) : IEndpointProvider
{
    private const int ClientSecretKeySizeBits = 256;

    private static readonly string[] DefaultGrantTypes =
    [
        OpenIdConstants.GrantTypes.AuthorizationCode,
    ];
    private static readonly string[] DefaultResponseTypes = [OpenIdConstants.ResponseTypes.Code];

    private static readonly string[] SupportedTokenEndpointAuthMethods =
    [
        OpenIdConstants.ClientAuthenticationMethods.None,
        OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic,
        OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost,
    ];

    private static readonly string[] SupportedSubjectTypes =
    [
        OpenIdConstants.SubjectTypes.Public,
        OpenIdConstants.SubjectTypes.Pairwise,
    ];

    private TimeProvider TimeProvider { get; } = timeProvider;
    private LinkGenerator LinkGenerator { get; } = linkGenerator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ISecretImporter SecretImporter { get; } = secretImporter;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IInitialAccessTokenValidator InitialAccessTokenValidator { get; } =
        initialAccessTokenValidator;
    private ISectorIdentifierValidator SectorIdentifierValidator { get; } =
        sectorIdentifierValidator;

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost(OpenIdConstants.EndpointPaths.Registration, RegisterClientAsync)
            .WithName(OpenIdConstants.EndpointNames.Registration)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces<RegisterClientResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .DisableAntiforgery()
            .WithOpenIdDiscoverable();

        var managementPath = $"{OpenIdConstants.EndpointPaths.Registration}/{{client_id}}";

        endpoints
            .MapMethods(
                managementPath,
                [HttpMethods.Get, HttpMethods.Put, HttpMethods.Delete],
                ManageClientAsync
            )
            .WithName(OpenIdConstants.EndpointNames.RegistrationManagement)
            .WithTags(OpenIdConstants.EndpointTags.OpenId)
            .Produces<RegisterClientResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound)
            .DisableAntiforgery();
    }

    private static bool IsApplicationJsonContentType(HttpContext httpContext) =>
        MediaTypeHeaderValue.TryParse(httpContext.Request.ContentType, out var header)
        && header.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase);

    private static string? GetBearerToken(HttpContext httpContext)
    {
        const string bearerPrefix = "Bearer ";
        foreach (var value in httpContext.Request.Headers.Authorization)
        {
            if (
                !string.IsNullOrEmpty(value)
                && value.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            )
            {
                var token = value[bearerPrefix.Length..].Trim();
                if (!string.IsNullOrEmpty(token))
                    return token;
            }
        }

        return null;
    }

    /// <summary>
    /// Dynamic client registration endpoint (RFC 7591).
    /// </summary>
    /// <remarks>
    /// Registers a new OpenID client from the submitted JSON client metadata and returns the assigned
    /// <c>client_id</c>, an optional <c>client_secret</c> for a confidential client, and the
    /// <c>registration_access_token</c> and <c>registration_client_uri</c> the client uses to manage its own
    /// registration (RFC 7592). When the tenant does not allow anonymous registration, the caller must present a
    /// valid initial access token as a Bearer credential.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="201">The registered client information response.</response>
    /// <response code="400">An error response carrying an error code and an optional error_description.</response>
    /// <response code="401">The initial access token is missing or invalid.</response>
    internal virtual async ValueTask<IResult> RegisterClientAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var openIdEnvironment = openIdContext.Environment;
        var errorFactory = openIdContext.ErrorFactory;
        var tenantId = openIdContext.Tenant.TenantId;

        if (!IsApplicationJsonContentType(httpContext))
        {
            return errorFactory
                .InvalidRequest(
                    "Only requests with Content-Type 'application/json' are supported.",
                    OpenIdConstants.ErrorCodes.InvalidClientMetadata
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var authorized = await AuthorizeRegistrationAsync(
            openIdContext,
            httpContext,
            cancellationToken
        );

        if (!authorized)
        {
            return errorFactory
                .InvalidToken()
                .WithStatusCode(StatusCodes.Status401Unauthorized)
                .AsHttpResult();
        }

        var request = await ReadRequestOrDefaultAsync(httpContext, cancellationToken);
        if (request is null)
        {
            return errorFactory
                .InvalidRequest(
                    "The request body could not be parsed as JSON client metadata.",
                    OpenIdConstants.ErrorCodes.InvalidClientMetadata
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var validationError = ValidateMetadata(errorFactory, request, out var resolved);
        if (validationError is not null)
        {
            return validationError.WithStatusCode(StatusCodes.Status400BadRequest).AsHttpResult();
        }

        var sectorError = await ValidateSectorIdentifierAsync(
            openIdContext,
            resolved,
            cancellationToken
        );
        if (sectorError is not null)
        {
            return sectorError.WithStatusCode(StatusCodes.Status400BadRequest).AsHttpResult();
        }

        var clientId = CryptoService.GenerateResourceId();
        var createdWhen = TimeProvider.GetUtcNow();

        var isConfidential =
            resolved.TokenEndpointAuthMethod != OpenIdConstants.ClientAuthenticationMethods.None;

        var secretsError = BuildClientSecrets(
            request,
            isConfidential,
            createdWhen,
            errorFactory,
            out var secrets,
            out var clientSecret
        );
        if (secretsError is not null)
        {
            return secretsError;
        }

        var settings = BuildSettingsObject(request, resolved);

        var client = new PersistedClient
        {
            TenantId = tenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = tenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = JsonSerializer.SerializeToElement(settings),
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = tenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = secrets,
            },
        };

        await using (var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken))
        {
            var store = storeManager.GetStore<IClientStore>();
            await store.AddAsync(client, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        var registrationAccessToken = CryptoService.GenerateUrlSafeKey();
        var registrationGrant = new PersistedGrant<RegistrationAccessTokenGrant>
        {
            Status = PersistedGrantStatus.Active,
            TenantId = tenantId,
            ClientId = clientId,
            SubjectId = null,
            SessionId = null,
            Payload = new RegistrationAccessTokenGrant { ClientId = clientId },
        };

        await PersistedGrantService.AddAsync(
            openIdContext,
            PersistedGrantService.CreateGrantId(
                tenantId,
                OpenIdConstants.PersistedGrantTypes.RegistrationAccessToken,
                registrationAccessToken
            ),
            registrationGrant,
            createdWhen,
            lifetime: null,
            cancellationToken
        );

        var registrationClientUri = GetRegistrationClientUri(httpContext, clientId);

        var response = ToResponse(client);
        response = response with
        {
            ClientSecret = clientSecret,
            ClientIdIssuedAt = createdWhen.ToUnixTimeSeconds(),
            ClientSecretExpiresAt = clientSecret is null ? null : 0,
            RegistrationAccessToken = registrationAccessToken,
            RegistrationClientUri = registrationClientUri,
        };

        return TypedResults.Json(
            response,
            openIdEnvironment.JsonSerializerOptions,
            statusCode: StatusCodes.Status201Created
        );
    }

    /// <summary>
    /// Registration management endpoint (RFC 7592).
    /// </summary>
    /// <remarks>
    /// Reads (<c>GET</c>), replaces (<c>PUT</c>), or deletes (<c>DELETE</c>) the metadata of an existing client. The
    /// caller authenticates with the client's <c>registration_access_token</c> presented as a Bearer credential.
    /// </remarks>
    /// <param name="httpContext">The <see cref="HttpContext"/> for the current request.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <response code="200">The current client information response (GET/PUT).</response>
    /// <response code="204">The client was deleted (DELETE).</response>
    /// <response code="400">An error response carrying an error code and an optional error_description.</response>
    /// <response code="401">The registration access token is missing or invalid.</response>
    /// <response code="404">No client matches the registration access token.</response>
    internal virtual async ValueTask<IResult> ManageClientAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var errorFactory = openIdContext.ErrorFactory;
        var tenantId = openIdContext.Tenant.TenantId;

        var clientId = (string?)httpContext.Request.RouteValues["client_id"];
        if (string.IsNullOrEmpty(clientId))
            return TypedResults.NotFound();

        var token = GetBearerToken(httpContext);
        if (token is null)
        {
            return errorFactory
                .InvalidToken()
                .WithStatusCode(StatusCodes.Status401Unauthorized)
                .AsHttpResult();
        }

        var grantId = PersistedGrantService.CreateGrantId(
            tenantId,
            OpenIdConstants.PersistedGrantTypes.RegistrationAccessToken,
            token
        );

        var grant = await PersistedGrantService.GetOrDefaultAsync<RegistrationAccessTokenGrant>(
            openIdContext,
            grantId,
            cancellationToken
        );

        if (
            grant is not { Status: PersistedGrantStatus.Active }
            || !string.Equals(grant.Value.Payload.ClientId, clientId, StringComparison.Ordinal)
        )
        {
            return errorFactory
                .InvalidToken()
                .WithStatusCode(StatusCodes.Status401Unauthorized)
                .AsHttpResult();
        }

        return httpContext.Request.Method switch
        {
            var method when method == HttpMethods.Get => await ReadClientAsync(
                httpContext,
                clientId,
                cancellationToken
            ),
            var method when method == HttpMethods.Put => await UpdateClientAsync(
                httpContext,
                clientId,
                cancellationToken
            ),
            var method when method == HttpMethods.Delete => await DeleteClientAsync(
                httpContext,
                clientId,
                grantId,
                cancellationToken
            ),
            _ => TypedResults.StatusCode(StatusCodes.Status405MethodNotAllowed),
        };
    }

    private async ValueTask<IResult> ReadClientAsync(
        HttpContext httpContext,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
            return TypedResults.NotFound();

        var response = ToResponse(client) with
        {
            RegistrationClientUri = GetRegistrationClientUri(httpContext, clientId),
        };

        return TypedResults.Json(response, openIdContext.Environment.JsonSerializerOptions);
    }

    private async ValueTask<IResult> UpdateClientAsync(
        HttpContext httpContext,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        var errorFactory = openIdContext.ErrorFactory;

        if (!IsApplicationJsonContentType(httpContext))
        {
            return errorFactory
                .InvalidRequest(
                    "Only requests with Content-Type 'application/json' are supported.",
                    OpenIdConstants.ErrorCodes.InvalidClientMetadata
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var request = await ReadRequestOrDefaultAsync(httpContext, cancellationToken);
        if (request is null)
        {
            return errorFactory
                .InvalidRequest(
                    "The request body could not be parsed as JSON client metadata.",
                    OpenIdConstants.ErrorCodes.InvalidClientMetadata
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var validationError = ValidateMetadata(errorFactory, request, out var resolved);
        if (validationError is not null)
        {
            return validationError.WithStatusCode(StatusCodes.Status400BadRequest).AsHttpResult();
        }

        var sectorError = await ValidateSectorIdentifierAsync(
            openIdContext,
            resolved,
            cancellationToken
        );
        if (sectorError is not null)
        {
            return sectorError.WithStatusCode(StatusCodes.Status400BadRequest).AsHttpResult();
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
            return TypedResults.NotFound();

        var settings = BuildSettingsObject(request, resolved);
        client.Settings.Value = JsonSerializer.SerializeToElement(settings);

        await store.UpdateSettingsAsync(client.Settings, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        var response = ToResponse(client) with
        {
            RegistrationClientUri = GetRegistrationClientUri(httpContext, clientId),
        };

        return TypedResults.Json(response, openIdContext.Environment.JsonSerializerOptions);
    }

    private async ValueTask<IResult> DeleteClientAsync(
        HttpContext httpContext,
        string clientId,
        PersistedGrantId registrationGrantId,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
            return TypedResults.NotFound();

        // A client cannot be removed while it still owns secrets, so remove its dependents first.
        var clientSecrets = await store.GetSecretsOrDefaultAsync(clientId, cancellationToken);
        if (clientSecrets is not null)
        {
            foreach (var secret in clientSecrets.Value)
            {
                await store.RemoveSecretAsync(clientId, secret.SecretId, cancellationToken);
            }
        }

        await store.RemoveAsync(clientId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        await PersistedGrantService.SetRevokedOnceAsync(
            openIdContext,
            registrationGrantId,
            TimeProvider.GetUtcNow(),
            cancellationToken
        );

        return TypedResults.NoContent();
    }

    private async ValueTask<bool> AuthorizeRegistrationAsync(
        OpenIdContext openIdContext,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var allowAnonymous = openIdContext.Tenant.SettingsProvider.Collection.GetValue(
            OpenIdSettingKeys.RegistrationEndpointAllowAnonymous
        );

        if (allowAnonymous)
            return true;

        var token = GetBearerToken(httpContext);
        if (token is null)
            return false;

        return await InitialAccessTokenValidator.ValidateAsync(
            openIdContext,
            token,
            cancellationToken
        );
    }

    private static async ValueTask<RegisterClientRequest?> ReadRequestOrDefaultAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = httpContext.GetOpenIdContext();
        try
        {
            return await httpContext.Request.ReadFromJsonAsync<RegisterClientRequest>(
                openIdContext.Environment.JsonSerializerOptions,
                cancellationToken
            );
        }
        catch (Exception exception) when (exception is JsonException or BadHttpRequestException)
        {
            return null;
        }
    }

    private string GetRegistrationClientUri(HttpContext httpContext, string clientId) =>
        LinkGenerator.GetUriByName(
            httpContext,
            OpenIdConstants.EndpointNames.RegistrationManagement,
            new { client_id = clientId }
        )
        ?? throw new InvalidOperationException("Unable to determine the registration client URI.");

    /// <summary>
    /// Validates the submitted client metadata and resolves the effective values (defaults applied).
    /// </summary>
    internal virtual IOpenIdError? ValidateMetadata(
        IOpenIdErrorFactory errorFactory,
        RegisterClientRequest request,
        out ResolvedMetadata resolved
    )
    {
        resolved = default;

        var tokenEndpointAuthMethod =
            request.TokenEndpointAuthMethod
            ?? OpenIdConstants.ClientAuthenticationMethods.ClientSecretBasic;

        if (!SupportedTokenEndpointAuthMethods.Contains(tokenEndpointAuthMethod))
        {
            return errorFactory.InvalidRequest(
                $"The 'token_endpoint_auth_method' value '{tokenEndpointAuthMethod}' is not supported.",
                OpenIdConstants.ErrorCodes.InvalidClientMetadata
            );
        }

        var grantTypes = request.GrantTypes is { Count: > 0 }
            ? request.GrantTypes
            : DefaultGrantTypes;

        var responseTypes = request.ResponseTypes is { Count: > 0 }
            ? request.ResponseTypes
            : DefaultResponseTypes;

        var redirectUris = request.RedirectUris ?? [];

        var requiresRedirectUris =
            grantTypes.Contains(OpenIdConstants.GrantTypes.AuthorizationCode)
            || grantTypes.Contains(OpenIdConstants.GrantTypes.Implicit);

        if (requiresRedirectUris && redirectUris.Count == 0)
        {
            return errorFactory.InvalidRequest(
                "At least one 'redirect_uris' value is required for the requested grant types.",
                OpenIdConstants.ErrorCodes.InvalidRedirectUri
            );
        }

        foreach (var redirectUri in redirectUris)
        {
            if (
                !Uri.TryCreate(redirectUri, UriKind.Absolute, out _)
                || redirectUri.Contains('#', StringComparison.Ordinal)
            )
            {
                return errorFactory.InvalidRequest(
                    $"The redirect URI '{redirectUri}' is not a valid absolute URI without a fragment.",
                    OpenIdConstants.ErrorCodes.InvalidRedirectUri
                );
            }
        }

        var subjectType = request.SubjectType;
        if (!string.IsNullOrEmpty(subjectType) && !SupportedSubjectTypes.Contains(subjectType))
        {
            return errorFactory.InvalidRequest(
                $"The 'subject_type' value '{subjectType}' is not supported.",
                OpenIdConstants.ErrorCodes.InvalidClientMetadata
            );
        }

        var sectorIdentifierUri = request.SectorIdentifierUri;

        // OpenID Connect Core 1.0 §8.1 requires a sector_identifier_uri for a pairwise client whose redirect_uris span
        // more than one host; without it the sector would be ambiguous. The remote document fetch + membership check is
        // performed separately (ISectorIdentifierValidator), because it is an asynchronous, SSRF-guarded network call.
        if (
            string.IsNullOrEmpty(sectorIdentifierUri)
            && string.Equals(
                subjectType,
                OpenIdConstants.SubjectTypes.Pairwise,
                StringComparison.Ordinal
            )
            && HasMultipleDistinctHosts(redirectUris)
        )
        {
            return errorFactory.InvalidRequest(
                "A 'sector_identifier_uri' is required for a pairwise client whose 'redirect_uris' span multiple hosts.",
                OpenIdConstants.ErrorCodes.InvalidClientMetadata
            );
        }

        resolved = new ResolvedMetadata
        {
            TokenEndpointAuthMethod = tokenEndpointAuthMethod,
            GrantTypes = grantTypes,
            ResponseTypes = responseTypes,
            RedirectUris = redirectUris,
            SubjectType = subjectType,
            SectorIdentifierUri = sectorIdentifierUri,
        };

        return null;
    }

    private static bool HasMultipleDistinctHosts(IReadOnlyCollection<string> redirectUris)
    {
        string? firstHost = null;
        foreach (var redirectUri in redirectUris)
        {
            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri))
                continue;

            if (firstHost is null)
                firstHost = uri.Host;
            else if (!string.Equals(firstHost, uri.Host, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Performs the OpenID Connect Core 1.0 §8.1 remote validation of the resolved <c>sector_identifier_uri</c> (fetch
    /// the document and verify it lists every redirect URI), returning the resulting error, or <see langword="null"/>
    /// when there is no <c>sector_identifier_uri</c> to validate or it is valid.
    /// </summary>
    private async ValueTask<IOpenIdError?> ValidateSectorIdentifierAsync(
        OpenIdContext openIdContext,
        ResolvedMetadata resolved,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrEmpty(resolved.SectorIdentifierUri))
            return null;

        return await SectorIdentifierValidator.ValidateAsync(
            openIdContext,
            resolved.SectorIdentifierUri,
            resolved.RedirectUris,
            cancellationToken
        );
    }

    /// <summary>
    /// Builds the new client's secrets: a generated symmetric secret for a confidential client, plus any public
    /// verification keys imported from an inline <c>jwks</c> set (ADR-0104). Returns an error result when an inline
    /// supported JWK is malformed; otherwise <see langword="null"/> with the secrets and one-time client secret.
    /// </summary>
    private IResult? BuildClientSecrets(
        RegisterClientRequest request,
        bool isConfidential,
        DateTimeOffset createdWhen,
        IOpenIdErrorFactory errorFactory,
        out List<PersistedSecret> secrets,
        out string? clientSecret
    )
    {
        secrets = [];
        clientSecret = null;

        if (isConfidential)
        {
            // A client_secret is an opaque string the client later presents verbatim. GenerateSharedSecret stores it
            // as the UTF-8 bytes client authentication compares against (see CommonClientAuthenticationHandler.
            // IsSecretEqual), unlike GenerateSecret's raw-byte crypto-key material whose base64url reveal never
            // round-trips back to the stored bytes.
            var generated = SecretGenerator.GenerateSharedSecret(
                new GenerateSecretRequest
                {
                    SecretId = CryptoService.GenerateResourceId(),
                    SecretType = SecretTypes.Symmetric,
                    KeySizeBits = ClientSecretKeySizeBits,
                    Use = null,
                    Algorithm = null,
                    CreatedWhen = createdWhen,
                    ExpiresWhen = DateTimeOffset.MaxValue,
                }
            );
            clientSecret = generated.SecretMaterial;
            secrets.Add(generated.Secret);
        }

        // Import any inline JWK Set keys as public verification secrets (ADR-0104); runtime jwks_uri resolution is
        // deferred (F16 Phase 0). An unsupported key type is skipped; a malformed supported key fails registration.
        if (
            request.Jwks is not { ValueKind: JsonValueKind.Object } jwks
            || !jwks.TryGetProperty("keys", out var jwksKeys)
            || jwksKeys.ValueKind != JsonValueKind.Array
        )
        {
            return null;
        }

        foreach (var jwk in jwksKeys.EnumerateArray())
        {
            if (!TryImportJwk(jwk, createdWhen, out var importedSecret, out var importError))
            {
                return errorFactory
                    .InvalidRequest(
                        importError ?? "The supplied JWK is invalid.",
                        OpenIdConstants.ErrorCodes.InvalidClientMetadata
                    )
                    .WithStatusCode(StatusCodes.Status400BadRequest)
                    .AsHttpResult();
            }

            if (importedSecret is not null)
            {
                secrets.Add(importedSecret);
            }
        }

        return null;
    }

    /// <summary>
    /// Attempts to import a single inline <c>JWK</c> as a public verification secret. Returns <see langword="true"/>
    /// with a <see langword="null"/> secret for an unsupported key type (skipped), <see langword="true"/> with the
    /// imported secret for a supported key, or <see langword="false"/> with an error for a malformed supported key.
    /// </summary>
    // See ADR-0104.
    private bool TryImportJwk(
        JsonElement jwk,
        DateTimeOffset createdWhen,
        out PersistedSecret? secret,
        out string? error
    )
    {
        secret = null;
        error = null;

        if (
            jwk.ValueKind != JsonValueKind.Object
            || !jwk.TryGetPropertyValue<string>("kty", out var kty)
        )
        {
            return true;
        }

        var secretType = kty switch
        {
            "RSA" => SecretTypes.Rsa,
            "EC" => SecretTypes.Ecc,
            _ => null,
        };
        if (secretType is null)
        {
            return true;
        }

        var keyId = ReadStringMember(jwk, "kid");
        if (string.IsNullOrEmpty(keyId))
        {
            keyId = CryptoService.GenerateResourceId();
        }

        try
        {
            secret = SecretImporter.ImportSecret(
                new SecretKeyImportRequest
                {
                    SecretId = keyId,
                    SecretType = secretType,
                    Format = SecretKeyImportFormat.Jwk,
                    KeyMaterial = jwk.GetRawText(),
                    Use = ReadStringMember(jwk, "use"),
                    Algorithm = ReadStringMember(jwk, "alg"),
                    CreatedWhen = createdWhen,
                    ExpiresWhen = DateTimeOffset.MaxValue,
                }
            );
            return true;
        }
        catch (ArgumentException exception)
        {
            error = $"The supplied JWK is invalid: {exception.Message}";
            return false;
        }
    }

    private static string? ReadStringMember(JsonElement element, string memberName) =>
        element.TryGetPropertyValue<string>(memberName, out var value) ? value : null;

    /// <summary>
    /// Projects the submitted metadata and the resolved values into the client's persisted settings document.
    /// </summary>
    internal virtual JsonObject BuildSettingsObject(
        RegisterClientRequest request,
        ResolvedMetadata resolved
    )
    {
        var settings = new JsonObject
        {
            [OpenIdSettingNames.RedirectUris] = ToJsonArray(resolved.RedirectUris),
            [OpenIdSettingNames.GrantTypesSupported] = ToJsonArray(resolved.GrantTypes),
            [OpenIdSettingNames.ResponseTypesSupported] = ToJsonArray(resolved.ResponseTypes),
            [RegistrationMetadataKeys.TokenEndpointAuthMethod] = resolved.TokenEndpointAuthMethod,
        };

        SetIfPresent(settings, OpenIdSettingNames.SubjectType, resolved.SubjectType);
        SetIfPresent(
            settings,
            OpenIdSettingNames.SectorIdentifierUri,
            resolved.SectorIdentifierUri
        );

        if (!string.IsNullOrEmpty(request.Scope))
        {
            var scopes = request.Scope.Split(
                OpenIdConstants.ParameterSeparatorChar,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );
            settings[OpenIdSettingNames.ScopesSupported] = ToJsonArray(scopes);
        }

        SetIfPresent(settings, RegistrationMetadataKeys.ClientName, request.ClientName);
        SetIfPresent(settings, RegistrationMetadataKeys.ClientUri, request.ClientUri);
        SetIfPresent(settings, RegistrationMetadataKeys.LogoUri, request.LogoUri);
        SetIfPresent(settings, RegistrationMetadataKeys.TosUri, request.TosUri);
        SetIfPresent(settings, RegistrationMetadataKeys.PolicyUri, request.PolicyUri);
        SetIfPresent(settings, RegistrationMetadataKeys.JwksUri, request.JwksUri);
        SetIfPresent(settings, RegistrationMetadataKeys.SoftwareId, request.SoftwareId);
        SetIfPresent(settings, RegistrationMetadataKeys.SoftwareVersion, request.SoftwareVersion);

        if (request.Contacts is { Count: > 0 })
            settings[RegistrationMetadataKeys.Contacts] = ToJsonArray(request.Contacts);

        if (request.Jwks is { ValueKind: JsonValueKind.Object } jwks)
            settings[RegistrationMetadataKeys.Jwks] = JsonSerializer.SerializeToNode(jwks);

        return settings;
    }

    /// <summary>
    /// Reconstructs a client information response from a persisted client's settings document.
    /// </summary>
    internal virtual RegisterClientResponse ToResponse(PersistedClient client)
    {
        var settings = ToJsonObject(client.Settings.Value);

        var scopes = ReadStringArray(settings, OpenIdSettingNames.ScopesSupported);

        return new RegisterClientResponse
        {
            ClientId = client.ClientId,
            RedirectUris = ReadStringArray(settings, OpenIdSettingNames.RedirectUris),
            GrantTypes = ReadStringArray(settings, OpenIdSettingNames.GrantTypesSupported),
            ResponseTypes = ReadStringArray(settings, OpenIdSettingNames.ResponseTypesSupported),
            TokenEndpointAuthMethod = ReadString(
                settings,
                RegistrationMetadataKeys.TokenEndpointAuthMethod
            ),
            SubjectType = ReadString(settings, OpenIdSettingNames.SubjectType),
            SectorIdentifierUri = ReadString(settings, OpenIdSettingNames.SectorIdentifierUri),
            Scope = scopes is null
                ? null
                : string.Join(OpenIdConstants.ParameterSeparatorString, scopes),
            ClientName = ReadString(settings, RegistrationMetadataKeys.ClientName),
            ClientUri = ReadString(settings, RegistrationMetadataKeys.ClientUri),
            LogoUri = ReadString(settings, RegistrationMetadataKeys.LogoUri),
            Contacts = ReadStringArray(settings, RegistrationMetadataKeys.Contacts),
            TosUri = ReadString(settings, RegistrationMetadataKeys.TosUri),
            PolicyUri = ReadString(settings, RegistrationMetadataKeys.PolicyUri),
            JwksUri = ReadString(settings, RegistrationMetadataKeys.JwksUri),
            Jwks = ReadElement(settings, RegistrationMetadataKeys.Jwks),
            SoftwareId = ReadString(settings, RegistrationMetadataKeys.SoftwareId),
            SoftwareVersion = ReadString(settings, RegistrationMetadataKeys.SoftwareVersion),
        };
    }

    private static JsonArray ToJsonArray(IReadOnlyCollection<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static void SetIfPresent(JsonObject settings, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            settings[key] = value;
    }

    private static JsonObject ToJsonObject(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? JsonObject.Create(element) ?? [] : [];

    private static List<string>? ReadStringArray(JsonObject settings, string key)
    {
        if (settings[key] is not JsonArray array)
            return null;

        var values = new List<string>(array.Count);
        foreach (var node in array)
        {
            if (node?.GetValueKind() == JsonValueKind.String)
                values.Add(node.GetValue<string>());
        }

        return values.Count == 0 ? null : values;
    }

    private static string? ReadString(JsonObject settings, string key)
    {
        var node = settings[key];
        return node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;
    }

    private static JsonElement? ReadElement(JsonObject settings, string key) =>
        settings[key] is { } node ? JsonSerializer.SerializeToElement(node) : null;
}
