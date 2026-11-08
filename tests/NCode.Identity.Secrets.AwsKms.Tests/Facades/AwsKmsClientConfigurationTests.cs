// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Amazon.Runtime;
using NCode.Identity.Secrets.AwsKms.Facades;
using Xunit;

namespace NCode.Identity.Secrets.AwsKms.Tests.Facades;

public class AwsKmsClientConfigurationTests
{
    #region Create Tests

    [Fact]
    public void Create_Region_SetsRegionEndpoint()
    {
        var config = AwsKmsClientConfiguration.Create("us-west-2");

        Assert.Equal("us-west-2", config.RegionEndpoint.SystemName);
    }

    [Fact]
    public void Create_Region_UsesStandardRetryMode()
    {
        var config = AwsKmsClientConfiguration.Create("eu-central-1");

        Assert.Equal(RequestRetryMode.Standard, config.RetryMode);
    }

    [Fact]
    public void Create_Region_SetsMaxErrorRetry()
    {
        var config = AwsKmsClientConfiguration.Create("eu-central-1");

        Assert.Equal(3, config.MaxErrorRetry);
    }

    #endregion

    #region CreateWithoutRetry Tests

    [Fact]
    public void CreateWithoutRetry_Region_SetsRegionEndpoint()
    {
        var config = AwsKmsClientConfiguration.CreateWithoutRetry("us-west-2");

        Assert.Equal("us-west-2", config.RegionEndpoint.SystemName);
    }

    [Fact]
    public void CreateWithoutRetry_Region_DoesNotRetry()
    {
        var config = AwsKmsClientConfiguration.CreateWithoutRetry("eu-central-1");

        Assert.Equal(0, config.MaxErrorRetry);
    }

    #endregion
}
