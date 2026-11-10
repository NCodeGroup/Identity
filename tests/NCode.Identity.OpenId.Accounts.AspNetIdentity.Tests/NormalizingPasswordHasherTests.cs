// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Identity;

namespace NCode.Identity.OpenId.Accounts.AspNetIdentity;

public sealed class NormalizingPasswordHasherTests : IDisposable
{
    private const string ComposedPassword = "caf\u00E9 correct horse";
    private const string DecomposedPassword = "cafe\u0301 correct horse";
    private const string UnpairedPassword = "correct\uD800horse";
    private const string StoredHash = "stored-hash";
    private const string MadeHash = "made-hash";

    private static IdentityUser User { get; } = new("alice");

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IPasswordHasher<IdentityUser>> InnerHasher { get; }
    private NormalizingPasswordHasher<IdentityUser> Hasher { get; }

    public NormalizingPasswordHasherTests()
    {
        InnerHasher = Mocks.Create<IPasswordHasher<IdentityUser>>();
        Hasher = new NormalizingPasswordHasher<IdentityUser>(InnerHasher.Object);
    }

    public void Dispose() => Mocks.Verify();

    #region HashPassword Tests

    [Fact]
    public void HashPassword_WhenThePasswordIsDecomposed_HashesTheComposedForm()
    {
        InnerHasher
            .Setup(x => x.HashPassword(User, ComposedPassword))
            .Returns(MadeHash)
            .Verifiable();

        var hash = Hasher.HashPassword(User, DecomposedPassword);

        Assert.Equal(MadeHash, hash);
    }

    [Fact]
    public void HashPassword_WhenThePasswordHasAnUnpairedSurrogate_HashesItAsProvided()
    {
        InnerHasher
            .Setup(x => x.HashPassword(User, UnpairedPassword))
            .Returns(MadeHash)
            .Verifiable();

        var hash = Hasher.HashPassword(User, UnpairedPassword);

        Assert.Equal(MadeHash, hash);
    }

    #endregion

    #region VerifyHashedPassword Tests

    [Fact]
    public void VerifyHashedPassword_WhenTheComposedFormVerifies_ReturnsItsResult()
    {
        InnerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();

        var result = Hasher.VerifyHashedPassword(User, StoredHash, DecomposedPassword);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenOnlyTheProvidedFormVerifies_ReturnsSuccessRehashNeeded()
    {
        InnerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();
        InnerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, DecomposedPassword))
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();

        var result = Hasher.VerifyHashedPassword(User, StoredHash, DecomposedPassword);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenNeitherFormVerifies_ReturnsFailed()
    {
        InnerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();
        InnerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, DecomposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();

        var result = Hasher.VerifyHashedPassword(User, StoredHash, DecomposedPassword);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenThePasswordIsInNfc_VerifiesItOnce()
    {
        InnerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable(Times.Once());

        var result = Hasher.VerifyHashedPassword(User, StoredHash, ComposedPassword);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    #endregion
}
