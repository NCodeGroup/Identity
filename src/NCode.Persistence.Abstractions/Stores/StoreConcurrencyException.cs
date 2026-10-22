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

using JetBrains.Annotations;

namespace NCode.Persistence.Stores;

/// <summary>
/// Represents the error that occurs when a unit of work writes an entity that another unit of work changed after it was
/// read, as the concurrency token of the entity shows. The caller lost the race and must not retry the same write.
/// </summary>
// See ADR-0208.
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
