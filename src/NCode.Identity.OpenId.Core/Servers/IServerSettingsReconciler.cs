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

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Brings everything derived from a server's effective settings in line with them whenever those settings differ from
/// the ones they were last reconciled against.
/// </summary>
// See ADR-0219.
internal interface IServerSettingsReconciler
{
    /// <summary>
    /// Reconciles the server's effective settings when they differ from the ones recorded as reconciled, with one
    /// replica per change.
    /// </summary>
    /// <param name="serverId">The identifier of the server whose effective settings are reconciled.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The result is
    /// <see langword="true"/> when the server's effective settings are reconciled, whether already or by this call, and
    /// <see langword="false"/> when the caller should try again later because another replica holds the lease or the
    /// record lost a race with a change to the server.
    /// </returns>
    ValueTask<bool> ReconcileAsync(string serverId, CancellationToken cancellationToken);
}
