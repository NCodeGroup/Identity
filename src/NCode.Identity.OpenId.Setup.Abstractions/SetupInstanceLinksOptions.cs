// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Setup;

/// <summary>
/// Describes what the host tells setup about the addresses it serves, so setup can list them when it finishes. The
/// host's composition configures it. It is not bound from configuration.
/// </summary>
[PublicAPI]
public sealed class SetupInstanceLinksOptions
{
    /// <summary>
    /// Gets or sets the host-root path of the API reference page when the host serves one, for example
    /// <c>/scalar/v1</c>, or <see langword="null"/> when it does not. Setup reads it to list the instance's addresses.
    /// </summary>
    public string? ApiReferencePath { get; set; }
}
