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


using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class Fapi1AdvancedSecurityProfileTests : BaseTests
{
    #region Properties Tests

    [Fact]
    public void ProfileName_IsFapi1Advanced()
    {
        var profile = new Fapi1AdvancedSecurityProfile();

        Assert.Equal(OpenIdConstants.SecurityProfiles.Fapi1Advanced, profile.ProfileName);
    }

    [Fact]
    public void Requirements_RequireAConfidentialClientAndAJwtSecuredCodeResponse()
    {
        var profile = new Fapi1AdvancedSecurityProfile();

        Assert.Collection(
            profile.Requirements,
            requirement => Assert.IsType<ConfidentialClientRequirement>(requirement),
            requirement => Assert.IsType<JwtSecuredAuthorizationResponseRequirement>(requirement)
        );
    }

    #endregion
}
