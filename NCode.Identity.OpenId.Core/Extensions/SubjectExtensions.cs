#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using JetBrains.Annotations;
using NCode.Identity.Jose;

namespace NCode.Identity.OpenId.Extensions;

[PublicAPI]
public static class SubjectExtensions
{
    public static string? GetSubjectIdOrDefault(
        this IEnumerable<Claim> claims,
        bool allowNameId = false,
        bool allowUpn = false)
    {
        string? nameId = null;
        string? upn = null;

        foreach (var claim in claims.Where(claim => !string.IsNullOrEmpty(claim.Value)))
        {
            switch (claim.Type)
            {
                case JoseClaimNames.Payload.Sub:
                    return claim.Value;

                case ClaimTypes.NameIdentifier when allowNameId:
                    nameId = claim.Value;
                    break;

                case ClaimTypes.Upn when allowUpn:
                    upn = claim.Value;
                    break;
            }
        }

        return nameId ?? upn;
    }
}
