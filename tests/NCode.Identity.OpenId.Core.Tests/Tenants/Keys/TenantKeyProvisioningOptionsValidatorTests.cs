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
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Tenants.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants.Keys;

public sealed class TenantKeyProvisioningOptionsValidatorTests : BaseTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WhenEveryKeyCanBeProvisioned_Succeeds()
    {
        var options = new OpenIdOptions();
        var policy = CreatePolicy(options, []);

        var result = new TenantKeyProvisioningOptionsValidator(policy.Object).Validate(
            null,
            options
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenAKeyCannotBeProvisioned_FailsNamingTheSection()
    {
        var options = new OpenIdOptions();
        var policy = CreatePolicy(options, ["The algorithm 'XX999' is not registered."]);

        var result = new TenantKeyProvisioningOptionsValidator(policy.Object).Validate(
            null,
            options
        );

        Assert.True(result.Failed);
        var failure = Assert.Single(result.Failures ?? []);
        Assert.Equal("Tenant:KeyProvisioning: The algorithm 'XX999' is not registered.", failure);
    }

    #endregion

    private Mock<ITenantKeyPolicy> CreatePolicy(OpenIdOptions options, List<string> errors)
    {
        var policy = CreateStrictMock<ITenantKeyPolicy>();
        policy
            .Setup(x => x.ResolveSpecifications(options.Tenant.KeyProvisioning))
            .Returns(new TenantKeySpecificationResolution { Specifications = [], Errors = errors })
            .Verifiable();
        return policy;
    }
}
