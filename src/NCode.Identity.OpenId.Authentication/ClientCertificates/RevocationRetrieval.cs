// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Holds the outcome of one retrieval of revocation information: the value it yielded and the time the value may be
/// reused until. The default value means that nothing usable was retrieved, which the
/// <see cref="RevocationRetrievalCache"/> remembers.
/// </summary>
/// <param name="Value">The value the retrieval yielded, or <see langword="null"/> when nothing usable was retrieved.</param>
/// <param name="CacheUntil">The time the value may be reused until, or <see langword="null"/> when it is not cached.</param>
/// <typeparam name="T">The type of the retrieved value.</typeparam>
internal readonly record struct RevocationRetrieval<T>(T? Value, DateTimeOffset? CacheUntil)
    where T : class;
