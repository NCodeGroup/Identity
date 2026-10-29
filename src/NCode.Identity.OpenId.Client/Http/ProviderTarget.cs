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

namespace NCode.Identity.OpenId.Client.Http;

/// <summary>
/// Where and how a request reaches a provider endpoint.
/// </summary>
/// <param name="Url">The URL the request is sent to: the endpoint, or its mTLS alias.</param>
/// <param name="Uri">The parsed <paramref name="Url"/>.</param>
/// <param name="HttpClientName">The name of the HTTP client that sends the request.</param>
internal readonly record struct ProviderTarget(string Url, Uri Uri, string HttpClientName);
