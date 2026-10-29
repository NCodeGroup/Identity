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

using System.Text.Json.Serialization;

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// The non-production environment a tenant's pages are served for, which the host embeds in the branding so the
/// single-page bundle can show that the page is not a production one. A production tenant has none.
/// </summary>
/// <param name="Type">The name of the tenant's effective environment type, such as <c>staging</c>.</param>
/// <param name="Name">The tenant's own name for its environment, or <see langword="null"/> when it has none.</param>
// See ADR-0289.
internal sealed record InteractiveEnvironment(
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Name
);
