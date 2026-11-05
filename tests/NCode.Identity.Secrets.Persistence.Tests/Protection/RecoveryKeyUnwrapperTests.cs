// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Security.Cryptography;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class RecoveryKeyUnwrapperTests
{
    private static string Wrap(RecoveryKeyWrapper wrapper, byte[] masterKey)
    {
        var writer = new ArrayBufferWriter<char>();
        wrapper.Wrap(masterKey, ref writer);
        return new string(writer.WrittenSpan);
    }

    #region FromPem Tests

    [Fact]
    public void FromPem_TextWithoutPem_ThrowsNamingTheLabel()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            RecoveryKeyUnwrapper.FromPem("not a pem")
        );

        Assert.Contains("PRIVATE KEY", exception.Message);
    }

    [Fact]
    public void FromPem_APublicKey_ThrowsNamingTheLabel()
    {
        using var rsa = RSA.Create(3072);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RecoveryKeyUnwrapper.FromPem(rsa.ExportSubjectPublicKeyInfoPem())
        );

        Assert.Contains("PRIVATE KEY", exception.Message);
    }

    [Fact]
    public void FromPem_CorruptBase64_ThrowsNamingTheLabel()
    {
        using var rsa = RSA.Create(3072);
        var pem = rsa.ExportPkcs8PrivateKeyPem();
        var index = pem.IndexOf('\n') + 5;
        var corrupt = string.Concat(pem.AsSpan(0, index), "*", pem.AsSpan(index + 1));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RecoveryKeyUnwrapper.FromPem(corrupt)
        );

        Assert.Contains("PRIVATE KEY", exception.Message);
    }

    [Fact]
    public void FromPem_APkcs8KeyThatIsNotRsa_ThrowsInvalidOperation()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RecoveryKeyUnwrapper.FromPem(ecdsa.ExportPkcs8PrivateKeyPem())
        );

        Assert.IsAssignableFrom<CryptographicException>(exception.InnerException);
    }

    [Fact]
    public void FromPem_A2048BitKey_ThrowsNaming3072()
    {
        using var rsa = RSA.Create(2048);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            RecoveryKeyUnwrapper.FromPem(rsa.ExportPkcs8PrivateKeyPem())
        );

        Assert.Contains("3072", exception.Message);
        Assert.Contains("2048", exception.Message);
    }

    [Fact]
    public void FromPem_A3072BitKey_HasTheRecoveryKeyIdOfItsPublicKey()
    {
        using var rsa = RSA.Create(3072);
        using var wrapper = RecoveryKeyWrapper.FromPem(rsa.ExportSubjectPublicKeyInfoPem());

        using var unwrapper = RecoveryKeyUnwrapper.FromPem(rsa.ExportPkcs8PrivateKeyPem());

        Assert.Equal(wrapper.RecoveryKeyId, unwrapper.RecoveryKeyId);
    }

    #endregion

    #region Unwrap Tests

    [Fact]
    public void Unwrap_ACopyWrappedUnderThisKey_WritesTheMasterKey()
    {
        using var rsa = RSA.Create(3072);
        using var wrapper = RecoveryKeyWrapper.FromPem(rsa.ExportSubjectPublicKeyInfoPem());
        using var unwrapper = RecoveryKeyUnwrapper.FromPem(rsa.ExportPkcs8PrivateKeyPem());
        var masterKey = RandomNumberGenerator.GetBytes(SecretProtectionFormat.MasterKeySizeBytes);
        var copy = Wrap(wrapper, masterKey);
        var destination = new ArrayBufferWriter<byte>();

        unwrapper.Unwrap(copy, destination);

        Assert.Equal(masterKey, destination.WrittenSpan.ToArray());
    }

    [Fact]
    public void Unwrap_ACopyWrappedUnderAnotherKey_ThrowsCryptographicException()
    {
        using var rsa = RSA.Create(3072);
        using var other = RSA.Create(3072);
        using var wrapper = RecoveryKeyWrapper.FromPem(other.ExportSubjectPublicKeyInfoPem());
        using var unwrapper = RecoveryKeyUnwrapper.FromPem(rsa.ExportPkcs8PrivateKeyPem());
        var copy = Wrap(
            wrapper,
            RandomNumberGenerator.GetBytes(SecretProtectionFormat.MasterKeySizeBytes)
        );

        Assert.ThrowsAny<CryptographicException>(() =>
            unwrapper.Unwrap(copy, new ArrayBufferWriter<byte>())
        );
    }

    [Fact]
    public void Unwrap_NullDestination_ThrowsArgumentNull()
    {
        using var rsa = RSA.Create(3072);
        using var unwrapper = RecoveryKeyUnwrapper.FromPem(rsa.ExportPkcs8PrivateKeyPem());

        Assert.Throws<ArgumentNullException>(() => unwrapper.Unwrap("copy", null!));
    }

    #endregion
}
