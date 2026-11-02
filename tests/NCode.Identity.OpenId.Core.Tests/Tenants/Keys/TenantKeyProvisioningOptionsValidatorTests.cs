// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
