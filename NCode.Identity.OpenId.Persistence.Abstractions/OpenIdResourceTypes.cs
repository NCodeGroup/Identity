#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Persistence;

/// <summary>
/// Contains constants used to identify various resource types.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public static class OpenIdResourceTypes
{
    /// <summary>
    /// Contains the value used to identify the resource type for a collection of settings.
    /// </summary>
    public const string Settings = "settings";

    /// <summary>
    /// Contains the value used to identify the resource type for a server.
    /// </summary>
    public const string Server = "server";

    /// <summary>
    /// Contains the value used to identify the resource type for a tenant.
    /// </summary>
    public const string Tenant = "tenant";

    /// <summary>
    /// Contains the value used to identify the resource type for a client.
    /// </summary>
    public const string Client = "client";
}
