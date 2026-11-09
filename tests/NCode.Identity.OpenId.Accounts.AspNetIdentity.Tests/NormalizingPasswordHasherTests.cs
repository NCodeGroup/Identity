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

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<IPasswordHasher<IdentityUser>> _innerHasher;
    private readonly NormalizingPasswordHasher<IdentityUser> _hasher;

    public NormalizingPasswordHasherTests()
    {
        _innerHasher = _mocks.Create<IPasswordHasher<IdentityUser>>();
        _hasher = new NormalizingPasswordHasher<IdentityUser>(_innerHasher.Object);
    }

    public void Dispose() => _mocks.Verify();

    #region HashPassword Tests

    [Fact]
    public void HashPassword_WhenThePasswordIsDecomposed_HashesTheComposedForm()
    {
        _innerHasher
            .Setup(x => x.HashPassword(User, ComposedPassword))
            .Returns(MadeHash)
            .Verifiable();

        var hash = _hasher.HashPassword(User, DecomposedPassword);

        Assert.Equal(MadeHash, hash);
    }

    [Fact]
    public void HashPassword_WhenThePasswordHasAnUnpairedSurrogate_HashesItAsProvided()
    {
        _innerHasher
            .Setup(x => x.HashPassword(User, UnpairedPassword))
            .Returns(MadeHash)
            .Verifiable();

        var hash = _hasher.HashPassword(User, UnpairedPassword);

        Assert.Equal(MadeHash, hash);
    }

    #endregion

    #region VerifyHashedPassword Tests

    [Fact]
    public void VerifyHashedPassword_WhenTheComposedFormVerifies_ReturnsItsResult()
    {
        _innerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();

        var result = _hasher.VerifyHashedPassword(User, StoredHash, DecomposedPassword);

        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenOnlyTheProvidedFormVerifies_ReturnsSuccessRehashNeeded()
    {
        _innerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();
        _innerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, DecomposedPassword))
            .Returns(PasswordVerificationResult.Success)
            .Verifiable();

        var result = _hasher.VerifyHashedPassword(User, StoredHash, DecomposedPassword);

        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenNeitherFormVerifies_ReturnsFailed()
    {
        _innerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();
        _innerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, DecomposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable();

        var result = _hasher.VerifyHashedPassword(User, StoredHash, DecomposedPassword);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    [Fact]
    public void VerifyHashedPassword_WhenThePasswordIsInNfc_VerifiesItOnce()
    {
        _innerHasher
            .Setup(x => x.VerifyHashedPassword(User, StoredHash, ComposedPassword))
            .Returns(PasswordVerificationResult.Failed)
            .Verifiable(Times.Once());

        var result = _hasher.VerifyHashedPassword(User, StoredHash, ComposedPassword);

        Assert.Equal(PasswordVerificationResult.Failed, result);
    }

    #endregion
}
