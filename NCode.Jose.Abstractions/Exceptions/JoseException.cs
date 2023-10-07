#region Copyright Preamble
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

namespace NCode.Jose.Exceptions;

/// <summary>
/// Represents errors that occur during <c>Jose</c> encoding or decoding.
/// </summary>
[Serializable]
public class JoseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JoseException"/> class.
    /// </summary>
    public JoseException()
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseException"/> class with the specified error message.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    public JoseException(string message)
        : base(message)
    {
        // nothing
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseException"/> class with the specified error message
    /// and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="inner">The exception that is the cause of the current exception,
    /// or a null reference if no inner exception is specified.</param>
    public JoseException(string message, Exception inner)
        : base(message, inner)
    {
        // nothing
    }
}
