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
