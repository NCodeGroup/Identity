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
using JetBrains.Annotations;
using NCode.PropertyBag;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Contains the result after a Json Web Token (JWT) has been validation.
/// </summary>
[PublicAPI]
public class ValidateJwtResult
{
    /// <summary>
    /// Factory method to create a <see cref="ValidateJwtResult"/> instance that represents a failed JWT validation result.
    /// </summary>
    /// <param name="parameters">A <see cref="ValidateJwtParameters"/> instance that was used to validate the Json Web Token (JWT).</param>
    /// <param name="propertyBag">An <see cref="IPropertyBag"/> that can provide additional user-defined information about the current operation.</param>
    /// <param name="encodedToken">A <see cref="string"/> that contains the Json Web Token (JWT) which failed validation.</param>
    /// <param name="exception">An <see cref="Exception"/> that contains the details why the validation failed.</param>
    /// <returns>A <see cref="ValidateJwtResult"/> instance that represents a failed JWT validation result.</returns>
    public static ValidateJwtResult Fail(ValidateJwtParameters parameters, IPropertyBag propertyBag, string encodedToken, Exception exception) =>
        new(parameters, propertyBag, encodedToken)
        {
            Exception = exception
        };

    /// <summary>
    /// Factory method to create a <see cref="ValidateJwtResult"/> instance that represents a successful JWT validation result.
    /// </summary>
    /// <param name="parameters">A <see cref="ValidateJwtParameters"/> instance that was used to validate the Json Web Token (JWT).</param>
    /// <param name="propertyBag">An <see cref="IPropertyBag"/> that can provide additional user-defined information about the current operation.</param>
    /// <param name="decodedJwt">A <see cref="DecodedJwt"/> that contains the decoded Json Web Token (JWT).</param>
    /// <returns></returns>
    public static ValidateJwtResult Success(ValidateJwtParameters parameters, IPropertyBag propertyBag, DecodedJwt decodedJwt)
    {
        // immediately capture the values from the parameters to prevent any changes
        var createClaimsIdentityAsync = parameters.CreateClaimsIdentityAsync;
        var authenticationType = parameters.AuthenticationType;
        var nameClaimType = parameters.NameClaimType;
        var roleClaimType = parameters.RoleClaimType;

        return new ValidateJwtResult(parameters, propertyBag, decodedJwt.EncodedToken)
        {
            DecodedJwt = decodedJwt,
            CreateClaimsIdentityAsync = async cancellationToken =>
                await createClaimsIdentityAsync(
                    decodedJwt,
                    propertyBag,
                    authenticationType,
                    nameClaimType,
                    roleClaimType,
                    cancellationToken)
        };
    }

    private ClaimsIdentity? ClaimsIdentityOrNull { get; set; }

    private Func<CancellationToken, ValueTask<ClaimsIdentity>>? CreateClaimsIdentityAsync { get; init; }

    /// <summary>
    /// Gets the <see cref="ValidateJwtParameters"/> instance that was used to validate the Json Web Token (JWT).
    /// </summary>
    public ValidateJwtParameters Parameters { get; }

    /// <summary>
    /// Gets an <see cref="IPropertyBag"/> that can provide additional user-defined information about the current operation.
    /// </summary>
    public IPropertyBag PropertyBag { get; }

    /// <summary>
    /// Gets the original encoded Json Web Token (JWT) value.
    /// </summary>
    public string EncodedToken { get; }

    /// <summary>
    /// Gets a value that indicates whether the Json Web Token (JWT) was successfully decoded and validated.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Exception))]
    [MemberNotNullWhen(true, nameof(DecodedJwt))]
    [MemberNotNullWhen(true, nameof(CreateClaimsIdentityAsync))]
    public bool IsValid => Exception is null;

    /// <summary>
    /// Gets an <see cref="Exception"/> that occurred during Json Web Token (JWT) validation.
    /// </summary>
    public Exception? Exception { get; private init; }

    /// <summary>
    /// Gets an <see cref="DecodedJwt"/> that contains the decoded Json Web Token (JWT) header and payload.
    /// </summary>
    public DecodedJwt? DecodedJwt { get; private init; }

    private ValidateJwtResult(ValidateJwtParameters parameters, IPropertyBag propertyBag, string encodedToken)
    {
        Parameters = parameters;
        PropertyBag = propertyBag;
        EncodedToken = encodedToken;
    }

    /// <summary>
    /// Gets an <see cref="ClaimsIdentity"/> that contains the claims from the decoded Json Web Token (JWT).
    /// </summary>
    public async ValueTask<ClaimsIdentity> GetClaimsIdentityAsync(CancellationToken cancellationToken)
    {
        if (!IsValid) throw new InvalidOperationException();
        return ClaimsIdentityOrNull ??= await CreateClaimsIdentityAsync(cancellationToken);
    }
}
