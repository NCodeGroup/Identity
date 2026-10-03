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

using NCode.Identity.OpenId.Settings;
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
        // client_credentials and password are intentionally opt-in (host-enabled), not default-on.
        settings.Set(
            OpenIdSettingKeys.GrantTypesSupported,
            [
                OpenIdConstants.GrantTypes.AuthorizationCode,
                OpenIdConstants.GrantTypes.Implicit,
                OpenIdConstants.GrantTypes.RefreshToken,
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
                OpenIdConstants.ResponseModes.Query,
                OpenIdConstants.ResponseModes.Fragment,
                OpenIdConstants.ResponseModes.FormPost,
            ]
        );

        settings.Set(
            OpenIdSettingKeys.ResponseTypesSupported,
            [
                OpenIdConstants.ResponseTypes.Code,
                OpenIdConstants.ResponseTypes.IdToken,
                OpenIdConstants.ResponseTypes.Token,
            ]
        );

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
