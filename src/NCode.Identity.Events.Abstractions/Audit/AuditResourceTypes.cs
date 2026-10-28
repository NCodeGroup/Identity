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
/// Provides the resource type codes that are shared by more than one plane for an
/// <see cref="AuditResource.Type"/>.
/// </summary>
/// <remarks>
/// Resource type is an extensible string vocabulary, not a closed enumeration. This class declares
/// only the types that both the protocol and the management planes reference; other packages declare
/// their own types beside the entities they own (for example <c>ManagementResourceTypes</c>). Values
/// are lower-case and stable, suitable for use as a log or index dimension.
/// </remarks>
[PublicAPI]
public static class AuditResourceTypes
{
    /// <summary>
    /// The resource type code for a user session, shared by the protocol and management planes.
    /// </summary>
    public const string Session = "session";
}
