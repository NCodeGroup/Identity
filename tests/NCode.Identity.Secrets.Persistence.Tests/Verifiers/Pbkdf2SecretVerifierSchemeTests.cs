// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using NCode.Buffers;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

public sealed class Pbkdf2SecretVerifierSchemeTests : IDisposable
{
    private static readonly KeyMetadata Metadata = new() { KeyId = "secret-1" };

    private static readonly byte[] Secret = SecureEncoding.UTF8.GetBytes("the-client-secret");

    private readonly Pbkdf2SecretVerifierScheme _scheme = CreateScheme(maxConcurrency: 1);

    public void Dispose() => _scheme.Dispose();

    private static Pbkdf2SecretVerifierScheme CreateScheme(int maxConcurrency) =>
        new(
            Microsoft.Extensions.Options.Options.Create(
                new Pbkdf2SecretVerifierOptions
                {
                    Iterations = TestSecretVerifierSchemes.Iterations,
                    MaxConcurrency = maxConcurrency,
                }
            )
        );

    private SecretVerifier CreateVerifier()
    {
        var writer = new ArrayBufferWriter<byte>();
        _scheme.WriteParameters(Secret, writer);
        Assert.True(_scheme.TryReadParameters(Metadata, writer.WrittenSpan, out var verifier));
        return verifier;
    }

    #region Constructor Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenTheConcurrencyIsBelowOne_Throws(int maxConcurrency)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateScheme(maxConcurrency));
    }

    #endregion

    #region VerifyAsync Tests

    [Fact]
    public async Task VerifyAsync_ReleasesItsTurnWhenItFinishes()
    {
        var verifier = CreateVerifier();

        Assert.True(await verifier.VerifyAsync(Secret, CancellationToken.None));
        Assert.False(await verifier.VerifyAsync(Secret.AsMemory(..^1), CancellationToken.None));

        Assert.Equal(1, _scheme.Gate.CurrentCount);
    }

    [Fact]
    public async Task VerifyAsync_WhenEveryTurnIsTaken_WaitsUntilOneIsReleased()
    {
        var verifier = CreateVerifier();
        var gate = _scheme.Gate;
        await gate.WaitAsync(CancellationToken.None);

        var verification = verifier.VerifyAsync(Secret, CancellationToken.None).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.False(verification.IsCompleted);

        gate.Release();
        Assert.True(await verification);
        Assert.Equal(1, gate.CurrentCount);
    }

    [Fact]
    public async Task VerifyAsync_WhenCancelledWhileItWaits_ThrowsWithoutTakingATurn()
    {
        var verifier = CreateVerifier();
        var gate = _scheme.Gate;
        await gate.WaitAsync(CancellationToken.None);
        using var cancellation = new CancellationTokenSource();

        var verification = verifier.VerifyAsync(Secret, cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verification);
        gate.Release();
        Assert.Equal(1, gate.CurrentCount);
    }

    [Fact]
    public async Task VerifyAsync_WhenAnotherVerifierOfTheSchemeHoldsTheTurn_Waits()
    {
        // Every verifier the scheme reads shares its gate, so the bound is the process's, not the verifier's.
        var holder = CreateVerifier();
        var waiter = CreateVerifier();
        await _scheme.Gate.WaitAsync(CancellationToken.None);

        var verification = waiter.VerifyAsync(Secret, CancellationToken.None).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(50), CancellationToken.None);
        Assert.False(verification.IsCompleted);

        _scheme.Gate.Release();
        Assert.True(await verification);
        Assert.True(await holder.VerifyAsync(Secret, CancellationToken.None));
    }

    #endregion
}
