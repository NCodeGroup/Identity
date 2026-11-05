// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyWrapping;

/// <summary>
/// Describes the identity that a referenced key wrapper takes on to reach a key, and the value that binds the access
/// to the owner the server acts for.
/// </summary>
/// <param name="Identity">The identity that the holder of the key grants access to, such as an IAM role ARN, a service
/// account email or a Microsoft Entra tenant ID.</param>
/// <param name="Binding">A value that is unique to the owner the server acts for, which the trust of the identity can
/// require, such as the AWS external ID.</param>
// See ADR-1429.
[PublicAPI]
public sealed record KeyAccess(string Identity, string Binding);
