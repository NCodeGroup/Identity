#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Contains constants that specify from where an authorization request message was loaded from.
/// </summary>
[PublicAPI]
public static class AuthorizationSourceTypes
{
    /// <summary>
    /// Specifies that the message was loaded from the HTTP query data.
    /// </summary>
    public const string Query = OpenIdRequestValuesSourceTypes.Query;

    /// <summary>
    /// Specifies that the message was loaded from the HTTP form data.
    /// </summary>
    public const string Form = OpenIdRequestValuesSourceTypes.Form;

    /// <summary>
    /// Specifies that the message was loaded from a JWT-Secured Authorization Request (RFC 9101) object.
    /// </summary>
    public const string Jar = "jar";

    /// <summary>
    /// Specifies that the authorization request was loaded after combining values from the request message (query or form)
    /// and the request (JAR) object.
    /// </summary>
    public const string Union = "union";
}
