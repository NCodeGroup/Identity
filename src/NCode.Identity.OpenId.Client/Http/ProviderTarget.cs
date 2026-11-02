// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// Where and how a request reaches a provider endpoint.
/// </summary>
/// <param name="Url">The URL the request is sent to: the endpoint, or its mTLS alias.</param>
/// <param name="Uri">The parsed <paramref name="Url"/>.</param>
/// <param name="HttpClientName">The name of the HTTP client that sends the request.</param>
internal readonly record struct ProviderTarget(string Url, Uri Uri, string HttpClientName);
