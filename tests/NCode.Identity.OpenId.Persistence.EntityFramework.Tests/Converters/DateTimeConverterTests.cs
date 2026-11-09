// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Converters;

public class DateTimeConverterTests
{
    private DateTimeConverter Converter { get; } = new();

    #region ConvertToProvider Tests

    [Fact]
    public void ConvertToProvider_ConvertsToUtc()
    {
        var local = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Local);

        var result = Assert.IsType<DateTime>(Converter.ConvertToProvider(local));

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(local.ToUniversalTime(), result);
    }

    #endregion

    #region ConvertFromProvider Tests

    [Fact]
    public void ConvertFromProvider_ConvertsToUtc()
    {
        var utc = new DateTime(2025, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        var result = Assert.IsType<DateTime>(Converter.ConvertFromProvider(utc));

        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(utc, result);
    }

    #endregion
}
