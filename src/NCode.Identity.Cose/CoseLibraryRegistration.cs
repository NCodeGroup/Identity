// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Cose.Algorithms;
using NCode.Registration;

namespace NCode.Identity.Cose;

/// <summary>
/// Provides extension methods for the <see cref="IServiceBuilder{TMarker}"/> of the <see cref="CoseLibrary"/> to
/// register further COSE algorithms.
/// </summary>
// See ADR-1409.
[PublicAPI]
public static class CoseLibraryRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> of the <see cref="CoseLibrary"/>.</param>
    extension(IServiceBuilder<CoseLibrary> builder)
    {
        /// <summary>
        /// Registers one more COSE algorithm, such as one of the descriptors that <c>AddCoseLibrary</c> does not
        /// register by default, or an application's own.
        /// </summary>
        /// <param name="algorithm">The algorithm descriptor.</param>
        /// <returns>The same <see cref="IServiceBuilder{T}"/> so that additional calls can be chained.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="algorithm"/> is <c>null</c>.</exception>
        public IServiceBuilder<CoseLibrary> AddCoseAlgorithm(CoseAlgorithm algorithm)
        {
            ArgumentNullException.ThrowIfNull(algorithm);

            builder.ServiceCollection.TryAddEnumerableInstance(algorithm);
            return builder;
        }
    }
}
