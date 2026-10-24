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
