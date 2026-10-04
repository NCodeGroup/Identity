#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parameters;

public class DefaultKnownParameterCollectionTests
{
    private static KnownParameter KnownParameter(string name) =>
        new KnownParameter<string>(name, new StringParser()) { AllowMissingStringValues = true };

    private static DefaultKnownParameterCollection CreateCollection() =>
        new([KnownParameter("a"), KnownParameter("b")]);

    #region Count / Enumeration Tests

    [Fact]
    public void Count_ReturnsNumberOfParameters()
    {
        Assert.Equal(2, CreateCollection().Count);
    }

    [Fact]
    public void GetEnumerator_YieldsAllParameters()
    {
        var names = CreateCollection().Select(x => x.Name).OrderBy(x => x).ToList();

        Assert.Equal(["a", "b"], names);
    }

    #endregion

    #region TryGet Tests

    [Fact]
    public void TryGet_WhenPresent_ReturnsTrue()
    {
        var found = CreateCollection().TryGet("a", out var knownParameter);

        Assert.True(found);
        Assert.Equal("a", knownParameter.Name);
    }

    [Fact]
    public void TryGet_WhenMissing_ReturnsFalse()
    {
        var found = CreateCollection().TryGet("missing", out var knownParameter);

        Assert.False(found);
        Assert.Null(knownParameter);
    }

    #endregion
}
