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

namespace NIdentity.OpenId.DataContracts;

/// <summary>
/// Contains the configuration that specifies how security tokens should be generated.
/// </summary>
public class TokenConfiguration
{
    /// <summary>
    /// Gets or sets a value indicating whether security tokens must be encrypted.
    /// </summary>
    public bool RequireEncryption { get; set; }

    /// <summary>
    /// Gets or sets the amount of time that a token is valid for.
    /// Defaults to 5 minutes.
    /// </summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(5.0);

    /// <summary>
    /// Gets or sets the ordered collection of string codes that specify the preferred algorithms
    /// which are to be used for digital signatures when signing security tokens.
    /// </summary>
    public IList<string> SignatureAlgorithms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the ordered collection of string codes that specify the preferred algorithms
    /// which are to be used for key management when encrypting security tokens.
    /// </summary>
    public IList<string> KeyManagementAlgorithms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the ordered collection of string codes that specify the preferred algorithms
    /// which are to be used for content encryption when encrypting security tokens.
    /// </summary>
    public IList<string> EncryptionAlgorithms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the ordered collection of string codes that specify the preferred algorithms
    /// which are to be used for content compression when encrypting security tokens.
    /// </summary>
    public IList<string> CompressionAlgorithms { get; set; } = new List<string>();
}
