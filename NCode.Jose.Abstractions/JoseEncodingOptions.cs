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

using NCode.Jose.Credentials;

namespace NCode.Jose;

/// <summary>
/// Contains the credentials and common set of options that are required when encoding a JOSE token.
/// </summary>
public abstract class JoseEncodingOptions
{
    private const string DefaultTokenType = "JWT";

    /// <summary>
    /// Gets the <see cref="JoseCredentials"/> that are used to encode the JOSE token.
    /// </summary>
    public abstract JoseCredentials Credentials { get; }

    /// <summary>
    /// Gets or set the value for the <c>typ</c> JOSE header parameter.
    /// The default value is <c>JWT</c>.
    /// </summary>
    public string TokenType { get; init; } = DefaultTokenType;

    /// <summary>
    /// Gets or sets a boolean indicating whether to add the secret's <c>kid</c> value to the JOSE header.
    /// The default value is <c>true</c>.
    /// </summary>
    public bool AddKeyIdHeader { get; init; } = true;
}
