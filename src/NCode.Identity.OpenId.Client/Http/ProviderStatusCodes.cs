// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// The HTTP status codes the client compares a provider response against.
/// </summary>
internal static class ProviderStatusCodes
{
    /// <summary>
    /// The <c>200 OK</c> status code.
    /// </summary>
    public const int Status200OK = 200;

    /// <summary>
    /// The <c>201 Created</c> status code.
    /// </summary>
    public const int Status201Created = 201;
}
