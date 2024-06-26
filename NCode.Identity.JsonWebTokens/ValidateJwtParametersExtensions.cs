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
using JetBrains.Annotations;
using NCode.Identity.JsonWebTokens.Exceptions;
using NCode.Identity.Secrets;

namespace NCode.Identity.JsonWebTokens;

/// <summary>
/// Contains extension methods for the <see cref="ValidateJwtParameters"/> class.
/// </summary>
[PublicAPI]
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
        parameters.ResolveValidationKeysAsync = (_, _, _, _) => ValueTask.FromResult(secretKeys);
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
    /// Adds a validator that asserts the specified <paramref name="claimName"/> exists in the Json Web Token (JWT) with any
    /// value specified from <paramref name="validValues"/>. If the claim does not exist or the value is not one of the valid
    /// values, then an exception is thrown.
    /// </summary>
    /// <param name="parameters">The <see cref="ValidateJwtParameters"/> instance.</param>
    /// <param name="claimName">The name of the claim to validate.</param>
    /// <param name="usePayload"><c>true</c> to check the payload claims, <c>false</c> to check the header claims.</param>
    /// <param name="allowCollection"><c>true</c> if the claim allows multiple values; <c>false</c> if the claim must be a single value.</param>
    /// <param name="validValues">The collection of allowable values for the claim.</param>
    /// <returns>The <see cref="ValidateJwtParameters"/> instance for method chaining.</returns>
    public static ValidateJwtParameters ValidateClaim(
        this ValidateJwtParameters parameters,
        string claimName,
        bool usePayload,
        bool allowCollection,
        IEnumerable<string> validValues)
    {
        return parameters.AddValidator((context, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            var claims = usePayload ? context.DecodedJwt.Payload : context.DecodedJwt.Header;
            if (!claims.TryGetProperty(claimName, out var property))
                throw new TokenValidationException($"The claim '{claimName}' is missing.");

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (property.ValueKind == JsonValueKind.Null)
            {
                throw new TokenValidationException($"The claim '{claimName}' is null.");
            }

            if (property.ValueKind == JsonValueKind.Array)
            {
                if (!allowCollection)
                    throw new TokenValidationException($"The claim '{claimName}' does not allow multiple values.");

                if (property.EnumerateArray().Select(jsonElement => jsonElement.ToString()).Except(validValues).Any())
                    throw new TokenValidationException($"The claim '{claimName}' is invalid because at least one value did not contain any of the valid values.");
            }
            else
            {
                var stringValue = property.ToString();
                if (!validValues.Contains(stringValue))
                    throw new TokenValidationException($"The claim '{claimName}' is invalid because it does not contain any of the valid values.");
            }

            return ValueTask.CompletedTask;
        });
    }
}
