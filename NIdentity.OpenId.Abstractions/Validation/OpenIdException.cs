#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System;
using System.Runtime.Serialization;

namespace NIdentity.OpenId.Validation;

/// <summary>
/// Represents an error that occured while executing an <c>OAuth</c> or <c>OpenID Connect</c> handler.
/// </summary>
[Serializable]
public class OpenIdException : Exception
{
    /// <summary>
    /// Gets the <see cref="IOpenIdExceptionFactory"/> singleton instance that can be used to create
    /// <see cref="OpenIdException"/> instances.
    /// </summary>
    public static IOpenIdExceptionFactory Factory { get; set; } = OpenIdExceptionFactory.Instance;

    /// <summary>
    /// Gets the <see cref="IErrorDetails"/> that contains detailed error information.
    /// </summary>
    public IErrorDetails ErrorDetails { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdException"/> class with a specified error message and
    /// error code.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="errorCode">The value that identifies the error.</param>
    public OpenIdException(string message, string errorCode)
        : base(message)
    {
        ErrorDetails = new ErrorDetails(errorCode);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdException"/> class with a specified error message, error
    /// code, and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="errorCode">The value that identifies the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a <c>null</c>
    /// reference if no inner exception is specified.</param>
    public OpenIdException(string message, string errorCode, Exception? innerException)
        : base(message, innerException)
    {
        ErrorDetails = new ErrorDetails(errorCode);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdException"/> class with serialized data.
    /// </summary>
    /// <param name="info">The <see cref="SerializationInfo"/> that holds the serialized object data about the
    /// exception being thrown.</param>
    /// <param name="context">The <see cref="StreamingContext"/> that contains contextual information about the
    /// source or destination.</param>
    protected OpenIdException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
        ErrorDetails = (ErrorDetails)(info.GetValue(nameof(ErrorDetails), typeof(ErrorDetails)) ?? new ErrorDetails(OpenIdConstants.ErrorCodes.ServerError));
    }

    /// <inheritdoc />
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        base.GetObjectData(info, context);

        info.AddValue(nameof(ErrorDetails), ErrorDetails, typeof(ErrorDetails));
    }
}