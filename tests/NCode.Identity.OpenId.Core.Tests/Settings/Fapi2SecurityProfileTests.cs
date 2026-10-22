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

using Moq;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class Fapi2SecurityProfileTests : BaseTests
{
    #region ApplyCeilingSettings Tests

    [Fact]
    public void ApplyCeilingSettings_PermitsHttpOnlyForALoopbackRedirectUri()
    {
        var mockCeiling = CreateLooseMock<ISecurityProfileCeilingBuilder>();
        mockCeiling
            .Setup(x =>
                x.Set(
                    OpenIdSettingKeys.RedirectUriKindsSupported,
                    It.Is<IReadOnlyCollection<string>>(kinds =>
                        kinds.SequenceEqual(
                            new[]
                            {
                                OpenIdConstants.RedirectUriKinds.Https,
                                OpenIdConstants.RedirectUriKinds.Loopback,
                                OpenIdConstants.RedirectUriKinds.PrivateUse,
                            }
                        )
                    )
                )
            )
            .Verifiable();

        new Fapi2SecurityProfile().ApplyCeilingSettings(mockCeiling.Object);
    }

    #endregion
}
