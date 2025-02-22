﻿#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Text.Json;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Persistence.Stores;

public interface IServerStore : IStore
{
    ValueTask<PersistedServer> GetAsync(CancellationToken cancellationToken);

    ValueTask<ConcurrentState<JsonElement>> GetSettingsAsync(
        ConcurrentState<JsonElement> lastKnownState,
        CancellationToken cancellationToken);

    ValueTask<ConcurrentState<IReadOnlyCollection<PersistedSecret>>> GetSecretsAsync(
        ConcurrentState<IReadOnlyCollection<PersistedSecret>> lastKnownState,
        CancellationToken cancellationToken);
}
