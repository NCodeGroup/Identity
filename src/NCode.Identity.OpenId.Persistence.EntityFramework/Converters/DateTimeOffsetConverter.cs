// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Converters;

/// <summary>
/// Provides an implementation of <see cref="ValueConverter"/> that always converts <see cref="DateTimeOffset"/> values to UTC.
/// </summary>
internal class DateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DateTimeOffsetConverter"/> class.
    /// </summary>
    public DateTimeOffsetConverter()
        : base(value => value.ToUniversalTime(), value => value.ToUniversalTime())
    {
        // nothing
    }
}
