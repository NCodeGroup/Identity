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
using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Results;

/// <summary>
/// A non-generic accessor for an <see cref="IResult"/> that renders an <see cref="IOpenIdResponse"/>,
/// allowing the wrapped response to be inspected without knowing its concrete type. Whether the response
/// represents an error is a property of the <see cref="Response"/> value (it is an
/// <see cref="IOpenIdError"/>), not of this type.
/// </summary>
[PublicAPI]
public interface IOpenIdResult : IResult
{
    /// <summary>
    /// Gets the <see cref="IOpenIdResponse"/> that will be rendered.
    /// </summary>
    IOpenIdResponse Response { get; }
}
