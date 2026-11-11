// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Registration;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides a type discriminator for <see cref="IServiceBuilder{T}"/> identifying the settings library. It is minted
/// by <c>AddSettingsLibrary</c>; the libraries built on settings require its builder.
/// </summary>
[PublicAPI]
public readonly struct SettingsLibrary : IRegistrationMarker<SettingsLibrary>;
