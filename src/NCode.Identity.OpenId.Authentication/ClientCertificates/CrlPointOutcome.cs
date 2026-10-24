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

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// The outcome of checking a certificate against one CRL distribution point: whether a list at the point revokes the
/// certificate, and the reasons the point answered for.
/// </summary>
/// <param name="IsRevoked">Whether a list at the point revokes the certificate.</param>
/// <param name="CoveredReasons">The reasons the point answered for as a mask, zero when it answered for none.</param>
internal readonly record struct CrlPointOutcome(bool IsRevoked, int CoveredReasons)
{
    /// <summary>
    /// Gets the outcome of a point whose list revokes the certificate.
    /// </summary>
    public static CrlPointOutcome Revoked { get; } = new(true, 0);

    /// <summary>
    /// Gets the outcome of a point that answered for no reason that was not already answered for.
    /// </summary>
    public static CrlPointOutcome Uncovered { get; } = new(false, 0);
}
