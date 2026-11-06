// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class ManagementConsoleFrontendValidatorTests : BaseTests
{
    #region Validate Tests

    [Fact]
    public void Validate_WhenTheMgmtFrontendIsConfigured_Succeeds()
    {
        var options = new BffOptions
        {
            Frontends = new Dictionary<string, BffFrontendOptions>
            {
                ["mgmt"] = new BffFrontendOptions(),
            },
        };

        var result = new ManagementConsoleFrontendValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheMgmtFrontendIsMissing_Fails()
    {
        var options = new BffOptions
        {
            Frontends = new Dictionary<string, BffFrontendOptions>
            {
                ["web"] = new BffFrontendOptions(),
            },
        };

        var result = new ManagementConsoleFrontendValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(
            "Bff:Frontends must configure the 'mgmt' frontend that serves the management console.",
            Assert.Single(result.Failures)
        );
    }

    #endregion
}
