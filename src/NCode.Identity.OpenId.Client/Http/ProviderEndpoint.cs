// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// An endpoint of the provider, named by its discovery metadata member.
/// </summary>
/// <param name="MetadataName">The name of the discovery metadata member that holds the endpoint, such as <c>token_endpoint</c>.</param>
/// <param name="Url">The URL of the endpoint.</param>
internal readonly record struct ProviderEndpoint(string MetadataName, string Url);
