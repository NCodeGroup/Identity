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
using NCode.Identity.Cryptography;
using NCode.Identity.Jwk;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Grants;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Results;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Authentication.ResourceServers;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Settings;
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
    IClientKeyImporter clientKeyImporter,
    ISecretSerializer secretSerializer,
    ISecretProtectorProvider secretProtectorProvider,
    ISecretKeyTypeCatalog secretKeyTypeCatalog,
    IStoreManagerFactory storeManagerFactory,
    IPersistedGrantService persistedGrantService,
    IInitialAccessTokenValidator initialAccessTokenValidator,
    ISectorIdentifierValidator sectorIdentifierValidator,
    IClientScopeService clientScopeService,
    ISettingSerializer settingSerializer,
    IClientAuthenticationMethodCatalog clientAuthenticationMethodCatalog,
    IClientCoherenceValidator clientCoherenceValidator
) : IEndpointProvider
{
    private const int ClientSecretKeySizeBits = 256;

    // The request-object metadata (OpenID Connect Dynamic Client Registration 1.0 §2) is stored as the client's own
    // algorithm lists, each narrowed to the one registered value, which the authorization endpoint enforces (ADR-0155).
    private const string RequestObjectEncryptionAlgMetadata = "request_object_encryption_alg";
    private const string RequestObjectEncryptionEncMetadata = "request_object_encryption_enc";

    private static readonly string[] DefaultGrantTypes =
    [
        OpenIdProtocol.GrantTypes.AuthorizationCode,
    ];
    private static readonly string[] DefaultResponseTypes = [OpenIdProtocol.ResponseTypes.Code];

    private static readonly string[] SupportedSubjectTypes =
    [
        OpenIdConstants.SubjectTypes.Public,
        OpenIdConstants.SubjectTypes.Pairwise,
    ];

    private TimeProvider TimeProvider { get; } = timeProvider;
    private LinkGenerator LinkGenerator { get; } = linkGenerator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private IClientKeyImporter ClientKeyImporter { get; } = clientKeyImporter;
    private ISecretSerializer SecretSerializer { get; } = secretSerializer;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private ISecretKeyTypeCatalog SecretKeyTypeCatalog { get; } = secretKeyTypeCatalog;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IPersistedGrantService PersistedGrantService { get; } = persistedGrantService;
    private IInitialAccessTokenValidator InitialAccessTokenValidator { get; } =
        initialAccessTokenValidator;
    private ISectorIdentifierValidator SectorIdentifierValidator { get; } =
        sectorIdentifierValidator;
    private IClientScopeService ClientScopeService { get; } = clientScopeService;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private IClientAuthenticationMethodCatalog ClientAuthenticationMethodCatalog { get; } =
        clientAuthenticationMethodCatalog;
    private IClientCoherenceValidator ClientCoherenceValidator { get; } = clientCoherenceValidator;

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
                    OpenIdProtocol.ErrorCodes.InvalidClientMetadata
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
        }

        var (authorized, token, initialAccessToken) = await AuthorizeRegistrationAsync(
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
                    OpenIdProtocol.ErrorCodes.InvalidClientMetadata
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

        var settingsJson = BuildSettingsJson(request, resolved);
        var settingsError = ValidateSettings(
            errorFactory,
            settingsJson,
            openIdEnvironment.JsonSerializerOptions
        );
        if (settingsError is not null)
        {
            return settingsError;
        }

        var tenantSettings = openIdContext.Tenant.SettingsProvider.Collection;
        var coherenceError = ValidateCoherence(errorFactory, tenantSettings, settingsJson);
        if (coherenceError is not null)
        {
            return coherenceError.WithStatusCode(StatusCodes.Status400BadRequest).AsHttpResult();
        }

        var isConfidential =
            resolved.TokenEndpointAuthMethod != OpenIdConstants.ClientAuthenticationMethods.None;

        var resourceServerId = initialAccessToken?.ResourceServerId;
        var bindingError = ValidateResourceServerBinding(
            errorFactory,
            request,
            isBound: resourceServerId is not null,
            isConfidential
        );
        if (bindingError is not null)
        {
            return bindingError;
        }

        var protector = await SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForTenant(tenantId),
            [],
            cancellationToken
        );

        var secretsError = BuildClientSecrets(
            protector,
            request,
            IssuesClientSecret(resolved.TokenEndpointAuthMethod),
            createdWhen,
            errorFactory,
            out var secrets,
            out var clientSecret
        );
        if (secretsError is not null)
        {
            return secretsError;
        }

        var client = new PersistedClient
        {
            TenantId = tenantId,
            ClientId = clientId,
            ConcurrencyToken = string.Empty,
            IsDisabled = false,
            ResourceServerId = resourceServerId,
            Settings = new PersistedClientSettings
            {
                TenantId = tenantId,
                ClientId = clientId,
                ConcurrencyToken = string.Empty,
                Value = settingsJson,
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
            if (
                resourceServerId is not null
                && !await CanBindResourceServerAsync(
                    storeManager,
                    tenantId,
                    resourceServerId,
                    cancellationToken
                )
            )
            {
                return errorFactory
                    .InvalidToken()
                    .WithStatusCode(StatusCodes.Status401Unauthorized)
                    .AsHttpResult();
            }

            // The token is spent only after every check has passed, so a refused request leaves it usable; a store
            // failure after the spend leaves it spent (fails closed) (ADR-0192).
            if (
                token is not null
                && !await InitialAccessTokenValidator.TryConsumeAsync(
                    openIdContext,
                    token,
                    cancellationToken
                )
            )
            {
                return errorFactory
                    .InvalidToken()
                    .WithStatusCode(StatusCodes.Status401Unauthorized)
                    .AsHttpResult();
            }

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

        var registrationClientUri = GetRegistrationClientUri(openIdContext, clientId);

        var response = await ToResponseAsync(client, cancellationToken);
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
                clientId,
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

        var response = await ToResponseAsync(client, cancellationToken);
        response = response with
        {
            RegistrationClientUri = GetRegistrationClientUri(openIdContext, clientId),
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
                    OpenIdProtocol.ErrorCodes.InvalidClientMetadata
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
                    OpenIdProtocol.ErrorCodes.InvalidClientMetadata
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

        var settingsJson = BuildSettingsJson(request, resolved);
        var settingsError = ValidateSettings(
            errorFactory,
            settingsJson,
            openIdContext.Environment.JsonSerializerOptions
        );
        if (settingsError is not null)
        {
            return settingsError;
        }

        var tenantSettings = openIdContext.Tenant.SettingsProvider.Collection;
        var coherenceError = ValidateCoherence(errorFactory, tenantSettings, settingsJson);
        if (coherenceError is not null)
        {
            return coherenceError.WithStatusCode(StatusCodes.Status400BadRequest).AsHttpResult();
        }

        var keysError = ImportRegisteredKeys(
            request,
            TimeProvider.GetUtcNow(),
            errorFactory,
            out var keys
        );
        if (keysError is not null)
        {
            return keysError;
        }

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
            return TypedResults.NotFound();

        var bindingError = ValidateResourceServerBinding(
            errorFactory,
            request,
            isBound: client.ResourceServerId is not null,
            isConfidential: resolved.TokenEndpointAuthMethod
                != OpenIdConstants.ClientAuthenticationMethods.None
        );
        if (bindingError is not null)
        {
            return bindingError;
        }

        client.Settings.Value = settingsJson;

        await store.UpdateSettingsAsync(client.Settings, cancellationToken);

        client.Secrets.Value = await ReplacePublicKeysAsync(store, client, keys, cancellationToken);

        await storeManager.SaveChangesAsync(cancellationToken);

        var response = await ToResponseAsync(client, cancellationToken);
        response = response with
        {
            RegistrationClientUri = GetRegistrationClientUri(openIdContext, clientId),
        };

        return TypedResults.Json(response, openIdContext.Environment.JsonSerializerOptions);
    }

    /// <summary>
    /// Replaces the public keys stored for a client with the keys of its updated registration, returning the client's
    /// secrets afterward. An update replaces all of a client's metadata, its keys included (RFC 7592 §2.2): the keys
    /// of the request's <c>jwks</c> take the place of every stored public key, and none remain when the client
    /// publishes a <c>jwks_uri</c> instead. The secrets the client shares with the server are kept.
    /// </summary>
    // See ADR-0161.
    private async ValueTask<List<PersistedSecret>> ReplacePublicKeysAsync(
        IClientStore store,
        PersistedClient client,
        IReadOnlyList<PersistedSecret> keys,
        CancellationToken cancellationToken
    )
    {
        var secrets = new List<PersistedSecret>();
        foreach (var secret in client.Secrets.Value)
        {
            if (
                SecretKeyTypeCatalog.TryGet(secret.SecretType, out var descriptor)
                && descriptor.IsAsymmetric
            )
            {
                await store.RemoveSecretAsync(client.ClientId, secret.SecretId, cancellationToken);
            }
            else
            {
                secrets.Add(secret);
            }
        }

        foreach (var key in keys)
        {
            await store.AddSecretAsync(client.ClientId, key, cancellationToken);
            secrets.Add(key);
        }

        return secrets;
    }

    private async ValueTask<IResult> DeleteClientAsync(
        string clientId,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var client = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (client is null)
            return TypedResults.NotFound();

        // The store removes the client's keys and grants with it, its registration access token included. See ADR-0212.
        await store.RemoveAsync(clientId, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return TypedResults.NoContent();
    }

    // A presented initial access token is always validated, even where anonymous registration is allowed, so a token
    // scoped to a resource server binds the client it registers and an invalid token is never silently ignored
    // (ADR-0192).
    private async ValueTask<(
        bool IsAuthorized,
        string? Token,
        InitialAccessTokenGrant? InitialAccessToken
    )> AuthorizeRegistrationAsync(
        OpenIdContext openIdContext,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var token = GetBearerToken(httpContext);
        if (token is null)
        {
            var allowAnonymous = openIdContext.Tenant.SettingsProvider.Collection.GetValue(
                OpenIdSettingKeys.RegistrationEndpointAllowAnonymous
            );

            return (allowAnonymous, null, null);
        }

        var grant = await InitialAccessTokenValidator.ValidateAsync(
            openIdContext,
            token,
            cancellationToken
        );

        return (grant is not null, token, grant);
    }

    // A client registered with a token scoped to a resource server is bound to it, so the resource server must still
    // exist in the tenant and must not be a system resource server when the client registers (ADR-0168, ADR-0192).
    private static async ValueTask<bool> CanBindResourceServerAsync(
        IStoreManager storeManager,
        string tenantId,
        string resourceServerId,
        CancellationToken cancellationToken
    )
    {
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();
        var resourceServer = await resourceServerStore.GetOrDefaultAsync(
            resourceServerId,
            cancellationToken
        );

        return resourceServer is { IsSystem: false }
            && string.Equals(resourceServer.TenantId, tenantId, StringComparison.Ordinal);
    }

    // A resource-server client introspects for its resource server, which requires a confidential client, and only a
    // resource-server client receives a JWT introspection response, so only it registers how that response is protected
    // (RFC 9701 §6, ADR-0168, ADR-0192).
    private static IResult? ValidateResourceServerBinding(
        IOpenIdErrorFactory errorFactory,
        RegisterClientRequest request,
        bool isBound,
        bool isConfidential
    )
    {
        string? description = null;
        if (isBound && !isConfidential)
        {
            description =
                "A client bound to a resource server must be confidential; 'token_endpoint_auth_method' cannot be 'none'.";
        }
        else if (
            !isBound
            && (
                request.IntrospectionSignedResponseAlg is not null
                || request.IntrospectionEncryptedResponseAlg is not null
                || request.IntrospectionEncryptedResponseEnc is not null
            )
        )
        {
            description =
                "Only a client bound to a resource server receives JWT introspection responses, so only it registers the 'introspection_*_response_*' metadata.";
        }

        return description is null
            ? null
            : errorFactory
                .InvalidRequest(description, OpenIdProtocol.ErrorCodes.InvalidClientMetadata)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .AsHttpResult();
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

    private string GetRegistrationClientUri(OpenIdContext openIdContext, string clientId) =>
        LinkGenerator.GetTenantUriByName(
            openIdContext,
            OpenIdConstants.EndpointNames.RegistrationManagement,
            new { client_id = clientId }
        )
        ?? throw new InvalidOperationException("Unable to determine the registration client URI.");

    /// <summary>
    /// Validates the submitted client metadata and resolves the effective values (defaults applied). The resolved grant
    /// types and response types are consistent with each other: every grant type a registered response type uses is
    /// registered.
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

        // The method vocabulary is open: the settings validation accepts every method a registered method descriptor
        // names, and the tenant's effective token_endpoint_auth_methods_supported narrows it (ValidateCoherence).
        if (string.IsNullOrEmpty(tokenEndpointAuthMethod))
        {
            return errorFactory.InvalidRequest(
                "The 'token_endpoint_auth_method' value must be a non-empty string.",
                OpenIdProtocol.ErrorCodes.InvalidClientMetadata
            );
        }

        if (
            request.ResponseTypes is { } requestedValues
            && requestedValues.Any(string.IsNullOrEmpty)
        )
        {
            return errorFactory.InvalidRequest(
                "Every 'response_types' value must be a non-empty string.",
                OpenIdProtocol.ErrorCodes.InvalidClientMetadata
            );
        }

        // A response type is stored in the canonical form the server compares, because the order of its names does
        // not matter (RFC 6749 §3.1.1).
        var requestedResponseTypes = request.ResponseTypes is { Count: > 0 }
            ? CanonicalizeResponseTypes(request.ResponseTypes)
            : null;

        var grantTypes = request.GrantTypes is { Count: > 0 }
            ? request.GrantTypes
            : GetDefaultGrantTypes(requestedResponseTypes);

        var responseTypes = requestedResponseTypes ?? GetDefaultResponseTypes(grantTypes);

        var consistencyError = ValidateGrantTypesOfResponseTypes(
            errorFactory,
            grantTypes,
            responseTypes
        );
        if (consistencyError is not null)
        {
            return consistencyError;
        }

        var redirectUris = request.RedirectUris ?? [];

        // The authorization endpoint returns its response to a redirection URI, so a client that registers a response
        // type registers at least one.
        if (responseTypes.Length > 0 && redirectUris.Count == 0)
        {
            return errorFactory.InvalidRequest(
                "At least one 'redirect_uris' value is required for the registered response types.",
                OpenIdProtocol.ErrorCodes.InvalidRedirectUri
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
                    OpenIdProtocol.ErrorCodes.InvalidRedirectUri
                );
            }
        }

        var subjectType = request.SubjectType;
        if (!string.IsNullOrEmpty(subjectType) && !SupportedSubjectTypes.Contains(subjectType))
        {
            return errorFactory.InvalidRequest(
                $"The 'subject_type' value '{subjectType}' is not supported.",
                OpenIdProtocol.ErrorCodes.InvalidClientMetadata
            );
        }

        var keysError =
            ValidateKeyMetadata(errorFactory, request)
            ?? ValidateAuthenticationMetadata(errorFactory, request, tokenEndpointAuthMethod);
        if (keysError is not null)
        {
            return keysError;
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
                OpenIdProtocol.ErrorCodes.InvalidClientMetadata
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

    private static string[] CanonicalizeResponseTypes(IReadOnlyList<string> responseTypes) =>
        [
            .. responseTypes
                .Select(responseType => ResponseTypeValues.Canonicalize(responseType))
                .Distinct(StringComparer.Ordinal),
        ];

    // RFC 7591 §2: omitted grant_types default to authorization_code. A client that registers response types defaults
    // instead to the grant types they use, so a default never makes the registration inconsistent (RFC 7591 §2.1).
    private static string[] GetDefaultGrantTypes(IReadOnlyList<string>? responseTypes) =>
        responseTypes is null
            ? DefaultGrantTypes
            :
            [
                .. responseTypes
                    .SelectMany(responseType => ResponseTypeValues.GetGrantTypes(responseType))
                    .Distinct(StringComparer.Ordinal),
            ];

    // RFC 7591 §2: omitted response_types default to code. A client whose grant types do not use the authorization
    // endpoint (neither authorization_code nor implicit) registers no response type.
    private static string[] GetDefaultResponseTypes(IReadOnlyList<string> grantTypes) =>
        grantTypes.Contains(OpenIdProtocol.GrantTypes.AuthorizationCode)
        || grantTypes.Contains(OpenIdProtocol.GrantTypes.Implicit)
            ? DefaultResponseTypes
            : [];

    /// <summary>
    /// Verifies that every grant type a registered response type uses is registered (OpenID Connect Dynamic Client
    /// Registration 1.0 §2), returning an <c>invalid_client_metadata</c> error for the first response type whose grant
    /// type is missing (RFC 7591 §2.1), or <see langword="null"/> when the registration is consistent. Without the grant
    /// type the authorization endpoint would refuse the response type, so the client could never use it.
    /// </summary>
    private static IOpenIdError? ValidateGrantTypesOfResponseTypes(
        IOpenIdErrorFactory errorFactory,
        IReadOnlyList<string> grantTypes,
        IReadOnlyList<string> responseTypes
    )
    {
        foreach (var responseType in responseTypes)
        {
            var requiredGrantTypes = ResponseTypeValues.GetGrantTypes(responseType);
            var missingGrantType = requiredGrantTypes.FirstOrDefault(grantType =>
                !grantTypes.Contains(grantType, StringComparer.Ordinal)
            );
            if (missingGrantType is null)
                continue;

            return errorFactory.InvalidRequest(
                $"The 'response_types' value '{responseType}' requires the '{missingGrantType}' grant type, which is not among the registered 'grant_types'.",
                OpenIdProtocol.ErrorCodes.InvalidClientMetadata
            );
        }

        return null;
    }

    /// <summary>
    /// Verifies that the settings of the client being registered satisfy every registered
    /// <see cref="IClientCoherenceRule"/>, including the values the client's own lists register: the validator
    /// resolves the client's settings document onto the tenant's effective settings as the runtime resolves it, so the
    /// tenant's ceilings (and those of the security profiles it selects) decide which values the client may register.
    /// Returns an <c>invalid_redirect_uri</c> error (RFC 7591 §3.2.2) when a redirect URI is refused, else an
    /// <c>invalid_client_metadata</c> error, or <see langword="null"/> when the client settings are coherent.
    /// </summary>
    /// <param name="errorFactory">The <see cref="IOpenIdErrorFactory"/> that creates the error.</param>
    /// <param name="tenantSettings">The effective settings of the tenant the client registers with.</param>
    /// <param name="settingsJson">The client's settings document.</param>
    /// <returns>The error, or <see langword="null"/> when the client settings are coherent.</returns>
    // See ADR-0206.
    internal virtual IOpenIdError? ValidateCoherence(
        IOpenIdErrorFactory errorFactory,
        IReadOnlySettingCollection tenantSettings,
        JsonElement settingsJson
    )
    {
        var violations = ClientCoherenceValidator.Validate(tenantSettings, settingsJson);
        if (violations.Count == 0)
        {
            return null;
        }

        var description = string.Join(" ", violations.Select(violation => violation.Description));
        var code = violations.Any(violation =>
            string.Equals(
                violation.SettingName,
                OpenIdSettingNames.RedirectUris,
                StringComparison.Ordinal
            )
        )
            ? OpenIdProtocol.ErrorCodes.InvalidRedirectUri
            : OpenIdProtocol.ErrorCodes.InvalidClientMetadata;

        return errorFactory.InvalidRequest(description, code);
    }

    /// <summary>
    /// Verifies the key and encryption metadata of a registration request (OpenID Connect Dynamic Client Registration
    /// 1.0 §2): <c>jwks</c> and <c>jwks_uri</c> are not both present, an <c>*_enc</c> value is registered only with its
    /// <c>*_alg</c>, and a client that registers an encrypted response publishes the public keys it is encrypted to.
    /// Returns an <c>invalid_client_metadata</c> error, or <see langword="null"/> when the metadata is consistent.
    /// </summary>
    // See ADR-0155.
    internal virtual IOpenIdError? ValidateKeyMetadata(
        IOpenIdErrorFactory errorFactory,
        RegisterClientRequest request
    )
    {
        var hasJwks = request.Jwks is { ValueKind: JsonValueKind.Object };
        var hasJwksUri = !string.IsNullOrEmpty(request.JwksUri);
        if (hasJwks && hasJwksUri)
        {
            return InvalidClientMetadata(
                errorFactory,
                "The 'jwks_uri' and 'jwks' parameters must not both be present."
            );
        }

        (string AlgName, string? Alg, string EncName, string? Enc)[] encryptions =
        [
            (
                OpenIdSettingNames.IdTokenEncryptedResponseAlg,
                request.IdTokenEncryptedResponseAlg,
                OpenIdSettingNames.IdTokenEncryptedResponseEnc,
                request.IdTokenEncryptedResponseEnc
            ),
            (
                OpenIdSettingNames.UserInfoEncryptedResponseAlg,
                request.UserInfoEncryptedResponseAlg,
                OpenIdSettingNames.UserInfoEncryptedResponseEnc,
                request.UserInfoEncryptedResponseEnc
            ),
            (
                OpenIdSettingNames.IntrospectionEncryptedResponseAlg,
                request.IntrospectionEncryptedResponseAlg,
                OpenIdSettingNames.IntrospectionEncryptedResponseEnc,
                request.IntrospectionEncryptedResponseEnc
            ),
            (
                OpenIdSettingNames.AuthorizationEncryptedResponseAlg,
                request.AuthorizationEncryptedResponseAlg,
                OpenIdSettingNames.AuthorizationEncryptedResponseEnc,
                request.AuthorizationEncryptedResponseEnc
            ),
            (
                RequestObjectEncryptionAlgMetadata,
                request.RequestObjectEncryptionAlg,
                RequestObjectEncryptionEncMetadata,
                request.RequestObjectEncryptionEnc
            ),
        ];

        foreach (var (algName, alg, encName, enc) in encryptions)
        {
            if (!string.IsNullOrEmpty(enc) && string.IsNullOrEmpty(alg))
            {
                return InvalidClientMetadata(
                    errorFactory,
                    $"The '{encName}' parameter requires '{algName}'."
                );
            }
        }

        // A response is encrypted to a public key the client publishes; without one it could never be delivered.
        var registersEncryptedResponse =
            !string.IsNullOrEmpty(request.IdTokenEncryptedResponseAlg)
            || !string.IsNullOrEmpty(request.UserInfoEncryptedResponseAlg)
            || !string.IsNullOrEmpty(request.IntrospectionEncryptedResponseAlg)
            || !string.IsNullOrEmpty(request.AuthorizationEncryptedResponseAlg);
        if (registersEncryptedResponse && !hasJwks && !hasJwksUri)
        {
            return InvalidClientMetadata(
                errorFactory,
                "An encrypted response requires the client's public keys in 'jwks' or 'jwks_uri'."
            );
        }

        return null;
    }

    /// <summary>
    /// Verifies that a registration request carries what its client-authentication method verifies against (RFC 8705
    /// §2, RFC 7523 §2.2): the public keys in <c>jwks</c> or <c>jwks_uri</c> for <c>private_key_jwt</c>; those keys,
    /// with a registered certificate when given by value, for <c>self_signed_tls_client_auth</c>; and exactly one
    /// <c>tls_client_auth_*</c> certificate identity for <c>tls_client_auth</c>, which is refused for any other method.
    /// A registering client never supplies trust anchors: a <c>tls_client_auth</c> certificate chains to the tenant's
    /// anchors, and with none configured the method fails closed (ADR-0173). Returns an <c>invalid_client_metadata</c>
    /// error, or <see langword="null"/> when the metadata is consistent.
    /// </summary>
    /// <param name="errorFactory">The <see cref="IOpenIdErrorFactory"/> that creates the error.</param>
    /// <param name="request">The registration request.</param>
    /// <param name="tokenEndpointAuthMethod">The client-authentication method the client registers.</param>
    /// <returns>The error, or <see langword="null"/> when the metadata is consistent.</returns>
    // See ADR-0145.
    internal virtual IOpenIdError? ValidateAuthenticationMetadata(
        IOpenIdErrorFactory errorFactory,
        RegisterClientRequest request,
        string tokenEndpointAuthMethod
    )
    {
        var hasJwks = request.Jwks is { ValueKind: JsonValueKind.Object };
        var hasJwksUri = !string.IsNullOrEmpty(request.JwksUri);
        var hasCertificate =
            request.Jwks is { ValueKind: JsonValueKind.Object } jwks && HasCertificate(jwks);

        string?[] identities =
        [
            request.TlsClientAuthSubjectDn,
            request.TlsClientAuthSanDns,
            request.TlsClientAuthSanUri,
            request.TlsClientAuthSanIp,
            request.TlsClientAuthSanEmail,
        ];
        var identityCount = identities.Count(identity => !string.IsNullOrEmpty(identity));

        switch (tokenEndpointAuthMethod)
        {
            case OpenIdConstants.ClientAuthenticationMethods.PrivateKeyJwt
                when !hasJwks && !hasJwksUri:
                return InvalidClientMetadata(
                    errorFactory,
                    "The 'private_key_jwt' method requires the client's public keys in 'jwks' or 'jwks_uri'."
                );

            case OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth
                when !hasJwks && !hasJwksUri:
                return InvalidClientMetadata(
                    errorFactory,
                    "The 'self_signed_tls_client_auth' method requires the client's certificates in 'jwks' or 'jwks_uri'."
                );

            case OpenIdConstants.ClientAuthenticationMethods.SelfSignedTlsClientAuth
                when hasJwks && !hasCertificate:
                return InvalidClientMetadata(
                    errorFactory,
                    "The 'self_signed_tls_client_auth' method requires a 'jwks' key with an 'x5c' certificate."
                );

            case OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth when identityCount != 1:
                return InvalidClientMetadata(
                    errorFactory,
                    "The 'tls_client_auth' method requires exactly one of 'tls_client_auth_subject_dn', "
                        + "'tls_client_auth_san_dns', 'tls_client_auth_san_uri', 'tls_client_auth_san_ip', or "
                        + "'tls_client_auth_san_email'."
                );

            case not OpenIdConstants.ClientAuthenticationMethods.TlsClientAuth
                when identityCount != 0:
                return InvalidClientMetadata(
                    errorFactory,
                    "The 'tls_client_auth_*' parameters apply only to the 'tls_client_auth' method."
                );
        }

        return null;
    }

    private static bool HasCertificate(JsonElement jwks) =>
        jwks.TryGetProperty("keys", out var keys)
        && keys.ValueKind == JsonValueKind.Array
        && keys.EnumerateArray()
            .Any(key =>
                key.ValueKind == JsonValueKind.Object
                && key.TryGetProperty("x5c", out var x5c)
                && x5c.ValueKind == JsonValueKind.Array
                && x5c.GetArrayLength() > 0
            );

    /// <summary>
    /// Determines whether the registration issues a <c>client_secret</c> to a client that registers the specified
    /// client-authentication method: the client is issued one when the method's descriptor requires a client secret;
    /// a method that is not registered receives none.
    /// </summary>
    /// <param name="tokenEndpointAuthMethod">The client-authentication method the client registers.</param>
    /// <returns><see langword="true"/> when the client is issued a <c>client_secret</c>.</returns>
    // See ADR-0145 and ADR-0207.
    internal virtual bool IssuesClientSecret(string tokenEndpointAuthMethod) =>
        ClientAuthenticationMethodCatalog.TryGet(tokenEndpointAuthMethod, out var descriptor)
        && descriptor.RequiresClientSecret;

    private static IOpenIdError InvalidClientMetadata(
        IOpenIdErrorFactory errorFactory,
        string description
    ) => errorFactory.InvalidRequest(description, OpenIdProtocol.ErrorCodes.InvalidClientMetadata);

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
    /// Builds the new client's secrets: a generated symmetric secret for a client whose method verifies one, protected
    /// under the tenant's master key by the given protector, plus the public keys of the <c>jwks</c> set the client
    /// registers by value, imported under the client-key policy. Returns an error result when the set is refused;
    /// otherwise <see langword="null"/> with the secrets and one-time client secret.
    /// </summary>
    private IResult? BuildClientSecrets(
        ISecretProtector protector,
        RegisterClientRequest request,
        bool issuesClientSecret,
        DateTimeOffset createdWhen,
        IOpenIdErrorFactory errorFactory,
        out List<PersistedSecret> secrets,
        out string? clientSecret
    )
    {
        secrets = [];
        clientSecret = null;

        if (issuesClientSecret)
        {
            // A client_secret is an opaque string the client later presents verbatim. GenerateSharedSecret stores it
            // as the UTF-8 bytes client authentication compares against (see CommonClientAuthenticationHandler.
            // IsSecretEqual), unlike GenerateSecret's raw-byte crypto-key material whose base64url reveal never
            // round-trips back to the stored bytes.
            var generated = SecretGenerator.GenerateSharedSecret(
                protector,
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

        var keysError = ImportRegisteredKeys(request, createdWhen, errorFactory, out var keys);
        if (keysError is not null)
        {
            return keysError;
        }

        secrets.AddRange(keys);
        return null;
    }

    /// <summary>
    /// Imports the public keys of the <c>jwks</c> set a registration request carries, under the client-key policy: every
    /// key is a public key of a supported type with a <c>kid</c> unique within the set and metadata that agrees with the
    /// key. Returns an <c>invalid_client_metadata</c> error result when the set is refused; otherwise
    /// <see langword="null"/> with the imported keys, which are empty when the request carries no <c>jwks</c>.
    /// </summary>
    // See ADR-0161.
    private IResult? ImportRegisteredKeys(
        RegisterClientRequest request,
        DateTimeOffset createdWhen,
        IOpenIdErrorFactory errorFactory,
        out IReadOnlyList<PersistedSecret> keys
    )
    {
        keys = [];

        if (request.Jwks is not { } jwks || jwks.IsNullOrUndefined())
        {
            return null;
        }

        if (
            ClientKeyImporter.TryImportRegisteredKeySet(
                jwks,
                createdWhen,
                out var importedKeys,
                out var error
            )
        )
        {
            keys = importedKeys;
            return null;
        }

        return errorFactory
            .InvalidRequest(
                $"The 'jwks' parameter is invalid. {error}",
                OpenIdProtocol.ErrorCodes.InvalidClientMetadata
            )
            .WithStatusCode(StatusCodes.Status400BadRequest)
            .AsHttpResult();
    }

    private JsonElement BuildSettingsJson(RegisterClientRequest request, ResolvedMetadata resolved)
    {
        var settings = BuildSettingsObject(request, resolved);
        return JsonSerializer.SerializeToElement(settings);
    }

    /// <summary>
    /// Validates the settings document a registration stores against the registered setting descriptors, returning an
    /// error result when the document cannot be honored — a value outside a setting's domain, such as an unsupported
    /// grant type or a <c>jwks_uri</c> that is not https — so the registration is refused rather than stored as a client
    /// that fails every request that loads it; otherwise <see langword="null"/>.
    /// </summary>
    // See ADR-0140.
    private IResult? ValidateSettings(
        IOpenIdErrorFactory errorFactory,
        JsonElement settingsJson,
        JsonSerializerOptions jsonOptions
    )
    {
        var settingErrors = SettingSerializer.ValidateSettings(
            settingsJson,
            OpenIdSettingScopes.Client,
            jsonOptions
        );
        if (settingErrors.Count == 0)
            return null;

        // RFC 7591 §3.2.2 reserves invalid_redirect_uri for the redirection URIs; every other value is client metadata.
        var isRedirectUriError = settingErrors.Any(error =>
            string.Equals(
                error.SettingName,
                OpenIdSettingNames.RedirectUris,
                StringComparison.Ordinal
            )
        );
        var errorCode = isRedirectUriError
            ? OpenIdProtocol.ErrorCodes.InvalidRedirectUri
            : OpenIdProtocol.ErrorCodes.InvalidClientMetadata;

        var messages = settingErrors.Select(error => error.Message);
        var description = string.Join(" ", messages);

        return errorFactory
            .InvalidRequest(description, errorCode)
            .WithStatusCode(StatusCodes.Status400BadRequest)
            .AsHttpResult();
    }

    /// <summary>
    /// Projects the submitted metadata and the resolved values into the client's persisted settings document. Every
    /// key is a registered client setting. The requested <c>scope</c> is not stored: the scopes a client may request
    /// come from the tenant's resource servers and the client's grants, and the registration response reports those.
    /// The <c>jwks</c> set is not stored either: its keys are imported as the client's secrets, and the registration
    /// response projects them back.
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
            [OpenIdSettingNames.TokenEndpointAuthMethod] = resolved.TokenEndpointAuthMethod,
        };

        SetIfPresent(settings, OpenIdSettingNames.SubjectType, resolved.SubjectType);
        SetIfPresent(
            settings,
            OpenIdSettingNames.SectorIdentifierUri,
            resolved.SectorIdentifierUri
        );

        SetIfPresent(settings, OpenIdSettingNames.ClientName, request.ClientName);
        SetIfPresent(settings, OpenIdSettingNames.ClientUri, request.ClientUri);
        SetIfPresent(settings, OpenIdSettingNames.LogoUri, request.LogoUri);
        SetIfPresent(settings, OpenIdSettingNames.TosUri, request.TosUri);
        SetIfPresent(settings, OpenIdSettingNames.PolicyUri, request.PolicyUri);
        SetIfPresent(settings, OpenIdSettingNames.JwksUri, request.JwksUri);
        SetIfPresent(
            settings,
            OpenIdSettingNames.TlsClientAuthSubjectDn,
            request.TlsClientAuthSubjectDn
        );
        SetIfPresent(settings, OpenIdSettingNames.TlsClientAuthSanDns, request.TlsClientAuthSanDns);
        SetIfPresent(settings, OpenIdSettingNames.TlsClientAuthSanUri, request.TlsClientAuthSanUri);
        SetIfPresent(settings, OpenIdSettingNames.TlsClientAuthSanIp, request.TlsClientAuthSanIp);
        SetIfPresent(
            settings,
            OpenIdSettingNames.TlsClientAuthSanEmail,
            request.TlsClientAuthSanEmail
        );

        // The RFC 8705 §3.4 member is the client's requirement. ValidateCoherence refuses it when the tenant does not
        // support binding.
        if (request.TlsClientCertificateBoundAccessTokens is { } bound)
            settings[OpenIdSettingNames.RequireTlsClientCertificateBoundAccessTokens] = bound;

        // The RFC 9449 §5.2 member is the client's requirement. ValidateCoherence refuses it when the tenant permits
        // no DPoP proof algorithm.
        if (request.DPoPBoundAccessTokens is { } dpopBound)
            settings[OpenIdSettingNames.RequireDPoPBoundAccessTokens] = dpopBound;

        SetIfPresent(settings, OpenIdSettingNames.SoftwareId, request.SoftwareId);
        SetIfPresent(settings, OpenIdSettingNames.SoftwareVersion, request.SoftwareVersion);

        if (request.Contacts is { Count: > 0 })
            settings[OpenIdSettingNames.Contacts] = ToJsonArray(request.Contacts);

        SetIfPresent(
            settings,
            OpenIdSettingNames.IdTokenSignedResponseAlg,
            request.IdTokenSignedResponseAlg
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.IdTokenEncryptedResponseAlg,
            request.IdTokenEncryptedResponseAlg
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.IdTokenEncryptedResponseEnc,
            request.IdTokenEncryptedResponseEnc
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.UserInfoSignedResponseAlg,
            request.UserInfoSignedResponseAlg
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.UserInfoEncryptedResponseAlg,
            request.UserInfoEncryptedResponseAlg
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.UserInfoEncryptedResponseEnc,
            request.UserInfoEncryptedResponseEnc
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.IntrospectionSignedResponseAlg,
            request.IntrospectionSignedResponseAlg
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.IntrospectionEncryptedResponseAlg,
            request.IntrospectionEncryptedResponseAlg
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.IntrospectionEncryptedResponseEnc,
            request.IntrospectionEncryptedResponseEnc
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.AuthorizationSignedResponseAlg,
            request.AuthorizationSignedResponseAlg
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.AuthorizationEncryptedResponseAlg,
            request.AuthorizationEncryptedResponseAlg
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.AuthorizationEncryptedResponseEnc,
            request.AuthorizationEncryptedResponseEnc
        );

        SetListIfPresent(
            settings,
            OpenIdSettingNames.RequestObjectSigningAlgValuesSupported,
            request.RequestObjectSigningAlg
        );
        SetListIfPresent(
            settings,
            OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported,
            request.RequestObjectEncryptionAlg
        );
        SetListIfPresent(
            settings,
            OpenIdSettingNames.RequestObjectEncryptionEncValuesSupported,
            request.RequestObjectEncryptionEnc
        );

        SetListIfPresent(
            settings,
            OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported,
            request.TokenEndpointAuthSigningAlg
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.BackchannelTokenDeliveryMode,
            request.BackchannelTokenDeliveryMode
        );
        SetIfPresent(
            settings,
            OpenIdSettingNames.ClientNotificationEndpoint,
            request.BackchannelClientNotificationEndpoint
        );
        SetListIfPresent(
            settings,
            OpenIdSettingNames.BackchannelAuthenticationRequestSigningAlgValuesSupported,
            request.BackchannelAuthenticationRequestSigningAlg
        );
        if (request.BackchannelUserCodeParameter is { } userCodeParameter)
            settings[OpenIdSettingNames.BackchannelUserCodeParameterSupported] = userCodeParameter;

        return settings;
    }

    /// <summary>
    /// Builds the client information response for a persisted client: the registered metadata reconstructed from its
    /// settings document, and a <c>scope</c> listing the scopes the client may currently request — the scopes of the
    /// tenant's system resource servers and of the resource servers the client is granted. A requested <c>scope</c>
    /// is replaced by that set (RFC 7591 §3.2.1), so the response never claims a scope the client cannot obtain.
    /// </summary>
    internal virtual async ValueTask<RegisterClientResponse> ToResponseAsync(
        PersistedClient client,
        CancellationToken cancellationToken
    )
    {
        var allowedScopes = await ClientScopeService.GetAllowedScopesAsync(
            client.TenantId,
            client.ClientId,
            cancellationToken
        );

        var orderedScopes = allowedScopes.Order(StringComparer.Ordinal);
        var scope =
            allowedScopes.Count == 0
                ? null
                : string.Join(OpenIdProtocol.ParameterSeparatorString, orderedScopes);

        return ToResponse(client) with
        {
            Scope = scope,
        };
    }

    /// <summary>
    /// Reconstructs the registered metadata of a client information response from a persisted client: the metadata
    /// from its settings document, and the <c>jwks</c> set from the public keys stored for it, which are read without
    /// a protector.
    /// </summary>
    internal virtual RegisterClientResponse ToResponse(PersistedClient client)
    {
        var settings = ToJsonObject(client.Settings.Value);
        var jwks = ExportPublicKeys(client.Secrets.Value);

        return new RegisterClientResponse
        {
            ClientId = client.ClientId,
            ResourceServerId = client.ResourceServerId,
            RedirectUris = ReadStringArray(settings, OpenIdSettingNames.RedirectUris),
            GrantTypes = ReadStringArray(settings, OpenIdSettingNames.GrantTypesSupported),
            ResponseTypes = ReadStringArray(settings, OpenIdSettingNames.ResponseTypesSupported),
            TokenEndpointAuthMethod = ReadString(
                settings,
                OpenIdSettingNames.TokenEndpointAuthMethod
            ),
            SubjectType = ReadString(settings, OpenIdSettingNames.SubjectType),
            SectorIdentifierUri = ReadString(settings, OpenIdSettingNames.SectorIdentifierUri),
            ClientName = ReadString(settings, OpenIdSettingNames.ClientName),
            ClientUri = ReadString(settings, OpenIdSettingNames.ClientUri),
            LogoUri = ReadString(settings, OpenIdSettingNames.LogoUri),
            Contacts = ReadStringArray(settings, OpenIdSettingNames.Contacts),
            TosUri = ReadString(settings, OpenIdSettingNames.TosUri),
            PolicyUri = ReadString(settings, OpenIdSettingNames.PolicyUri),
            JwksUri = ReadString(settings, OpenIdSettingNames.JwksUri),
            TlsClientAuthSubjectDn = ReadString(
                settings,
                OpenIdSettingNames.TlsClientAuthSubjectDn
            ),
            TlsClientAuthSanDns = ReadString(settings, OpenIdSettingNames.TlsClientAuthSanDns),
            TlsClientAuthSanUri = ReadString(settings, OpenIdSettingNames.TlsClientAuthSanUri),
            TlsClientAuthSanIp = ReadString(settings, OpenIdSettingNames.TlsClientAuthSanIp),
            TlsClientAuthSanEmail = ReadString(settings, OpenIdSettingNames.TlsClientAuthSanEmail),
            TlsClientCertificateBoundAccessTokens = ReadBoolean(
                settings,
                OpenIdSettingNames.RequireTlsClientCertificateBoundAccessTokens
            ),
            DPoPBoundAccessTokens = ReadBoolean(
                settings,
                OpenIdSettingNames.RequireDPoPBoundAccessTokens
            ),
            Jwks = jwks,
            SoftwareId = ReadString(settings, OpenIdSettingNames.SoftwareId),
            SoftwareVersion = ReadString(settings, OpenIdSettingNames.SoftwareVersion),
            IdTokenSignedResponseAlg = ReadString(
                settings,
                OpenIdSettingNames.IdTokenSignedResponseAlg
            ),
            IdTokenEncryptedResponseAlg = ReadString(
                settings,
                OpenIdSettingNames.IdTokenEncryptedResponseAlg
            ),
            IdTokenEncryptedResponseEnc = ReadString(
                settings,
                OpenIdSettingNames.IdTokenEncryptedResponseEnc
            ),
            UserInfoSignedResponseAlg = ReadString(
                settings,
                OpenIdSettingNames.UserInfoSignedResponseAlg
            ),
            UserInfoEncryptedResponseAlg = ReadString(
                settings,
                OpenIdSettingNames.UserInfoEncryptedResponseAlg
            ),
            UserInfoEncryptedResponseEnc = ReadString(
                settings,
                OpenIdSettingNames.UserInfoEncryptedResponseEnc
            ),
            IntrospectionSignedResponseAlg = ReadString(
                settings,
                OpenIdSettingNames.IntrospectionSignedResponseAlg
            ),
            IntrospectionEncryptedResponseAlg = ReadString(
                settings,
                OpenIdSettingNames.IntrospectionEncryptedResponseAlg
            ),
            IntrospectionEncryptedResponseEnc = ReadString(
                settings,
                OpenIdSettingNames.IntrospectionEncryptedResponseEnc
            ),
            AuthorizationSignedResponseAlg = ReadString(
                settings,
                OpenIdSettingNames.AuthorizationSignedResponseAlg
            ),
            AuthorizationEncryptedResponseAlg = ReadString(
                settings,
                OpenIdSettingNames.AuthorizationEncryptedResponseAlg
            ),
            AuthorizationEncryptedResponseEnc = ReadString(
                settings,
                OpenIdSettingNames.AuthorizationEncryptedResponseEnc
            ),
            RequestObjectSigningAlg = ReadSingleValue(
                settings,
                OpenIdSettingNames.RequestObjectSigningAlgValuesSupported
            ),
            RequestObjectEncryptionAlg = ReadSingleValue(
                settings,
                OpenIdSettingNames.RequestObjectEncryptionAlgValuesSupported
            ),
            RequestObjectEncryptionEnc = ReadSingleValue(
                settings,
                OpenIdSettingNames.RequestObjectEncryptionEncValuesSupported
            ),
            TokenEndpointAuthSigningAlg = ReadSingleValue(
                settings,
                OpenIdSettingNames.TokenEndpointAuthSigningAlgValuesSupported
            ),
            BackchannelTokenDeliveryMode = ReadString(
                settings,
                OpenIdSettingNames.BackchannelTokenDeliveryMode
            ),
            BackchannelClientNotificationEndpoint = ReadString(
                settings,
                OpenIdSettingNames.ClientNotificationEndpoint
            ),
            BackchannelAuthenticationRequestSigningAlg = ReadSingleValue(
                settings,
                OpenIdSettingNames.BackchannelAuthenticationRequestSigningAlgValuesSupported
            ),
            BackchannelUserCodeParameter = ReadBoolean(
                settings,
                OpenIdSettingNames.BackchannelUserCodeParameterSupported
            ),
        };
    }

    /// <summary>
    /// Projects the public keys stored for a client into the JSON Web Key Set its registration reports, or returns
    /// <see langword="null"/> when it has none. A secret without readable public key material — the client secret, or
    /// a private key stored without its public key — is skipped and never deserialized.
    /// </summary>
    // See ADR-0161 and ADR-0268.
    private JsonElement? ExportPublicKeys(IEnumerable<PersistedSecret> secrets)
    {
        var keys = new List<JsonWebKey>();
        foreach (var secret in secrets)
        {
            if (
                !SecretKeyTypeCatalog.TryGet(secret.SecretType, out var descriptor)
                || !descriptor.SupportsPublicKeyExport
                || !secret.HasPublicKey()
            )
            {
                continue;
            }

            var secretKey = SecretSerializer.DeserializePublicSecret(secret);
            var exported = descriptor.ExportPublicKey(secretKey);
            if (exported is not null)
                keys.Add(exported.Jwk);
        }

        if (keys.Count == 0)
            return null;

        var keySet = new JsonWebKeySet(keys);
        return JsonSerializer.SerializeToElement(keySet);
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

    // A single registered value narrows the client's own list to it, which the endpoint enforces.
    private static void SetListIfPresent(JsonObject settings, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
            settings[key] = new JsonArray(JsonValue.Create(value));
    }

    // A list narrowed to one value reports it as the registered metadata; any other list names no single registration.
    private static string? ReadSingleValue(JsonObject settings, string key) =>
        ReadStringArray(settings, key) is [var value] ? value : null;

    private static JsonObject ToJsonObject(JsonElement element) =>
        element.ValueKind == JsonValueKind.Object ? JsonObject.Create(element) ?? [] : [];

    // A stored empty list is reported as empty, not omitted: an omitted 'response_types' or 'grant_types' would read as
    // its RFC 7591 default rather than as what is registered.
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

        return values;
    }

    private static bool? ReadBoolean(JsonObject settings, string key) =>
        settings[key]?.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null,
        };

    private static string? ReadString(JsonObject settings, string key)
    {
        var node = settings[key];
        return node?.GetValueKind() == JsonValueKind.String ? node.GetValue<string>() : null;
    }
}
