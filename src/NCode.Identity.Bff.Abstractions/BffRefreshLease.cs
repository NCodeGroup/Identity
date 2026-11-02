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

namespace NCode.Identity.Bff;

/// <summary>
/// A refresh lease that an <see cref="IBffSessionStore"/> granted: the key it guards and the random owner token that
/// only its holder knows, so a release removes only this hold.
/// </summary>
/// <param name="LeaseKey">The key of the lease.</param>
/// <param name="Owner">The owner token the store recorded when it granted the lease.</param>
// See ADR-1308.
[PublicAPI]
public readonly record struct BffRefreshLease(string LeaseKey, string Owner);
