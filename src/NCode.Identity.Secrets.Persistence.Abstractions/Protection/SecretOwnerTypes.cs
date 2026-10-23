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

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Contains the known values for <see cref="SecretOwner.OwnerType"/>. The owner type is an open string, so these are
/// the owner types that are known to the library and not necessarily all of the owner types that exist.
/// </summary>
[PublicAPI]
public static class SecretOwnerTypes
{
    /// <summary>
    /// Contains the owner type for secrets that belong to the server as a whole.
    /// </summary>
    public const string Server = "server";

    /// <summary>
    /// Contains the owner type for secrets that belong to a single tenant.
    /// </summary>
    public const string Tenant = "tenant";
}
