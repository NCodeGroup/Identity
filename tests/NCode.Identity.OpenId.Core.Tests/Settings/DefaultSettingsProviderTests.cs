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

using Moq;
using NCode.Identity.OpenId.Authentication.Settings;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class DefaultSettingsProviderTests : BaseTests
{
    private static void SetupBaseline(
        Mock<ISettingCollection> mockSettings,
        SettingKey<IReadOnlyCollection<string>> key,
        params string[] expected
    ) =>
        mockSettings
            .Setup(x =>
                x.Set(
                    key,
                    It.Is<IReadOnlyCollection<string>>(value =>
                        expected.All(value.Contains) && value.Count == expected.Length
                    )
                )
            )
            .Verifiable();

    [Fact]
    public void Configure_SetsTheStandardBaseline()
    {
        var mockSettings = CreateStrictMock<ISettingCollection>();

        SetupBaseline(
            mockSettings,
            OpenIdSettingKeys.GrantTypesSupported,
            OpenIdConstants.GrantTypes.AuthorizationCode,
            OpenIdConstants.GrantTypes.Implicit,
            OpenIdConstants.GrantTypes.RefreshToken,
            OpenIdConstants.GrantTypes.DeviceCode
        );
        SetupBaseline(
            mockSettings,
            OpenIdSettingKeys.PromptValuesSupported,
            OpenIdConstants.PromptTypes.None,
            OpenIdConstants.PromptTypes.Login,
            OpenIdConstants.PromptTypes.Consent,
            OpenIdConstants.PromptTypes.SelectAccount,
            OpenIdConstants.PromptTypes.CreateAccount
        );
        SetupBaseline(
            mockSettings,
            OpenIdSettingKeys.ResponseModesSupported,
            OpenIdConstants.ResponseModes.Query,
            OpenIdConstants.ResponseModes.Fragment,
            OpenIdConstants.ResponseModes.FormPost
        );
        SetupBaseline(
            mockSettings,
            OpenIdSettingKeys.ResponseTypesSupported,
            OpenIdConstants.ResponseTypes.Code,
            OpenIdConstants.ResponseTypes.IdToken,
            OpenIdConstants.ResponseTypes.Token
        );
        SetupBaseline(
            mockSettings,
            OpenIdSettingKeys.ClaimsSupported,
            [
                .. OpenIdConstants.ProtocolClaims,
                .. OpenIdConstants.ClaimsByScope.Profile,
                .. OpenIdConstants.ClaimsByScope.Email,
                .. OpenIdConstants.ClaimsByScope.Address,
                .. OpenIdConstants.ClaimsByScope.Phone,
            ]
        );

        var provider = new DefaultSettingsProvider();
        provider.Configure(mockSettings.Object);
    }
}
