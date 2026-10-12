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

namespace NCode.Identity.OpenId.Authentication.Logic;

/// <summary>
/// Provides lookup over the registered <see cref="ICodeChallengeMethod"/> transformations, keyed by their
/// case-sensitive <c>code_challenge_method</c> value. Register a custom implementation to replace the default behavior.
/// </summary>
[PublicAPI]
public interface ICodeChallengeMethodCollectionProvider
{
    /// <summary>
    /// Gets the case-sensitive set of supported <c>code_challenge_method</c> values.
    /// </summary>
    IReadOnlyCollection<string> SupportedMethods { get; }

    /// <summary>
    /// Attempts to get the <see cref="ICodeChallengeMethod"/> transformation registered for the specified
    /// <c>code_challenge_method</c> value.
    /// </summary>
    /// <param name="method">The case-sensitive <c>code_challenge_method</c> value to look up.</param>
    /// <param name="codeChallengeMethod">When this method returns <see langword="true"/>, contains the matching
    /// <see cref="ICodeChallengeMethod"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a transformation is registered for the specified value; otherwise,
    /// <see langword="false"/>.</returns>
    bool TryGet(string method, [NotNullWhen(true)] out ICodeChallengeMethod? codeChallengeMethod);
}
