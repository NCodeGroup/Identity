#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

/// <summary>
/// Holds a hash that the host's ASP.NET Core Identity password hasher made of a random password nobody knows. The
/// adapter verifies a submitted password against it when no user it may verify has a hash, so a failed verification
/// takes as long whichever user the user name names.
/// </summary>
/// <typeparam name="TUser">The ASP.NET Core Identity user type.</typeparam>
// See ADR-0239.
internal sealed class AspNetIdentityDummyPasswordHash<TUser>
    where TUser : IdentityUser, new()
{
    private string? Hash { get; set; }

    /// <summary>
    /// Gets the dummy hash, making it with the specified password hasher on the first call.
    /// </summary>
    /// <param name="passwordHasher">The host's ASP.NET Core Identity password hasher.</param>
    /// <returns>The dummy hash.</returns>
    public string GetOrCreate(IPasswordHasher<TUser> passwordHasher)
    {
        if (Hash is { } existing)
        {
            return existing;
        }

        // The hash is process-wide and can be rebuilt, so two first verifications that race only make a second hash;
        // either one serves.
        var password = RandomNumberGenerator.GetHexString(32);
        var user = new TUser();
        var hash = passwordHasher.HashPassword(user, password);
        Hash = hash;
        return hash;
    }
}
