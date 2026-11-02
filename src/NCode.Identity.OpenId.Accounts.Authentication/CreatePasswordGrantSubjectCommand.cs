// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Accounts.Authentication;

/// <summary>
/// Represents a mediator command to create the subject (a <see cref="ClaimsPrincipal"/>) for a resource-owner password
/// grant from a validated <see cref="PersistedLocalAccount"/>. The default handler assembles the account's self-issued
/// connection claims and its profile claims; applications replace it to customize the subject (for example, to add
/// authentication-context claims such as <c>amr</c> or <c>auth_time</c>).
/// </summary>
/// <param name="OpenIdContext">The <see cref="OpenIdContext"/> associated with the current request.</param>
/// <param name="Account">The validated <see cref="PersistedLocalAccount"/> the subject is created from.</param>
[PublicAPI]
public readonly record struct CreatePasswordGrantSubjectCommand(
    OpenIdContext OpenIdContext,
    PersistedLocalAccount Account
) : ICommand<ClaimsPrincipal>;
