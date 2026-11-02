// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Converters;

/// <summary>
/// Provides an implementation of <see cref="ValueConverter"/> that always converts <see cref="DateTime"/> values to UTC.
/// </summary>
internal class DateTimeConverter : ValueConverter<DateTime, DateTime>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DateTimeConverter"/> class.
    /// </summary>
    public DateTimeConverter()
        : base(value => value.ToUniversalTime(), value => value.ToUniversalTime())
    {
        // nothing
    }
}
