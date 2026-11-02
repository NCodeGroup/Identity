// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography.X509Certificates;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

/// <summary>
/// One distribution point <see cref="TestCrls.DistributionPoints"/> writes: the location of a list, the reasons that
/// list covers as a mask (bit <c>i</c> is reason <c>i</c>), and the issuer of the list when it is not the certificate's
/// issuer.
/// </summary>
internal sealed record TestDistributionPoint(
    string Uri,
    int? Reasons = null,
    X500DistinguishedName? CrlIssuer = null
);
