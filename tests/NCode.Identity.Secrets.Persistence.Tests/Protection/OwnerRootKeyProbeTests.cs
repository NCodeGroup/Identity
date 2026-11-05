// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Moq;
using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class OwnerRootKeyProbeTests
{
    private const string Reference = "kms://tenant-a/root";

    private static readonly SecretOwner Owner = SecretOwner.ForTenant("tenant-a");

    [Fact]
    public async Task Probe_WorkingKey_ReturnsNullAndWrapsAndUnwrapsOnce()
    {
        var wrapper = new FakeReferencedKeyWrapper();

        var failure = await OwnerRootKeyProbe.ProbeAsync(
            wrapper,
            Reference,
            Owner,
            CancellationToken.None
        );

        Assert.Null(failure);
        Assert.Equal(1, wrapper.WrapCount);
        Assert.Equal(1, wrapper.UnwrapCount);
    }

    [Fact]
    public async Task Probe_RevokedKey_ReturnsTheFailure()
    {
        var wrapper = new FakeReferencedKeyWrapper();
        wrapper.Revoke(Reference);

        var failure = await OwnerRootKeyProbe.ProbeAsync(
            wrapper,
            Reference,
            Owner,
            CancellationToken.None
        );

        Assert.Equal("InvalidOperationException: revoked", failure);
    }

    [Fact]
    public async Task Probe_KeyThatCannotUnwrap_ReturnsTheFailure()
    {
        var wrapper = new FakeReferencedKeyWrapper();
        wrapper.RevokeUnwrapOnly(Reference);

        var failure = await OwnerRootKeyProbe.ProbeAsync(
            wrapper,
            Reference,
            Owner,
            CancellationToken.None
        );

        Assert.Equal("InvalidOperationException: revoked", failure);
        Assert.Equal(1, wrapper.WrapCount);
    }

    [Fact]
    public async Task Probe_WrapperThatReturnsAnotherValue_ReportsAMismatch()
    {
        var mock = new Mock<IReferencedKeyWrapper>(MockBehavior.Strict);
        mock.Setup(x =>
                x.WrapAsync(
                    Reference,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new WrappedKey
                {
                    WrapperName = "mock",
                    KeyId = "id",
                    Ciphertext = "AAAA",
                }
            )
            .Verifiable();
        mock.Setup(x =>
                x.UnwrapAsync(
                    It.IsAny<WrappedKey>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<IBufferWriter<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<WrappedKey, ReadOnlyMemory<byte>, IBufferWriter<byte>, CancellationToken>(
                (_, _, destination, _) =>
                {
                    destination.GetSpan(32)[..32].Clear();
                    destination.Advance(32);
                }
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var failure = await OwnerRootKeyProbe.ProbeAsync(
            mock.Object,
            Reference,
            Owner,
            CancellationToken.None
        );

        Assert.Equal("the key unwrapped a value other than the one it wrapped", failure);
        mock.Verify();
    }

    [Fact]
    public async Task Probe_LongFailureMessage_IsTruncated()
    {
        var mock = new Mock<IReferencedKeyWrapper>(MockBehavior.Strict);
        mock.Setup(x =>
                x.WrapAsync(
                    Reference,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException(new string('x', 5000)))
            .Verifiable();

        var failure = await OwnerRootKeyProbe.ProbeAsync(
            mock.Object,
            Reference,
            Owner,
            CancellationToken.None
        );

        Assert.Equal(1024, failure?.Length);
        Assert.StartsWith("InvalidOperationException: xxx", failure);
    }

    [Fact]
    public async Task Probe_CancelledToken_Propagates()
    {
        var mock = new Mock<IReferencedKeyWrapper>(MockBehavior.Strict);
        using var source = new CancellationTokenSource();
        mock.Setup(x =>
                x.WrapAsync(
                    Reference,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                async (
                    string _,
                    ReadOnlyMemory<byte> _,
                    ReadOnlyMemory<byte> _,
                    CancellationToken token
                ) =>
                {
                    await source.CancelAsync();
                    token.ThrowIfCancellationRequested();
                    return new WrappedKey
                    {
                        WrapperName = "mock",
                        KeyId = "id",
                        Ciphertext = "AAAA",
                    };
                }
            )
            .Verifiable();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await OwnerRootKeyProbe.ProbeAsync(mock.Object, Reference, Owner, source.Token)
        );
    }

    [Fact]
    public async Task Probe_WrapsUnderTheProbeContextLabel()
    {
        var mock = new Mock<IReferencedKeyWrapper>(MockBehavior.Strict);
        byte[]? context = null;
        mock.Setup(x =>
                x.WrapAsync(
                    Reference,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, ReadOnlyMemory<byte>, ReadOnlyMemory<byte>, CancellationToken>(
                (_, _, wrapContext, _) => context = wrapContext.ToArray()
            )
            .ThrowsAsync(new InvalidOperationException("stop"))
            .Verifiable();

        await OwnerRootKeyProbe.ProbeAsync(mock.Object, Reference, Owner, CancellationToken.None);

        Assert.NotNull(context);
        Assert.True(
            context.AsSpan().StartsWith("ncode-identity/secret-protection/v1/root-probe"u8)
        );
    }
}
