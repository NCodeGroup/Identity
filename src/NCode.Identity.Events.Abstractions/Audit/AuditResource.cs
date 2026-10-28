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

namespace NCode.Identity.Events.Audit;

/// <summary>
/// Describes an entity that an audited operation acted on.
/// </summary>
/// <param name="Type">
/// The kind of entity, typically one of the <see cref="AuditResourceTypes"/> codes or a code that a
/// sibling package declares for its own entities.
/// </param>
/// <param name="Id">
/// The identifier of the entity within its <paramref name="Type"/>, or <see langword="null"/> when
/// the entity has no identifier (for example it was not created).
/// </param>
[PublicAPI]
public sealed record AuditResource(string Type, string? Id);
