#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Messages.Parameters;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parameters;

public class DefaultKnownParameterDataSourceTests : BaseTests
{
    private Mock<INullChangeToken> MockNullChangeToken { get; }
    private DefaultKnownParameterDataSource DataSource { get; }

    public DefaultKnownParameterDataSourceTests()
    {
        MockNullChangeToken = CreateStrictMock<INullChangeToken>();
        DataSource = new DefaultKnownParameterDataSource(MockNullChangeToken.Object);
    }

    [Fact]
    public void GetChangeToken_Valid()
    {
        var result = DataSource.GetChangeToken();
        Assert.Same(MockNullChangeToken.Object, result);
    }

    [Fact]
    public void Collection_Valid()
    {
        var results = DataSource.Collection;
        Assert.Equal(35, results.Count());
    }
}
