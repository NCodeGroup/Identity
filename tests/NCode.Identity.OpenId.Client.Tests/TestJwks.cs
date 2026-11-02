// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
