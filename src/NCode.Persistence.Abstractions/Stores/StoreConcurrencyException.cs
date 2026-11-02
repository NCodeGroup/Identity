// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Persistence.Stores;

/// <summary>
/// Represents the error that occurs when a unit of work writes an entity that another unit of work changed after it was
/// read, as the concurrency token of the entity shows. The caller lost the race and must not retry the same write.
/// </summary>
// See ADR-1208.
[PublicAPI]
public class StoreConcurrencyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StoreConcurrencyException"/> class.
    /// </summary>
    public StoreConcurrencyException()
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreConcurrencyException"/> class with the specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public StoreConcurrencyException(string message)
        : base(message)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreConcurrencyException"/> class with the specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception,
    /// or a null reference if no inner exception is specified.</param>
    public StoreConcurrencyException(string message, Exception inner)
        : base(message, inner)
    {
        // nothing
    }
}
