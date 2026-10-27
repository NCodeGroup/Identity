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

using System.Security.Cryptography;
using System.Text.Json;
using NCode.Encoders;

namespace NCode.Identity.OpenId.Client.Tests;

internal static class TestJwks
{
    public static Dictionary<string, object> Rsa(RSA rsa, string kid, string? use = "sig")
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var jwk = new Dictionary<string, object> { ["kty"] = "RSA", ["kid"] = kid };
        if (use is not null)
            jwk["use"] = use;
        jwk["n"] = Base64Url.Encode(parameters.Modulus!);
        jwk["e"] = Base64Url.Encode(parameters.Exponent!);
        return jwk;
    }

    public static string Set(params Dictionary<string, object>[] keys) =>
        JsonSerializer.Serialize(new Dictionary<string, object> { ["keys"] = keys });
}
