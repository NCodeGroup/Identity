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

namespace NCode.Identity.OpenId.Endpoints.Authorization.Messages;

/// <summary>
/// Specifies from where the JWT-Secured Authorization Request (JAR) object was loaded from.
/// </summary>
[PublicAPI]
public enum RequestObjectSource
{
    /// <summary>
    /// Represents a value that hasn't been initialized yet and its value is unknown.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Specifies that the <c>Request Object</c> was loaded from the <c>request</c> parameter.
    /// </summary>
    Inline,

    /// <summary>
    /// Specifies that the <c>Request Object</c> was loaded by the fetching <c>request_uri</c> parameter.
    /// </summary>
    Remote
}
