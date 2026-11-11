// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Registration;

namespace NCode.Identity.Persistence;

/// <summary>
/// A registration marker announcing that a persistence store-manager framework (an <c>IStoreManager</c> provider, such
/// as the Entity Framework one) has been registered. A store slice — which registers a store factory the store manager
/// resolves — requires this builder as its receiver, so a slice cannot be composed without the framework it depends on.
/// </summary>
[PublicAPI]
public readonly struct StoreManagerLibrary : IRegistrationMarker<StoreManagerLibrary>;
