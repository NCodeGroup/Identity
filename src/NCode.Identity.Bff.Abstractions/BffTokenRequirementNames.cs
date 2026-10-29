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
/// The known names of token requirements.
/// </summary>
/// <remarks>
/// The names are an open vocabulary: a package may register a requirement under a name that is not listed here. Names
/// are compared ordinally.
/// </remarks>
[PublicAPI]
public static class BffTokenRequirementNames
{
    /// <summary>
    /// The access token of the signed-in user.
    /// </summary>
    public const string User = "user";
}
