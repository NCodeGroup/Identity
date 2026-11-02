// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Registration;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// A registration marker announcing that a local-account store (<see cref="Stores.ILocalAccountStore"/>) implementation
/// has been registered. Every store registration mints it — the generic Entity Framework reference
/// (<c>AddLocalAccountEntityFramework</c>), the ASP.NET Core Identity adapter
/// (<c>AddAspNetIdentityLocalAccountStore</c>), and any custom store — and the opt-in local-account capabilities
/// (resource-owner password grant, interactive credential verifier, local-account management) verify it at composition
/// so a capability cannot be wired up without a store.
/// </summary>
[PublicAPI]
public readonly struct LocalAccountLibrary : IRegistrationMarker<LocalAccountLibrary>;
