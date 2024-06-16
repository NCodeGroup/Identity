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

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Contains the properties for serializing and deserializing <see cref="IAuthorizationRequest"/> instances
/// to and from JSON.
/// </summary>
public readonly struct AuthorizationRequestJsonEnvelope
{
    /// <summary>
    /// Gets or sets a value indicating whether the request is a continuation.
    /// </summary>
    public required bool IsContinuation { get; init; }

    /// <summary>
    /// Gets or sets the original request message.
    /// </summary>
    public required IAuthorizationRequestMessage? OriginalRequestMessage { get; init; }

    /// <summary>
    /// Gets or sets the original request object.
    /// </summary>
    public required IAuthorizationRequestObject? OriginalRequestObject { get; init; }
}
