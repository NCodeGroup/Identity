// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Cose;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cwt.Validation;

/// <summary>
/// Resolves the keys that may verify a token from the headers of its authenticated message, such as its <c>kid</c>.
/// The headers are not authenticated yet; the resolver chooses candidates and the verification decides.
/// </summary>
/// <param name="message">The type and headers of the authenticated message of the token.</param>
/// <param name="cancellationToken">A token to cancel the operation.</param>
/// <returns>The keys to try.</returns>
[PublicAPI]
public delegate ValueTask<IReadOnlyCollection<SecretKey>> ResolveCwtValidationKeysAsync(
    CoseMessageInfo message,
    CancellationToken cancellationToken
);
