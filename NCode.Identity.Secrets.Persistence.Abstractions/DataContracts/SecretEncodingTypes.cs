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

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.DataContracts;

/// <summary>
/// Contains constants for the possible values of the <see cref="PersistedSecret.EncodingType"/> property.
/// </summary>
[PublicAPI]
public static class SecretEncodingTypes
{
    /// <summary>
    /// Indicates that the secret is stored as base64url without encryption.
    /// </summary>
    public const string Basic = "basic";
}
