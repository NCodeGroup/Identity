// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

/// <summary>
/// One entry of a list <see cref="TestCrls.Build"/> writes: a revoked serial number and the extensions of that entry
/// alone (a reason code, a certificate issuer).
/// </summary>
internal sealed record TestCrlEntry(
    byte[] SerialNumber,
    IReadOnlyList<(string Oid, bool Critical, byte[] Value)>? Extensions = null
);
