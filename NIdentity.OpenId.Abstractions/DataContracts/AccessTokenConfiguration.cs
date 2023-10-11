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

using NCode.Jose;

namespace NIdentity.OpenId.DataContracts;

/// <summary>
/// Contains the configuration that specifies how access tokens should be generated.
/// </summary>
public class AccessTokenConfiguration : TokenConfiguration
{
    /// <summary>
    /// Gets or sets the value for the <c>typ</c> header when generating access tokens.
    /// The default value is <c>JWT</c>.
    /// </summary>
    public string TokenType { get; set; } = JoseTokenTypes.Jwt;
}
