// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The <see cref="IInteractivePasswordChanger"/> of a surface whose backend holds no password to change: it always
/// reports <see cref="InteractivePasswordChangeResult.NotSupported"/>. The surface offers no password change until a
/// backend that holds passwords replaces it.
/// </summary>
internal sealed class DefaultInteractivePasswordChanger : IInteractivePasswordChanger
{
    /// <inheritdoc />
    public ValueTask<InteractivePasswordChangeResult> ChangePasswordAsync(
        OpenIdContext openIdContext,
        ClaimsPrincipal principal,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(InteractivePasswordChangeResult.NotSupported);
}
