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

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// A decrypted BFF session together with the handle and version it was read at.
/// </summary>
/// <param name="Handle">The secret session handle that the browser carries.</param>
/// <param name="StoreKey">The key of the session record in the store.</param>
/// <param name="Version">The version of the session record.</param>
/// <param name="Metadata">The plaintext metadata of the session.</param>
/// <param name="Payload">The decrypted payload of the session.</param>
internal sealed record BffSessionState(
    string Handle,
    string StoreKey,
    long Version,
    BffSessionMetadata Metadata,
    BffSessionPayload Payload
);
