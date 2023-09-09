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

using System.Text.Json;
using NCode.Cryptography.Keys;
using NCode.Jose;

namespace NCode.Identity.Jwt;

/// <summary>
/// Contains extension methods for the <see cref="ValidateJwtParameters"/> class.
/// </summary>
public static class ValidateJwtParametersExtensions
{
    /// <summary>
    /// Sets the <see cref="ValidateJwtParameters.ResolveValidationKeysAsync"/> property on <see cref="ValidateJwtParameters"/>
    /// to a delegate that returns the specified <paramref name="secretKeys"/>.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <param name="secretKeys">The collection of <see cref="SecretKey"/> instances to use for Json Web Token (JWT) validation.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters UseValidationKeys(
        this ValidateJwtParameters parameters,
        IEnumerable<SecretKey> secretKeys)
    {
        parameters.ResolveValidationKeysAsync = (_, _, _, _, _) => ValueTask.FromResult(secretKeys);
        return parameters;
    }

    /// <summary>
    /// Adds the specified <paramref name="validator"/> delegate to the <see cref="ValidateJwtParameters.Validators"/>
    /// property on <see cref="ValidateJwtParameters"/>.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <param name="validator">The <see cref="ValidateJwtAsync"/> delegate to add.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters AddValidator(
        this ValidateJwtParameters parameters,
        ValidateJwtAsync validator)
    {
        parameters.Validators.Add(validator);
        return parameters;
    }

    /// <summary>
    /// Adds a validator that asserts the specified <paramref name="claimName"/> exists in the Json Web Token (JWT) payload
    /// with any value specified from <paramref name="validValues"/>. If the claim does not exist or the value is not one of
    /// the valid values, then an exception is thrown.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <param name="claimName">The name of the claim to validate.</param>
    /// <param name="allowCollection"><c>true</c> if the claim allows multiple values; <c>false</c> if the claim must be a single value.</param>
    /// <param name="validValues">The collection of allowable values for the claim.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters ValidateClaim(
        this ValidateJwtParameters parameters,
        string claimName,
        bool allowCollection,
        params string[] validValues) =>
        parameters.AddValidator(
            ValidatePayloadClaimValue(
                claimName,
                allowCollection,
                validValues.ToHashSet(StringComparer.Ordinal)));

    /// <summary>
    /// Adds a validator that asserts the <c>iss</c> claim exists in the Json Web Token (JWT) payload with any of the
    /// specified <paramref name="validIssuers"/>.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <param name="validIssuers">The collection of allowable values for the <c>iss></c> claim.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters ValidateIssuer(
        this ValidateJwtParameters parameters,
        params string[] validIssuers) =>
        parameters.ValidateClaim(
            JoseClaimNames.Payload.Iss,
            allowCollection: false,
            validIssuers);

    /// <summary>
    /// Adds a validator that asserts the <c>aud</c> claim exists in the Json Web Token (JWT) payload with any of the
    /// specified <paramref name="validAudiences"/>.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <param name="validAudiences">The collection of allowable values for the <c>aud></c> claim.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters ValidateAudience(
        this ValidateJwtParameters parameters,
        params string[] validAudiences) =>
        parameters.ValidateClaim(
            JoseClaimNames.Payload.Aud,
            allowCollection: true,
            validAudiences);

    private static ValidateJwtAsync ValidatePayloadClaimValue(
        string claimName,
        bool allowCollection,
        ICollection<string> validValues) => (context, cancellationToken) =>
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!context.DecodedJwt.Payload.TryGetProperty(claimName, out var property))
            throw new Exception("TODO");

        // ReSharper disable once ConvertIfStatementToSwitchStatement
        if (property.ValueKind == JsonValueKind.Null)
        {
            throw new Exception("TODO");
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            if (!allowCollection)
                throw new Exception("TODO");

            if (property.EnumerateArray().Select(jsonElement => jsonElement.ToString()).Except(validValues).Any())
                throw new Exception("TODO");
        }
        else
        {
            var stringValue = property.ToString();
            if (!validValues.Contains(stringValue))
                throw new Exception("TODO");
        }

        return ValueTask.CompletedTask;
    };
}
