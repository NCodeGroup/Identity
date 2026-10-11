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
