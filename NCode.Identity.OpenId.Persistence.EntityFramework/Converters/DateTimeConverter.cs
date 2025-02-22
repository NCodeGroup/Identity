﻿#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Converters;

/// <summary>
/// Provides an implementation of <see cref="ValueConverter"/> that always converts <see cref="DateTime"/> values to UTC.
/// </summary>
[PublicAPI]
public class DateTimeConverter : ValueConverter<DateTime, DateTime>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DateTimeConverter"/> class.
    /// </summary>
    public DateTimeConverter()
        : base(
            value => value.ToUniversalTime(),
            value => value.ToUniversalTime())
    {
        // nothing
    }
}
