// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.Bff.Blazor.Client;

/// <summary>
/// The user a <c>/bff/user</c> document describes: the principal built from the document and the instant the session
/// goes idle.
/// </summary>
/// <param name="Principal">The principal the document's claims, logout URL and logout guard build.</param>
/// <param name="IdleExpiresAt">
/// The instant the session's idle timeout ends, or <see langword="null"/> when the document carries none.
/// </param>
internal sealed record BffClientUser(ClaimsPrincipal Principal, DateTimeOffset? IdleExpiresAt);
