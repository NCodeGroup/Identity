// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;

namespace NCode.Identity.OpenId.Exceptions;

/// <summary>
/// Provides extension methods for a sequence of <see cref="IOpenIdExceptionTranslator"/> strategies.
/// </summary>
// See ADR-1428.
[PublicAPI]
public static class OpenIdExceptionTranslatorExtensions
{
    /// <param name="translators">The <see cref="IOpenIdExceptionTranslator"/> strategies to ask, in order.</param>
    extension(IEnumerable<IOpenIdExceptionTranslator> translators)
    {
        /// <summary>
        /// Attempts to map an exception to an <see cref="IOpenIdError"/>, returning the answer of the first translator
        /// that answers.
        /// </summary>
        /// <param name="openIdEnvironment">The <see cref="OpenIdEnvironment"/> instance associated with the current request.</param>
        /// <param name="exception">The exception that occurred.</param>
        /// <param name="error">When this method returns <c>true</c>, the <see cref="IOpenIdError"/> that represents the exception.</param>
        /// <returns><c>true</c> if a translator answered; otherwise, <c>false</c>.</returns>
        public bool TryTranslate(
            OpenIdEnvironment openIdEnvironment,
            Exception exception,
            [NotNullWhen(true)] out IOpenIdError? error
        )
        {
            foreach (var translator in translators)
            {
                if (translator.TryTranslate(openIdEnvironment, exception, out error))
                    return true;
            }

            error = null;
            return false;
        }
    }
}
