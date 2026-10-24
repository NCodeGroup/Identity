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

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

/// <summary>
/// Adapts an ASP.NET Core Identity <see cref="UserManager{TUser}"/> to the backing-store-agnostic
/// <see cref="ILocalAccountStore"/> seam, so a host already running ASP.NET Core Identity exposes its users as local
/// accounts (enabling the resource-owner password grant and the local-account management endpoints) with no change to
/// the OpenID runtime. It hashes a password with the user manager's password hasher and stores the hash through the
/// user store, so no password validator of ASP.NET Core Identity judges a password the password policy accepted;
/// security stamps and concurrency are owned by the <see cref="UserManager{TUser}"/>. It verifies the NFC form of a
/// submitted password, then the password as submitted, and verifies a password against a dummy hash when it holds no
/// hash it may verify, so a failed verification takes as long whether or not the user exists.
/// </summary>
/// <remarks>
/// ASP.NET Core Identity users are global (tenant-independent), so <see cref="PersistedLocalAccount.TenantId"/> is mapped
/// as empty; the resource-owner password grant authenticates against the request's tenant, not the account's. Each
/// operation resolves the scoped <see cref="UserManager{TUser}"/> from a fresh dependency-injection scope, so this
/// adapter holds no scoped state and its writes commit immediately (they do not enlist in the OpenID unit of work).
/// </remarks>
/// <typeparam name="TUser">The ASP.NET Core Identity user type.</typeparam>
internal sealed class AspNetIdentityLocalAccountStore<TUser>(
    IServiceScopeFactory serviceScopeFactory,
    IStoreProvider storeProvider,
    AspNetIdentityDummyPasswordHash<TUser> dummyPasswordHash
) : ILocalAccountStore
    where TUser : IdentityUser, new()
{
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private IStoreProvider StoreProvider { get; } = storeProvider;
    private AspNetIdentityDummyPasswordHash<TUser> DummyPasswordHash { get; } = dummyPasswordHash;

    /// <inheritdoc />
    public object? GetService(Type serviceType) => StoreProvider.GetService(serviceType);

    /// <inheritdoc />
    public TStore GetStore<TStore>()
        where TStore : IStore => StoreProvider.GetStore<TStore>();

    /// <inheritdoc />
    public async ValueTask AddAsync(
        PersistedLocalAccount account,
        ReadOnlyMemory<byte>? password,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        // The id is assigned by the orchestrating create handler (it is also the self-issued identity's subject), so it
        // must be carried onto the Identity user rather than letting the store generate its own.
        var user = new TUser
        {
            Id = account.LocalAccountId,
            UserName = account.UserName,
            Email = account.Email,
            EmailConfirmed = account.EmailVerified,
        };

        if (password is { } value)
        {
            var passwordStore = GetPasswordStore(scope.ServiceProvider);
            var submitted = DecodePassword(value);
            var normalized = NormalizePassword(submitted);
            await SetPasswordHashAsync(
                userManager,
                passwordStore,
                user,
                normalized,
                cancellationToken
            );
        }

        ThrowIfFailed(await userManager.CreateAsync(user), "create the local account");

        foreach (var claim in account.Claims)
        {
            ThrowIfFailed(
                await userManager.AddClaimAsync(user, new Claim(claim.Type, claim.Value)),
                "add a local-account claim"
            );
        }

        if (!account.IsEnabled)
        {
            await DisableAsync(userManager, user);
        }
    }

    /// <inheritdoc />
    public async ValueTask UpdateAsync(
        PersistedLocalAccount account,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await GetRequiredUserAsync(userManager, account.LocalAccountId);

        // SetUserNameAsync/SetEmailAsync also refresh the normalized lookup columns; setting the properties directly
        // would leave those stale. SetEmailAsync resets the confirmed flag, so the confirmation is applied after it.
        ThrowIfFailed(
            await userManager.SetUserNameAsync(user, account.UserName),
            "update the username"
        );
        ThrowIfFailed(await userManager.SetEmailAsync(user, account.Email), "update the email");
        user.EmailConfirmed = account.EmailVerified;

        if (account.IsEnabled)
        {
            await EnableAsync(userManager, user);
        }
        else
        {
            await DisableAsync(userManager, user);
        }

        ThrowIfFailed(await userManager.UpdateAsync(user), "update the local account");
    }

    /// <inheritdoc />
    public async ValueTask SetPasswordAsync(
        string localAccountId,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();
        var passwordStore = GetPasswordStore(scope.ServiceProvider);

        var user = await GetRequiredUserAsync(userManager, localAccountId);

        var submitted = DecodePassword(password);
        var normalized = NormalizePassword(submitted);
        await SetPasswordHashAsync(userManager, passwordStore, user, normalized, cancellationToken);

        // Rotating the security stamp, which also saves the user, invalidates the sessions and tokens issued before the
        // reset.
        ThrowIfFailed(
            await userManager.UpdateSecurityStampAsync(user),
            "reset the local-account password"
        );
    }

    /// <inheritdoc />
    public async ValueTask<PersistedLocalAccount?> VerifyCredentialAsync(
        string userName,
        ReadOnlyMemory<byte> password,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var serviceProvider = scope.ServiceProvider;
        var userManager = serviceProvider.GetRequiredService<UserManager<TUser>>();
        var passwordStore = GetPasswordStore(serviceProvider);
        var passwordHasher = userManager.PasswordHasher;

        // Made before the user is looked up, so the first verification costs the same whichever user name it names.
        var dummyPasswordHash = DummyPasswordHash.GetOrCreate(passwordHasher);

        var submitted = DecodePassword(password);
        var normalized = NormalizePassword(submitted);

        var user = await userManager.FindByNameAsync(userName);
        var passwordHash = user is null
            ? null
            : await GetVerifiablePasswordHashAsync(
                userManager,
                passwordStore,
                user,
                cancellationToken
            );

        // An unknown user name, a locked-out user and a user without a password are verified against the dummy hash, so
        // a wrong password takes as long for them as for a user whose hash is verified. See ADR-0239.
        var verifiedUser = user ?? new TUser();
        var verifiedHash = passwordHash ?? dummyPasswordHash;
        var result = VerifyPassword(
            passwordHasher,
            verifiedUser,
            verifiedHash,
            normalized,
            submitted
        );
        if (user is null || passwordHash is null || result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            await SetPasswordHashAsync(
                userManager,
                passwordStore,
                user,
                normalized,
                cancellationToken
            );

            // The credential is unchanged, so the security stamp is kept; when the user validators refuse the update,
            // the stored hash, which still verifies, stays.
            _ = await userManager.UpdateAsync(user);
        }

        return await MapAsync(userManager, user);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedLocalAccount?> GetByIdOrDefaultAsync(
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await userManager.FindByIdAsync(localAccountId);
        return user is null ? null : await MapAsync(userManager, user);
    }

    /// <inheritdoc />
    public async ValueTask<PersistedLocalAccount?> GetByUserNameOrDefaultAsync(
        string userName,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await userManager.FindByNameAsync(userName);
        return user is null ? null : await MapAsync(userManager, user);
    }

    /// <inheritdoc />
    public async ValueTask<PagedResult<PersistedLocalAccount>> GetPageAsync(
        PageRequest page,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        if (!userManager.SupportsQueryableUsers)
        {
            throw new NotSupportedException(
                "The configured ASP.NET Core Identity store does not support querying users."
            );
        }

        var query = userManager.Users;
        if (!string.IsNullOrEmpty(page.Cursor))
        {
            var cursor = page.Cursor;
            // Keyset by id; the untyped comparison is translated to a SQL range predicate by the provider (the database
            // collation, not an in-memory ordinal comparison, orders the page), so CA1309 does not apply here.
#pragma warning disable CA1309
            query = query.Where(user => string.Compare(user.Id, cursor) > 0);
#pragma warning restore CA1309
        }

        var limit = page.Limit;

        // Fetch one extra row to determine whether a further page exists.
        var users = query.OrderBy(user => user.Id).Take(limit + 1).ToList();

        var hasMore = users.Count > limit;
        var items = new List<PersistedLocalAccount>(Math.Min(users.Count, limit));
        for (var index = 0; index < users.Count && index < limit; index++)
        {
            items.Add(await MapAsync(userManager, users[index]));
        }

        var nextCursor = hasMore ? items[^1].LocalAccountId : null;
        return new PagedResult<PersistedLocalAccount> { Items = items, NextCursor = nextCursor };
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string localAccountId, CancellationToken cancellationToken)
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await userManager.FindByIdAsync(localAccountId);
        if (user is null)
        {
            return;
        }

        ThrowIfFailed(await userManager.DeleteAsync(user), "delete the local account");
    }

    /// <inheritdoc />
    public async ValueTask ReplaceClaimsAsync(
        string localAccountId,
        IReadOnlyList<PersistedLocalAccountClaim> claims,
        CancellationToken cancellationToken
    )
    {
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TUser>>();

        var user = await GetRequiredUserAsync(userManager, localAccountId);

        var existing = await userManager.GetClaimsAsync(user);
        if (existing.Count > 0)
        {
            ThrowIfFailed(
                await userManager.RemoveClaimsAsync(user, existing),
                "remove the existing local-account claims"
            );
        }

        if (claims.Count > 0)
        {
            ThrowIfFailed(
                await userManager.AddClaimsAsync(
                    user,
                    claims.Select(claim => new Claim(claim.Type, claim.Value))
                ),
                "add the local-account claims"
            );
        }
    }

    private static async ValueTask<PersistedLocalAccount> MapAsync(
        UserManager<TUser> userManager,
        TUser user
    )
    {
        var claims = await userManager.GetClaimsAsync(user);
        var isEnabled =
            !userManager.SupportsUserLockout || !await userManager.IsLockedOutAsync(user);

        return new PersistedLocalAccount
        {
            // ASP.NET Core Identity users are global; the grant authenticates against the request's tenant.
            TenantId = string.Empty,
            LocalAccountId = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email,
            EmailVerified = user.EmailConfirmed,
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            IsEnabled = isEnabled,
            Claims = claims
                .Select(claim => new PersistedLocalAccountClaim
                {
                    Type = claim.Type,
                    Value = claim.Value,
                })
                .ToList(),
            ConcurrencyToken = user.ConcurrencyStamp ?? string.Empty,
        };
    }

    private static async ValueTask EnableAsync(UserManager<TUser> userManager, TUser user)
    {
        if (userManager.SupportsUserLockout)
        {
            ThrowIfFailed(
                await userManager.SetLockoutEndDateAsync(user, lockoutEnd: null),
                "enable the local account"
            );
        }
    }

    private static async ValueTask DisableAsync(UserManager<TUser> userManager, TUser user)
    {
        if (!userManager.SupportsUserLockout)
        {
            throw new NotSupportedException(
                "The configured ASP.NET Core Identity store does not support lockout, which is required to disable a local account."
            );
        }

        ThrowIfFailed(
            await userManager.SetLockoutEnabledAsync(user, enabled: true),
            "enable lockout for the local account"
        );
        ThrowIfFailed(
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue),
            "disable the local account"
        );
    }

    private static async ValueTask<TUser> GetRequiredUserAsync(
        UserManager<TUser> userManager,
        string localAccountId
    ) =>
        await userManager.FindByIdAsync(localAccountId)
        ?? throw new InvalidOperationException(
            $"A local account with '{localAccountId}' was not found."
        );

    private static IUserPasswordStore<TUser> GetPasswordStore(IServiceProvider serviceProvider) =>
        serviceProvider.GetRequiredService<IUserStore<TUser>>() as IUserPasswordStore<TUser>
        ?? throw new NotSupportedException(
            "The configured ASP.NET Core Identity store does not store passwords, which a local account's password requires."
        );

    private static async ValueTask SetPasswordHashAsync(
        UserManager<TUser> userManager,
        IUserPasswordStore<TUser> passwordStore,
        TUser user,
        string password,
        CancellationToken cancellationToken
    )
    {
        // The hash is made with the user manager's hasher and stored through the user store, so no password validator
        // of ASP.NET Core Identity judges a password the password policy accepted. See ADR-0238.
        var passwordHash = userManager.PasswordHasher.HashPassword(user, password);
        await passwordStore.SetPasswordHashAsync(user, passwordHash, cancellationToken);
    }

    private static async ValueTask<string?> GetVerifiablePasswordHashAsync(
        UserManager<TUser> userManager,
        IUserPasswordStore<TUser> passwordStore,
        TUser user,
        CancellationToken cancellationToken
    )
    {
        // A locked-out user's password is not verified: the lockout defends against guessing, so a correct password must
        // not be told apart from a wrong one. See ADR-0229.
        if (userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        // An empty hash is no password, as in the Entity Framework store; the host's hasher would refuse it without the
        // work of a verification. See ADR-0239.
        var passwordHash = await passwordStore.GetPasswordHashAsync(user, cancellationToken);
        return string.IsNullOrEmpty(passwordHash) ? null : passwordHash;
    }

    private static PasswordVerificationResult VerifyPassword(
        IPasswordHasher<TUser> passwordHasher,
        TUser user,
        string passwordHash,
        string normalized,
        string submitted
    )
    {
        var result = passwordHasher.VerifyHashedPassword(user, passwordHash, normalized);
        if (
            result != PasswordVerificationResult.Failed
            || string.Equals(normalized, submitted, StringComparison.Ordinal)
        )
        {
            return result;
        }

        // A stored hash of the password as submitted still verifies and is replaced by a hash of the NFC form. See ADR-0238.
        var submittedResult = passwordHasher.VerifyHashedPassword(user, passwordHash, submitted);
        return submittedResult == PasswordVerificationResult.Failed
            ? PasswordVerificationResult.Failed
            : PasswordVerificationResult.SuccessRehashNeeded;
    }

    // ASP.NET Core Identity's credential API is string-based, so the credential is decoded to a string only here.
    private static string DecodePassword(ReadOnlyMemory<byte> password) =>
        SecureEncoding.UTF8.GetString(password.Span);

    // A password is hashed and verified in its Unicode Normalization Form C (NFC). See ADR-0238.
    private static string NormalizePassword(string password) =>
        password.Normalize(NormalizationForm.FormC);

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"Failed to {action}: {errors}");
    }
}
