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
/// Marks a migration whose data change keeps the previous release working: every row its raw SQL or seed-data change
/// writes is one the previous release does not read, or reads without failing, with the same meaning or a restriction
/// this release applies too.
/// </summary>
// See ADR-0184 and ADR-0250.
[PublicAPI]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ExpandMigrationAttribute(string reason) : Attribute
{
    /// <summary>
    /// Gets why the data change keeps the previous release working.
    /// </summary>
    public string Reason { get; } = reason;
}
