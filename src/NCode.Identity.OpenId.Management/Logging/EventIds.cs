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

namespace NCode.Identity.OpenId.Management.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.OpenId.Management</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>3000</c>–<c>3999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier. Each constant is named exactly as the <c>Log</c> method it identifies.
/// </remarks>
internal static class EventIds
{
    private const int Base = 3000;

    public const int SecretGenerationFailed = Base + 1;
    public const int JsonPatchFailed = Base + 2;
    public const int ResourceConflict = Base + 3;
    public const int MissingDependency = Base + 4;
}
