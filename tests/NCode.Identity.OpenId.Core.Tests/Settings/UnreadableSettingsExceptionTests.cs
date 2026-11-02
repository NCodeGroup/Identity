// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
