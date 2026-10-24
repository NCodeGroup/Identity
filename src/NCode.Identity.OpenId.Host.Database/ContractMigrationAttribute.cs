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

namespace NCode.Identity.OpenId.Host.Database;

/// <summary>
/// Marks a migration as a deliberate contract step: it drops, renames, or tightens schema that an earlier release
/// reads, or it changes data so that release fails on or loses a value it reads, and it ships only once no running
/// release reads that shape any more. A migration with neither marker only expands the schema and changes no data,
/// which <see cref="ExpandOnlyRule.Check"/> checks for every migration.
/// </summary>
// See ADR-0184.
[PublicAPI]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ContractMigrationAttribute(string reason) : Attribute
{
    /// <summary>
    /// Gets why the contract step is safe: which release stopped reading the shape this migration removes.
    /// </summary>
    public string Reason { get; } = reason;
}
