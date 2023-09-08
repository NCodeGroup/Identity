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

public static class ValidateJwtParametersExtensions
{
    public static ValidateJwtParameters UseValidationKeys(
        this ValidateJwtParameters parameters,
        IEnumerable<SecretKey> secretKeys)
    {
        parameters.ResolveValidationKeysAsync = (_, _, _, _, _) => ValueTask.FromResult(secretKeys);
        return parameters;
    }

    public static ValidateJwtParameters AddValidator(
        this ValidateJwtParameters parameters,
        ValidateJwtAsync validator)
    {
        parameters.Validators.Add(validator);
        return parameters;
    }

    public static ValidateJwtParameters ValidateClaim(
        this ValidateJwtParameters parameters,
        string claimName,
        bool allowCollection,
        params string[] validValues) =>
        parameters.AddValidator(
            ValidateClaimValue(
                claimName,
                allowCollection,
                validValues.ToHashSet(StringComparer.Ordinal)));

    public static ValidateJwtParameters ValidateIssuer(
        this ValidateJwtParameters parameters,
        params string[] validIssuers) =>
        parameters.ValidateClaim(
            JoseClaimNames.Payload.Iss,
            allowCollection: false,
            validIssuers);

    public static ValidateJwtParameters ValidateAudience(
        this ValidateJwtParameters parameters,
        params string[] validAudiences) =>
        parameters.ValidateClaim(
            JoseClaimNames.Payload.Aud,
            allowCollection: true,
            validAudiences);

    private static ValidateJwtAsync ValidateClaimValue(
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
