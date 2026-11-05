// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.RateLimiting;

public class DefaultRateLimitSettingDescriptorDataSourceTests : BaseTests
{
    private DefaultRateLimitSettingDescriptorDataSource CreateDataSource() =>
        new(CreateStrictMock<INullChangeToken>().Object);

    #region Collection Property Tests

    [Fact]
    public void Collection_WhenEnumerated_EachIsALimitThatMergesWithAMeet()
    {
        var descriptors = CreateDataSource().Collection.ToList();

        Assert.Equal(7, descriptors.Count);
        Assert.All(
            descriptors,
            descriptor =>
            {
                Assert.True(descriptor.IsLimit, descriptor.Name);
                Assert.True(descriptor.HasMeetMerge, descriptor.Name);
            }
        );
    }

    #endregion
}
