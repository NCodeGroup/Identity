﻿#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.Jose.Exceptions;

namespace NCode.Identity.JsonWebTokens.Exceptions;

/// <summary>
/// Represents errors that occur during <c>Json Web Token (JWT)</c> validation.
/// </summary>
[PublicAPI]
[Serializable]
public class TokenValidationException : JoseException
{
    private const string DefaultMessage = "Token validation failed.";

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenValidationException"/> class with a default error message.
    /// </summary>
    public TokenValidationException()
        : base(DefaultMessage)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenValidationException"/> class with the specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public TokenValidationException(string message)
        : base(message)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TokenValidationException"/> class with the specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception,
    /// or a null reference if no inner exception is specified.</param>
    public TokenValidationException(string message, Exception inner)
        : base(message, inner)
    {
        // nothing
    }
}
