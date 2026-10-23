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
/// Contains the known values for <see cref="SecretProtectionContext.Purpose"/>. A value protected for one purpose
/// cannot be unprotected for another purpose, even when the owner and the item are the same.
/// </summary>
// See ADR-0228.
[PublicAPI]
public static class SecretProtectionPurposes
{
    /// <summary>
    /// Contains the purpose for the private or symmetric key material of a persisted secret.
    /// </summary>
    public const string PersistedSecret = "persisted-secret";

    /// <summary>
    /// Contains the purpose for the ASP.NET Core Data Protection key ring.
    /// </summary>
    public const string DataProtectionKeyRing = "data-protection-key-ring";
}
