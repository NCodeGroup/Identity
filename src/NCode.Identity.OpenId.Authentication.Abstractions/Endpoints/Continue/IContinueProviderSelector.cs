// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Continue;

/// <summary>
/// Provides the ability to select an <see cref="IContinueProvider"/> instance given a <see cref="string"/> <c>Continue Code</c>.
/// </summary>
[PublicAPI]
public interface IContinueProviderSelector
{
    /// <summary>
    /// Selects an <see cref="IContinueProvider"/> instance given a <see cref="string"/> <c>Continue Code</c>.
    /// </summary>
    /// <param name="continueCode">The <see cref="string"/> <c>Continue Code</c>.</param>
    /// <returns>The <see cref="IContinueProvider"/> instance.</returns>
    IContinueProvider SelectProvider(string continueCode);
}
