#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Settings;

/// <summary>
/// Provides the built-in, off-the-shelf baseline for the <c>*_supported</c> server settings so that a host
/// which configures nothing still gets a sensible, spec-compliant baseline. Registered by default and
/// replaceable/extendable by hosts.
/// </summary>
internal sealed class DefaultSettingsProvider : IDefaultSettingsProvider
{
    /// <inheritdoc />
    public void Configure(ISettingCollection settings)
    {
        // Secure-by-default: implicit/hybrid and client_credentials/password are intentionally not advertised.
        // The implicit/hybrid capability still exists; an operator re-enables it by widening this setting (and
        // response_types_supported) via configuration (RFC 9700 / OAuth 2.1 direction — no implicit/hybrid).
        // A hybrid flow uses both 'authorization_code' and 'implicit' (OpenID Connect Registration §2).
        settings.Set(
            OpenIdSettingKeys.GrantTypesSupported,
            [
                OpenIdProtocol.GrantTypes.AuthorizationCode,
                OpenIdProtocol.GrantTypes.RefreshToken,
                OpenIdProtocol.GrantTypes.DeviceCode,
                OpenIdProtocol.GrantTypes.Ciba,
            ]
        );

        // CIBA token delivery: poll and ping complete through the token endpoint; push delivers tokens inside the
        // client notification.
        settings.Set(
            OpenIdSettingKeys.BackchannelTokenDeliveryModesSupported,
            [
                OpenIdProtocol.BackchannelTokenDeliveryModes.Poll,
                OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
                OpenIdProtocol.BackchannelTokenDeliveryModes.Push,
            ]
        );

        settings.Set(
            OpenIdSettingKeys.PromptValuesSupported,
            [
                OpenIdConstants.PromptTypes.None,
                OpenIdConstants.PromptTypes.Login,
                OpenIdConstants.PromptTypes.Consent,
                OpenIdConstants.PromptTypes.SelectAccount,
                OpenIdConstants.PromptTypes.CreateAccount,
            ]
        );

        settings.Set(
            OpenIdSettingKeys.ResponseModesSupported,
            [
                OpenIdProtocol.ResponseModes.Query,
                OpenIdProtocol.ResponseModes.Fragment,
                OpenIdProtocol.ResponseModes.FormPost,
                OpenIdProtocol.ResponseModes.QueryJwt,
                OpenIdProtocol.ResponseModes.FragmentJwt,
                OpenIdProtocol.ResponseModes.FormPostJwt,
                OpenIdProtocol.ResponseModes.Jwt,
            ]
        );

        // Secure-by-default: only the authorization-code flow is advertised. The setting lists whole response_type
        // values, so an operator re-enables a front-channel-token flow that modern guidance (RFC 9700 / OAuth 2.1)
        // removes only by listing it — for example 'code id_token' — and enables exactly that flow (ADR-0141).
        settings.Set(OpenIdSettingKeys.ResponseTypesSupported, [OpenIdProtocol.ResponseTypes.Code]);

        // claims_supported is advisory by default; it only becomes an enforcement allow-list when
        // claims_supported_is_strict is enabled, so the baseline is kept host-extendable (ADR-0010).
        settings.Set(
            OpenIdSettingKeys.ClaimsSupported,
            [
                .. OpenIdConstants.ProtocolClaims,
                .. OpenIdConstants.ClaimsByScope.Profile,
                .. OpenIdConstants.ClaimsByScope.Email,
                .. OpenIdConstants.ClaimsByScope.Address,
                .. OpenIdConstants.ClaimsByScope.Phone,
            ]
        );
    }
}
