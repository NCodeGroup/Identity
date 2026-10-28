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
            OpenIdProtocol.GrantTypes.AuthorizationCode,
            OpenIdProtocol.GrantTypes.RefreshToken,
            OpenIdProtocol.GrantTypes.DeviceCode,
            OpenIdProtocol.GrantTypes.Ciba
        );
        SetupBaseline(
            mockSettings,
            OpenIdSettingKeys.BackchannelTokenDeliveryModesSupported,
            OpenIdProtocol.BackchannelTokenDeliveryModes.Poll,
            OpenIdProtocol.BackchannelTokenDeliveryModes.Ping,
            OpenIdProtocol.BackchannelTokenDeliveryModes.Push
        );
        SetupBaseline(
            mockSettings,
            OpenIdSettingKeys.PromptValuesSupported,
            OpenIdProtocol.PromptTypes.None,
            OpenIdProtocol.PromptTypes.Login,
            OpenIdProtocol.PromptTypes.Consent,
            OpenIdProtocol.PromptTypes.SelectAccount,
            OpenIdProtocol.PromptTypes.CreateAccount
        );
        SetupBaseline(
            mockSettings,
            OpenIdSettingKeys.ResponseModesSupported,
            OpenIdProtocol.ResponseModes.Query,
            OpenIdProtocol.ResponseModes.Fragment,
            OpenIdProtocol.ResponseModes.FormPost,
            OpenIdProtocol.ResponseModes.QueryJwt,
            OpenIdProtocol.ResponseModes.FragmentJwt,
            OpenIdProtocol.ResponseModes.FormPostJwt,
            OpenIdProtocol.ResponseModes.Jwt
        );
        SetupBaseline(
            mockSettings,
            OpenIdSettingKeys.ResponseTypesSupported,
            OpenIdProtocol.ResponseTypes.Code
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
