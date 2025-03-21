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
using NCode.Identity.OpenId.Endpoints.Authorization.Messages;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Contains constants for various <see cref="IParameterParser{T}"/> implementations that are used by <c>OAuth</c>
/// and <c>OpenID Connect</c> messages.
/// </summary>
[PublicAPI]
public static class ParameterParsers
{
    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="IRequestClaims"/> from a JSON payload.
    /// </summary>
    public static readonly JsonParser<IRequestClaims> RequestClaims = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="string"/> values.
    /// </summary>
    public static readonly StringParser String = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse string collections which are separated by the
    /// space ' ' character.
    /// </summary>
    public static readonly StringSetParser StringSet = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse string collections which are separated by the
    /// space ' ' character.
    /// </summary>
    public static readonly StringListParser StringList = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="StringValues"/> values.
    /// </summary>
    public static readonly StringValuesParser StringValues = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="TimeSpan"/> values.
    /// </summary>
    public static readonly TimeSpanParser TimeSpan = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="Uri"/> values.
    /// </summary>
    public static readonly UriParser Uri = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="bool"/> values.
    /// </summary>
    public static readonly BoolParser Bool = new();

    /// <summary>
    /// Gets a <see cref="IParameterParser{T}"/> that can be used to parse <see cref="DateTimeOffset"/> values.
    /// </summary>
    public static readonly DateTimeOffsetParser DateTimeOffset = new();
}
