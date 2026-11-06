// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The claim types that the BFF adds to the principal of a session, beside the claims of the provider.
/// </summary>
/// <remarks>
/// The types are reserved: a <see cref="IBffSessionClaimContributor"/> can neither add nor remove them, and the claims of
/// a provider that use them are dropped.
/// </remarks>
[PublicAPI]
public static class BffClaimTypes
{
    /// <summary>
    /// The action of the session's logout form: the URL that the form posts to.
    /// </summary>
    public const string LogoutUrl = "bff_logout_url";

    /// <summary>
    /// The logout guard of the session: the value of the <c>sid</c> field of the logout form.
    /// </summary>
    public const string LogoutSid = "bff_logout_sid";
}
