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

using System.Text.Json;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Settings;

public class UnreadableSettingsExceptionTests
{
    private const string ScopeName = "tenant";
    private const string OwnerId = "tenant-1";
    private const string DocumentError = "boom";

    #region Constructor Tests

    [Fact]
    public void Constructor_WhenTheScopeNameIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new UnreadableSettingsException(null!, OwnerId, new JsonException(DocumentError))
        );

        Assert.Equal("scopeName", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenTheOwnerIdIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new UnreadableSettingsException(ScopeName, null!, new JsonException(DocumentError))
        );

        Assert.Equal("ownerId", exception.ParamName);
    }

    [Fact]
    public void Constructor_WhenTheInnerExceptionIsNull_ThrowsArgumentNullException()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new UnreadableSettingsException(ScopeName, OwnerId, null!)
        );

        Assert.Equal("innerException", exception.ParamName);
    }

    [Fact]
    public void Constructor_NamesTheDocument()
    {
        var inner = new JsonException(DocumentError);

        var exception = new UnreadableSettingsException(ScopeName, OwnerId, inner);

        Assert.Equal(ScopeName, exception.ScopeName);
        Assert.Equal(OwnerId, exception.OwnerId);
        Assert.Equal(DocumentError, exception.DocumentError);
        Assert.Same(inner, exception.InnerException);
        Assert.Equal(
            "The stored settings document of tenant 'tenant-1' cannot be honored: boom",
            exception.Message
        );
    }

    #endregion
}
