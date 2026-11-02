// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using System.Text.Json;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Validation;
using NCode.PropertyBag;

namespace NCode.Identity.OpenId.Core.Tests;

/// <summary>
/// Builds <see cref="JwtValidationResult"/> instances for tests that stub a JSON Web Token (JWT) validator.
/// </summary>
internal static class TestJwtValidationResults
{
    public static JwtValidationResult Valid(
        object payloadObject,
        string encodedToken = "aaa.bbb.ccc"
    )
    {
        var payload = JsonSerializer.SerializeToElement(payloadObject);
        var compactJwt = new CompactJwt("JWS", default, encodedToken, []);
        var decodedJwt = new DecodedJwt(compactJwt, payload, signingKey: null!);
        return JwtValidationResult.Success(
            new JwtValidationParameters(),
            PropertyBagFactory.Create(),
            decodedJwt,
            (decoded, _, authType, nameType, roleType, _) =>
            {
                var identity = new ClaimsIdentity(authType, nameType, roleType);
                foreach (var property in decoded.Payload.EnumerateObject())
                {
                    identity.AddClaim(new Claim(property.Name, property.Value.ToString()));
                }
                return ValueTask.FromResult(identity);
            }
        );
    }

    public static JwtValidationResult Invalid(string encodedToken) =>
        JwtValidationResult.Fail(
            new JwtValidationParameters(),
            PropertyBagFactory.Create(),
            encodedToken,
            new JwtValidationException("invalid")
        );
}
