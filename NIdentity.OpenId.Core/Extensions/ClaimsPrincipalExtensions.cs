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

using System.Security.Claims;

namespace NIdentity.OpenId.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static string GetSubjectId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(OpenIdConstants.Parameters.Subject) ??
        throw new InvalidOperationException("The principal is missing the 'sub' claim.");

    public static long GetAuthenticationTime(this ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst(OpenIdConstants.Parameters.AuthTime);
        if (claim == null)
            throw new InvalidOperationException("The principal is missing the 'auth_time' claim.");

        if (!long.TryParse(claim.Value, out var epochSeconds))
            throw new InvalidOperationException("Failed to parse the 'auth_time' claim as a number.");

        return epochSeconds;
    }
}
