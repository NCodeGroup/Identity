// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Server.DevelopmentEnvironment;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class BootstrapAdminOptionsValidatorTests
{
    private static BootstrapAdminOptionsValidator Validator { get; } = new();

    #region Validate Tests

    [Fact]
    public void Validate_WhenNotConfigured_Succeeds()
    {
        var result = Validator.Validate(name: null, new BootstrapAdminOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenConfiguredWithATenant_Succeeds()
    {
        var result = Validator.Validate(
            name: null,
            new BootstrapAdminOptions
            {
                ClientId = "bootstrap-admin",
                ClientSecret = "c2VjcmV0",
                TenantId = "default",
            }
        );

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenConfiguredWithoutATenant_Fails()
    {
        // Without a named tenant the client would be seeded into every tenant, each created at runtime included.
        var result = Validator.Validate(
            name: null,
            new BootstrapAdminOptions { ClientId = "bootstrap-admin", ClientSecret = "c2VjcmV0" }
        );

        Assert.True(result.Failed);
        Assert.Contains(
            "BootstrapAdmin:TenantId is required",
            result.FailureMessage,
            StringComparison.Ordinal
        );
    }

    #endregion
}
