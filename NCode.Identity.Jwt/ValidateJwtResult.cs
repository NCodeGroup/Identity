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
using System.Security.Claims;

namespace NCode.Identity.Jwt;

public class ValidateJwtResult
{
    /// <summary>
    /// Gets the original encoded Json Web Token (JWT) value.
    /// </summary>
    public string EncodedToken { get; }

    /// <summary>
    /// Gets a value that indicates whether the Json Web Token (JWT) was successfully decoded and validated.
    /// </summary>
    [MemberNotNullWhen(true, nameof(DecodedJwt))]
    [MemberNotNullWhen(true, nameof(ClaimsIdentity))]
    [MemberNotNullWhen(false, nameof(Exception))]
    public bool IsValid => Exception is null;

    /// <summary>
    /// Gets an <see cref="Exception"/> that occurred during Json Web Token (JWT) validation.
    /// </summary>
    public Exception? Exception { get; private init; }

    /// <summary>
    /// Gets an <see cref="DecodedJwt"/> that contains the decoded Json Web Token (JWT) header and payload.
    /// </summary>
    public DecodedJwt? DecodedJwt { get; private init; }

    /// <summary>
    /// Gets an <see cref="ClaimsIdentity"/> that contains the claims from the decoded Json Web Token (JWT).
    /// </summary>
    public ClaimsIdentity? ClaimsIdentity { get; private init; }

    /// <summary>
    /// Gets a <see cref="PropertyBag"/> that can be used to store custom state information.
    /// </summary>
    public PropertyBag PropertyBag { get; }

    private ValidateJwtResult(string encodedToken, PropertyBag propertyBag)
    {
        EncodedToken = encodedToken;
        PropertyBag = propertyBag;
    }

    public static ValidateJwtResult Fail(string encodedToken, Exception exception, PropertyBag propertyBag) =>
        new(encodedToken, propertyBag)
        {
            Exception = exception,
        };

    public static ValidateJwtResult Success(DecodedJwt decodedJwt, ClaimsIdentity claimsIdentity, PropertyBag propertyBag) =>
        new(decodedJwt.EncodedToken, propertyBag)
        {
            DecodedJwt = decodedJwt,
            ClaimsIdentity = claimsIdentity
        };
}
