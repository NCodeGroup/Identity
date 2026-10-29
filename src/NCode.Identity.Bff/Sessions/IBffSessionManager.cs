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

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Creates, loads, updates and ends BFF sessions on top of the session store and the session codec.
/// </summary>
internal interface IBffSessionManager
{
    /// <summary>
    /// Creates a session under a new handle.
    /// </summary>
    ValueTask<BffSessionState> CreateAsync(
        BffSessionMetadata metadata,
        BffSessionPayload payload,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Loads and decrypts the session of a handle.
    /// </summary>
    ValueTask<BffSessionState?> LoadAsync(string handle, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a change to a session with an optimistic read-modify-write.
    /// </summary>
    ValueTask<BffSessionState?> UpdateAsync(
        BffSessionState current,
        Func<BffSessionState, BffSessionState?> update,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Ends a session and records why.
    /// </summary>
    ValueTask EndAsync(
        HttpContext? httpContext,
        string storeKey,
        BffSessionMetadata metadata,
        string reason,
        CancellationToken cancellationToken
    );
}
