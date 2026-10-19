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

using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public class OpenIdClientRegistrationExtensionsTests : BaseTests
{
    #region IsRegisteredAsConfidential Property Tests

    [Theory]
    [InlineData("client_secret_basic", true)]
    [InlineData("private_key_jwt", true)]
    [InlineData("none", false)]
    [InlineData("", false)]
    public void IsRegisteredAsConfidential_FollowsTheRegisteredAuthenticationMethod(
        string registeredAuthMethod,
        bool expected
    )
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.TokenEndpointAuthMethod))
            .Returns(registeredAuthMethod)
            .Verifiable();
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.SetupGet(x => x.Settings).Returns(mockSettings.Object).Verifiable();

        var isConfidential = mockClient.Object.IsRegisteredAsConfidential;

        Assert.Equal(expected, isConfidential);
    }

    [Fact]
    public void IsRegisteredAsConfidential_WhenTheClientIsNull_Throws()
    {
        OpenIdClient? client = null;

        Assert.Throws<ArgumentNullException>(() => client!.IsRegisteredAsConfidential);
    }

    #endregion
}
