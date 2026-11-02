// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Messages.Parameters;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parameters;

public class DefaultCommonParameterDataSourceTests : BaseTests
{
    private Mock<INullChangeToken> MockNullChangeToken { get; }
    private DefaultCommonParameterDataSource DataSource { get; }

    public DefaultCommonParameterDataSourceTests()
    {
        MockNullChangeToken = CreateStrictMock<INullChangeToken>();
        DataSource = new DefaultCommonParameterDataSource(MockNullChangeToken.Object);
    }

    [Fact]
    public void GetChangeToken_Called_ReturnsMockNullChangeToken()
    {
        var result = DataSource.GetChangeToken();
        Assert.Same(MockNullChangeToken.Object, result);
    }

    [Fact]
    public void Collection_Accessed_Returns56Parameters()
    {
        var results = DataSource.Collection;
        Assert.Equal(56, results.Count());
    }
}
