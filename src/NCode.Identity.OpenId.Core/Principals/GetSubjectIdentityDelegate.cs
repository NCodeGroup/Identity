// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;

namespace NCode.Identity.OpenId.Principals;

/// <summary>
/// Represents a delegate that extracts a <see cref="ClaimsIdentity"/> from a <see cref="ClaimsPrincipal"/>.
/// </summary>
/// <param name="subject">The <see cref="ClaimsPrincipal"/> to extract the <see cref="ClaimsIdentity"/> from.</param>
/// <returns>The <see cref="ClaimsIdentity"/> from the <see cref="ClaimsPrincipal"/>.</returns>
public delegate ClaimsIdentity GetSubjectIdentityDelegate(ClaimsPrincipal subject);
