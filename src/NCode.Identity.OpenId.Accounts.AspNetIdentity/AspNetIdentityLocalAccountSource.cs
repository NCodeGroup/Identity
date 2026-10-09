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

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

/// <summary>
/// Adapts an ASP.NET Core Identity <see cref="UserManager{TUser}"/> to the <see cref="ILocalAccountSource"/> seam, so a
/// host already running ASP.NET Core Identity exposes its users as local accounts with no change to the OpenID runtime.
/// An <c>IdentityUser</c> maps to a local account; the principal linkage sits above it unchanged.
/// </summary>
/// <remarks>
/// Registered as a singleton, it creates a dependency-injection scope per call to resolve the scoped
/// <see cref="UserManager{TUser}"/>, so it holds no scoped state. Credential hashing is owned by the
/// <see cref="UserManager{TUser}"/>, so this adapter does not use the <c>IPasswordHasher</c> facade.
/// </remarks>
/// <typeparam name="TUser">The ASP.NET Core Identity user type.</typeparam>
internal sealed class AspNetIdentityLocalAccountSource<TUser>(
    IServiceScopeFactory serviceScopeFactory
) : ILocalAccountSource
    where TUser : class
{
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;

    /// <inheritdoc />
    public async ValueTask<LocalAccount?> ValidateCredentialsAsync(
        OpenIdContext openIdContext,
        string userName,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await userManager.FindByNameAsync(userName);
        if (user is null)
        {
            return null;
        }

        // ASP.NET Core Identity's API is string-based, so the credential is decoded to a string only at this boundary.
        var passwordText = SecureEncoding.UTF8.GetString(password.Span);
        var valid = await userManager.CheckPasswordAsync(user, passwordText);
        if (!valid)
        {
            return null;
        }

        return await MapToLocalAccountAsync(userManager, user);
    }

    /// <inheritdoc />
    public async ValueTask<LocalAccount?> FindBySubjectAsync(
        OpenIdContext openIdContext,
        string subject,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await userManager.FindByIdAsync(subject);
        return user is null ? null : await MapToLocalAccountAsync(userManager, user);
    }

    private static async ValueTask<LocalAccount> MapToLocalAccountAsync(
        UserManager<TUser> userManager,
        TUser user
    )
    {
        var subject = await userManager.GetUserIdAsync(user);
        var claims = await userManager.GetClaimsAsync(user);

        // Feature-detect the optional capabilities so a minimal store does not throw.
        var isEnabled =
            !userManager.SupportsUserLockout || !await userManager.IsLockedOutAsync(user);
        var securityStamp = userManager.SupportsUserSecurityStamp
            ? await userManager.GetSecurityStampAsync(user)
            : null;

        return new LocalAccount
        {
            Subject = subject,
            IsEnabled = isEnabled,
            SecurityStamp = securityStamp,
            Claims = claims.ToList(),
        };
    }
}
