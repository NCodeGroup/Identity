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
/// Contains the credentials and set of options that are required when encrypting a JWE token.
/// </summary>
public class JoseEncryptingOptions : JoseEncodingOptions
{
    /// <inheritdoc />
    public override JoseEncodingCredentials Credentials => EncryptingCredentials;

    /// <summary>
    /// Gets the <see cref="JoseEncryptingCredentials"/> that are used to encrypt the JWE token.
    /// </summary>
    public JoseEncryptingCredentials EncryptingCredentials { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JoseEncryptingOptions"/> class.
    /// </summary>
    /// <param name="encryptingCredentials">The <see cref="JoseEncryptingCredentials"/> that are used to encrypt the JWE token.</param>
    public JoseEncryptingOptions(JoseEncryptingCredentials encryptingCredentials)
    {
        EncryptingCredentials = encryptingCredentials;
    }
}
