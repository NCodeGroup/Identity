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

namespace NCode.Identity.Jwt;

internal class ValidateClaimValueJwtHandler : ValidateJwtHandler
{
    private string ClaimName { get; }
    private bool AllowCollection { get; }
    private ISet<string> ValidValues { get; }

    public ValidateClaimValueJwtHandler(string claimName, bool allowCollection, IEnumerable<string> validValues)
    {
        ClaimName = claimName;
        AllowCollection = allowCollection;
        ValidValues = validValues.ToHashSet(StringComparer.Ordinal);
    }

    protected override void Validate(ValidateJwtContext context)
    {
        if (!context.DecodedJwt.Payload.TryGetProperty(ClaimName, out var property))
            throw new Exception("TODO");

        if (property.ValueKind == JsonValueKind.Null)
        {
            throw new Exception("TODO");
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            if (!AllowCollection)
                throw new Exception("TODO");

            if (property.EnumerateArray().Select(jsonElement => jsonElement.ToString()).Except(ValidValues).Any())
                throw new Exception("TODO");
        }
        else
        {
            var stringValue = property.ToString();
            if (!ValidValues.Contains(stringValue))
                throw new Exception("TODO");
        }
    }
}
