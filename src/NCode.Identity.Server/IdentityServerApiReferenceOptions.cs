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

using JetBrains.Annotations;

namespace NCode.Identity.Server;

/// <summary>
/// Controls what <c>MapIdentityServerApiReference</c> maps. The OpenAPI document (the spec JSON) and the API reference
/// UI are independent toggles: a host may expose the machine-readable spec without the UI, serve the UI from a document
/// hosted elsewhere, or disable both in environments where neither should be reachable.
/// </summary>
[PublicAPI]
public sealed class IdentityServerApiReferenceOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the OpenAPI document (the spec JSON) endpoint is mapped.
    /// The default value is <see langword="true"/>.
    /// </summary>
    public bool MapOpenApiDocument { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the Scalar API reference UI is mapped. The UI fetches the OpenAPI
    /// document at runtime, so it must be reachable (either mapped here or served elsewhere) for the UI to load.
    /// The default value is <see langword="true"/>.
    /// </summary>
    public bool MapApiReferenceUi { get; set; } = true;
}
