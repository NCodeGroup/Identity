// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Names a key encryption key that the caller references, together with the identity that reaches it.
/// </summary>
/// <param name="KeyReference">The reference to the key encryption key, in the format that the key wrapper defines.</param>
/// <param name="Access">The identity that reaches the key, or <see langword="null"/> to reach it with the identity
/// of the deployment.</param>
// See ADR-1429.
[PublicAPI]
public sealed record ReferencedKey(string KeyReference, KeyAccess? Access);
