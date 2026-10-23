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
using NCode.Identity.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Represents a mediator command that brings everything derived from the server's effective settings in line with
/// them. One replica sends it in the background whenever the server's effective settings differ from the ones they
/// were last reconciled against: after a change to the server's settings document, a configuration reload, or a
/// deployment. Every registered handler runs; the default handlers seed every tenant that lacks a key its settings
/// use and report every stored client the settings make incoherent. A handler is idempotent and continues past an item
/// it cannot handle, because one state may be reconciled more than once.
/// </summary>
/// <param name="ServerId">The identifier of the server whose effective settings are reconciled.</param>
/// <param name="Settings">The server's effective settings that everything derived from them is brought in line with.</param>
// See ADR-0219.
[PublicAPI]
public readonly record struct ReconcileServerSettingsCommand(
    string ServerId,
    IReadOnlySettingCollection Settings
) : ICommand;
