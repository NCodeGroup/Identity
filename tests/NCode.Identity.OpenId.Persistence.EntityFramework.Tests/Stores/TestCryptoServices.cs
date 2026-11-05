// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cryptography;

namespace NCode.Identity.OpenId.Persistence.EntityFramework.Stores;

/// <summary>
/// Provides <see cref="ICryptoService"/> instances for the store tests.
/// </summary>
internal static class TestCryptoServices
{
    /// <summary>
    /// Creates an <see cref="ICryptoService"/> that generates a unique resource identifier on each call.
    /// </summary>
    /// <returns>The <see cref="ICryptoService"/> instance.</returns>
    public static ICryptoService CreateResourceIdGenerator()
    {
        var mockCryptoService = new Mock<ICryptoService>(MockBehavior.Strict);

        mockCryptoService
            .Setup(x => x.GenerateKey(16, BinaryEncodings.Base64Url))
            .Returns(() => Guid.NewGuid().ToString("N"))
            .Verifiable();

        return mockCryptoService.Object;
    }
}
