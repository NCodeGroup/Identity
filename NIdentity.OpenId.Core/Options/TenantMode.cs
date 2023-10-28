﻿#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NIdentity.OpenId.Options;

/// <summary>
/// Specifies how multi-tenancy is configured.
/// </summary>
public enum TenantMode
{
    /// <summary>
    /// Indicates that multi-tenancy is disabled and information about the single tenant must be explicitly configured.
    /// </summary>
    StaticSingle,

    /// <summary>
    /// Indicates that multi-tenancy is enabled and tenants are discovered by the host address.
    /// </summary>
    DynamicByHost,

    /// <summary>
    /// Indicates that multi-tenancy is enabled and tenants are discovered by the request path.
    /// </summary>
    DynamicByPath
}