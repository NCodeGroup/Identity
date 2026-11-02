// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace NCode.Identity.Persistence;

/// <summary>
/// Provides the ability to check for optimistic concurrency violations by using a random value that compared to
/// the existing value in a database. The random value is automatically generated every time a row is inserted or
/// updated in the database.
/// </summary>
[PublicAPI]
public interface ISupportConcurrencyToken
{
    /// <summary>
    /// Gets a value used to check for optimistic concurrency violations.
    /// </summary>
    [MaxLength(MaxLengths.ConcurrencyToken)]
    string ConcurrencyToken { get; }
}
