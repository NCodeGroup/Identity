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

using System.Security.Claims;
using System.Text.Json;

namespace NCode.Jose.Jwt;

public class JwtPayload : JwtClaimsSet
{
    // Jti
    // Iat
    // Sub
    // Nbf
    // Exp

    private IReadOnlyCollection<Claim>? ClaimsOrNull { get; set; }

    private string? JtiOrNull { get; set; }
    private DateTimeOffset? IatOrNull { get; set; }
    private DateTimeOffset? NbfOrNull { get; set; }
    private DateTimeOffset? ExpOrNull { get; set; }
    private string? IssOrNull { get; set; }
    private IReadOnlyCollection<string>? AudOrNull { get; set; }
    private string? SubOrNull { get; set; }

    /// <inheritdoc />
    public JwtPayload(JsonElement rootElement)
        : base(rootElement)
    {
        // nothing
    }

    public IReadOnlyCollection<Claim> Claims => ClaimsOrNull ??= CreateClaimCollection(Iss);

    public string Jti => JtiOrNull ??= GetString(JwtClaimNames.Jti);
    public DateTimeOffset Iat => IatOrNull ??= GetDateTimeOffset(JwtClaimNames.Iat, DateTimeOffset.UnixEpoch);
    public DateTimeOffset Nbf => NbfOrNull ??= GetDateTimeOffset(JwtClaimNames.Nbf, DateTimeOffset.UnixEpoch);
    public DateTimeOffset Exp => ExpOrNull ??= GetDateTimeOffset(JwtClaimNames.Exp, DateTimeOffset.UnixEpoch);
    public string Iss => IssOrNull ??= GetString(JwtClaimNames.Iss);
    public IReadOnlyCollection<string> Aud => AudOrNull ??= GetStringCollection(JwtClaimNames.Aud);
    public string Sub => SubOrNull ??= GetString(JwtClaimNames.Sub);
}
