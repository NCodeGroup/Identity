// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The known names of token requirements.
/// </summary>
/// <remarks>
/// The names are an open vocabulary: a package may register a requirement under a name that is not listed here. Names
/// are compared ordinally.
/// </remarks>
[PublicAPI]
public static class BffTokenRequirementNames
{
    /// <summary>
    /// The access token of the signed-in user.
    /// </summary>
    public const string User = "user";
}
