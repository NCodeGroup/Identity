#region Copyright Preamble

//
//    Copyright @ 2026 NCode Group
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
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Authorization;

/// <summary>
/// Provides a default implementation of the <see cref="IAuthorizationResponseDeliveryResolver"/> abstraction.
/// </summary>
internal class DefaultAuthorizationResponseDeliveryResolver(
    IAuthorizationResponseModeProvider responseModeProvider,
    IClientJwtCredentialSelector clientJwtCredentialSelector,
    IRedirectUriKindCatalog redirectUriKindCatalog
) : IAuthorizationResponseDeliveryResolver
{
    private IAuthorizationResponseModeProvider ResponseModeProvider { get; } = responseModeProvider;

    private IClientJwtCredentialSelector ClientJwtCredentialSelector { get; } =
        clientJwtCredentialSelector;

    private IRedirectUriKindCatalog RedirectUriKindCatalog { get; } = redirectUriKindCatalog;

    /// <inheritdoc />
    public async ValueTask<AuthorizationResponseDelivery> ResolveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IRequestValues requestValues,
        CancellationToken cancellationToken
    )
    {
        var state = GetState(requestValues);

        // The default follows the response type, so a response that returns a token is never redirected in a query
        // string, whether it carries the tokens or an error (ADR-0141); the JARM 'jwt' shorthand resolves the same way.
        requestValues.TryGetValue(
            OpenIdProtocol.Parameters.ResponseType,
            out var responseTypeStringValues
        );
        var responseTypes = SplitResponseTypeNames(responseTypeStringValues);

        var requestedResponseMode = GetResponseMode(openIdContext, requestValues, state);
        var handler = ResolveResponseModeHandler(
            openIdContext,
            openIdClient,
            requestedResponseMode,
            responseTypes,
            state
        );

        var redirectUriValue = GetRedirectUri(openIdContext, requestValues, state);
        var redirectUri = CheckRedirectUri(openIdContext, openIdClient, redirectUriValue, state);

        return await CreateDeliveryAsync(
            openIdContext,
            openIdClient,
            redirectUri,
            handler,
            state,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<AuthorizationResponseDelivery> ResolveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        CancellationToken cancellationToken
    )
    {
        // RFC 9101 §6.3: once the request object is verified, only its values are used.
        var values =
            (IBaseAuthorizationRequestValues?)authorizationRequest.OriginalRequestObject
            ?? authorizationRequest.OriginalRequestMessage;

        var state = string.IsNullOrEmpty(values.State) ? null : values.State;
        var handler = ResolveResponseModeHandler(
            openIdContext,
            openIdClient,
            values.ResponseMode,
            values.ResponseTypes ?? [],
            state
        );
        var redirectUri = CheckRedirectUri(openIdContext, openIdClient, values.RedirectUri, state);

        return await CreateDeliveryAsync(
            openIdContext,
            openIdClient,
            redirectUri,
            handler,
            state,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<AuthorizationResponseDelivery> ResolveAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        IAuthorizationRequest authorizationRequest,
        string checkedRedirectUri,
        CancellationToken cancellationToken
    )
    {
        // RFC 9101 §6.3: once the request object is verified, only its values are used.
        var values =
            (IBaseAuthorizationRequestValues?)authorizationRequest.OriginalRequestObject
            ?? authorizationRequest.OriginalRequestMessage;

        var state = string.IsNullOrEmpty(values.State) ? null : values.State;
        var handler = ResolveResponseModeHandler(
            openIdContext,
            openIdClient,
            values.ResponseMode,
            values.ResponseTypes ?? [],
            state
        );

        return await CreateDeliveryAsync(
            openIdContext,
            openIdClient,
            checkedRedirectUri,
            handler,
            state,
            cancellationToken
        );
    }

    /// <summary>
    /// Creates the delivery and, for a JWT-secured response mode, selects the credentials that secure the response
    /// when the delivery is derived, so a response that cannot be signed is refused before the user signs in or a code
    /// is issued (ADR-0201).
    /// </summary>
    private async ValueTask<AuthorizationResponseDelivery> CreateDeliveryAsync(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string redirectUri,
        IAuthorizationResponseModeHandler handler,
        string? state,
        CancellationToken cancellationToken
    )
    {
        var delivery = new AuthorizationResponseDelivery(redirectUri, handler, state);
        if (handler.SecuredResponseMode is null)
            return delivery;

        var credentials = await ClientJwtCredentialSelector.SelectCredentialsAsync(
            openIdContext,
            openIdClient,
            ClientJwtProtectionSettings.Authorization,
            cancellationToken
        );

        if (credentials.SigningCredentials is null)
        {
            throw openIdContext
                .ErrorFactory.Create(OpenIdProtocol.ErrorCodes.ServerError)
                .WithDescription(
                    $"The client registers no '{OpenIdSettingNames.AuthorizationSignedResponseAlg}', and a JWT-secured authorization response is always signed."
                )
                .WithStatusCode(StatusCodes.Status500InternalServerError)
                .AsException();
        }

        return delivery with
        {
            SecuringCredentials = credentials,
        };
    }

    private static string? GetState(IRequestValues requestValues) =>
        requestValues.TryGetValue(OpenIdProtocol.Parameters.State, out var stateStringValues)
        && !StringValues.IsNullOrEmpty(stateStringValues)
            ? stateStringValues.ToString()
            : null;

    private static string? GetResponseMode(
        OpenIdContext openIdContext,
        IRequestValues requestValues,
        string? state
    )
    {
        if (
            !requestValues.TryGetValue(
                OpenIdProtocol.Parameters.ResponseMode,
                out var responseModeStringValues
            )
        )
        {
            return null;
        }

        if (responseModeStringValues.Count > 1)
        {
            throw openIdContext
                .ErrorFactory.TooManyParameterValues(OpenIdProtocol.Parameters.ResponseMode)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        return responseModeStringValues.ToString();
    }

    private static string? GetRedirectUri(
        OpenIdContext openIdContext,
        IRequestValues requestValues,
        string? state
    )
    {
        if (
            !requestValues.TryGetValue(
                OpenIdProtocol.Parameters.RedirectUri,
                out var redirectUriStringValues
            )
        )
        {
            return null;
        }

        // The value is matched exactly as written (ADR-0203), so a second value is refused rather than joined.
        if (redirectUriStringValues.Count > 1)
        {
            throw openIdContext
                .ErrorFactory.TooManyParameterValues(OpenIdProtocol.Parameters.RedirectUri)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        return redirectUriStringValues.ToString();
    }

    private IAuthorizationResponseModeHandler ResolveResponseModeHandler(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? requestedResponseMode,
        IReadOnlyCollection<string> responseTypes,
        string? state
    )
    {
        var settings = openIdClient.Settings;

        // A response mode no registered handler serves could not carry even an error back to the client, and neither
        // could one the client does not list in response_modes_supported (ADR-0201).
        if (
            !ResponseModeProvider.TryResolve(requestedResponseMode, responseTypes, out var handler)
            || !IsListed(settings, handler.ResponseMode)
            || (requestedResponseMode is not null && !IsListed(settings, requestedResponseMode))
        )
        {
            throw openIdContext
                .ErrorFactory.InvalidParameterValue(OpenIdProtocol.Parameters.ResponseMode)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        return handler;
    }

    /// <summary>
    /// Determines whether the specified response mode is permitted by the <c>response_modes_supported</c> setting,
    /// which permits every mode when it is not set. It is applied to the value as written (so <c>jwt</c> must be
    /// listed to be requested) and to the resolved mode, the default included (ADR-0201).
    /// </summary>
    private static bool IsListed(IReadOnlySettingCollection settings, string responseMode) =>
        !settings.TryGetValue(OpenIdSettingKeys.ResponseModesSupported, out var supported)
        || supported.Contains(responseMode, StringComparer.Ordinal);

    private string CheckRedirectUri(
        OpenIdContext openIdContext,
        OpenIdClient openIdClient,
        string? redirectUriValue,
        string? state
    )
    {
        var errorFactory = openIdContext.ErrorFactory;
        var settings = openIdClient.Settings;

        if (redirectUriValue is null)
        {
            throw errorFactory
                .MissingParameter(OpenIdProtocol.Parameters.RedirectUri)
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        if (!AbsoluteUri.TryParse(redirectUriValue, out _))
        {
            throw errorFactory
                .InvalidRequest("The specified 'redirect_uri' is not valid absolute URI.")
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        // The raw string must equal a registered one exactly; only a registered loopback IP redirect URI may vary its
        // port, and only while the client allows it (RFC 9700 §4.1.3, RFC 8252 §7.3; see ADR-0203).
        var allowLoopbackPortChange = settings.GetValue(OpenIdSettingKeys.AllowLoopbackRedirect);
        var isRegistered = openIdClient.RedirectUris.Any(registered =>
            RedirectUriMatcher.MatchesExactly(redirectUriValue, registered)
            || (
                allowLoopbackPortChange
                && RedirectUriMatcher.MatchesLoopbackWithAnyPort(redirectUriValue, registered)
            )
        );
        if (!isRegistered)
        {
            throw errorFactory
                .UnauthorizedClient(
                    "The specified 'redirect_uri' is not valid for the associated 'client_id'."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        // FAPI 1.0 Part 1 §5.2.2-20 and FAPI 2.0 §5.3.2.2-8 refuse an http redirect URI; the effective
        // redirect_uri_kinds_supported carries every selected profile's ceiling (ADR-0203).
        if (!RedirectUriKindCatalog.IsPermitted(settings, redirectUriValue))
        {
            throw errorFactory
                .InvalidRequest(
                    "The specified 'redirect_uri' is not a kind of redirect URI the client's settings permit."
                )
                .WithStatusCode(StatusCodes.Status400BadRequest)
                .WithState(state)
                .AsException();
        }

        return redirectUriValue;
    }

    private static List<string> SplitResponseTypeNames(StringValues responseTypeStringValues) =>
        [
            .. responseTypeStringValues.SelectMany(value =>
                value?.Split(OpenIdProtocol.ParameterSeparatorChar) ?? []
            ),
        ];
}
