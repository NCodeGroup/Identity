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
/// Contains the credentials and set of options that are required when signing a JWS token.
/// </summary>
public class JoseSigningOptions : JoseEncodingOptions
{
    /// <inheritdoc />
    public override JoseCredentials Credentials => SigningCredentials;

    /// <summary>
    /// Gets the <see cref="JoseSigningCredentials"/> that are used to sign the JWS token.
    /// </summary>
    public JoseSigningCredentials SigningCredentials { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the payload should be base64url encoded (default).
    /// Note that unencoded and non-detached payloads may cause issues with JWS compact serialization.
    /// See RFC 7797 Section 5 for more information.
    /// </summary>
    public bool EncodePayload { get; init; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the payload should NOT be included to the encoded token.
    /// See RFC 7515 Appendix F for more information.
    /// </summary>
    public bool DetachPayload { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseSigningOptions"/> class.
    /// </summary>
    /// <param name="signingCredentials">The <see cref="JoseSigningCredentials"/> that are used to sign the JWS token.</param>
    public JoseSigningOptions(JoseSigningCredentials signingCredentials)
    {
        SigningCredentials = signingCredentials;
    }
}
