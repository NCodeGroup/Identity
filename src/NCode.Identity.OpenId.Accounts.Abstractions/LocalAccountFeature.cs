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

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Accounts.Stores;

namespace NCode.Identity.OpenId.Accounts;

/// <summary>
/// A marker service registered when an <see cref="ILocalAccountStore"/> is configured. Surfaces that depend on local
/// accounts — the resource-owner password grant and the local-account management endpoints — feature-detect it through
/// an optional dependency: when it is absent, local accounts are reported as unsupported. The backing store itself is
/// resolved per unit of work and so cannot be injected directly, hence this lightweight, backing-store-agnostic signal.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class LocalAccountFeature;
