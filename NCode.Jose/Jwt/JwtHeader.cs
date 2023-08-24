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

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace NCode.Jose.Jwt;

[SuppressMessage("ReSharper", "IdentifierTypo")]
public class JwtHeader : JwtClaimsSet
{
    // Cty
    // Enc
    // Typ
    // Zip

    private string? KidOrNull { get; set; }
    private string? X5tOrNull { get; set; }
    private string? X5tS256OrNull { get; set; }

    /// <inheritdoc />
    public JwtHeader(JsonElement rootElement)
        : base(rootElement)
    {
        // nothing
    }

    public string Kid => KidOrNull ??= GetString(JwtClaimNames.Kid);
    public string X5t => X5tOrNull ??= GetString(JwtClaimNames.X5t);
    public string X5tS256 => X5tS256OrNull ??= GetString(JwtClaimNames.X5tS256);
}
