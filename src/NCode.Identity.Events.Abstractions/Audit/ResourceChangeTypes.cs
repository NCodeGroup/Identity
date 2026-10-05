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
/// Defines the well-known change-type discriminators that describe the lifecycle transition an
/// <see cref="IResourceChangeAuditEvent"/> represents.
/// </summary>
[PublicAPI]
public static class ResourceChangeTypes
{
    /// <summary>
    /// The resource was created.
    /// </summary>
    public const string Created = "created";

    /// <summary>
    /// The resource's metadata was updated.
    /// </summary>
    public const string Updated = "updated";

    /// <summary>
    /// The resource was deleted.
    /// </summary>
    public const string Deleted = "deleted";

    /// <summary>
    /// The resource's settings were updated.
    /// </summary>
    public const string SettingsUpdated = "settings.updated";
}
