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

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Specifies from where an <see cref="IBaseAuthorizationRequest"/> was loaded from.
/// </summary>
[PublicAPI]
public enum AuthorizationSourceType
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Specifies that the authorization request was loaded from the HTTP query data.
    /// </summary>
    Query,

    /// <summary>
    /// Specifies that the authorization request was loaded from the HTTP form data.
    /// </summary>
    Form,

    /// <summary>
    /// Specifies that the authorization request was loaded from a JWT-Secured Authorization Request (RFC 9101) object.
    /// </summary>
    Jar,

    /// <summary>
    /// Specifies that the authorization request was loaded after combining values from the request message (query or form)
    /// and the request (JAR) object.
    /// </summary>
    Union
}
