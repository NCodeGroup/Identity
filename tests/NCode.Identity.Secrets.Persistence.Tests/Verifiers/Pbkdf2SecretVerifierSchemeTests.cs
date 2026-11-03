// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Microsoft.Extensions.Time.Testing;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Verifiers;

namespace NCode.Identity.Secrets.Persistence.Verifiers;

public sealed class Pbkdf2SecretVerifierSchemeTests : IDisposable
{
    private static readonly KeyMetadata Metadata = new() { KeyId = "secret-1" };

    private static readonly byte[] Secret = "the-client-secret"u8.ToArray();

    private static readonly TimeSpan QueueTimeout = TimeSpan.FromSeconds(2);

    private readonly FakeTimeProvider _timeProvider = new(
        new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
    );

    private readonly Pbkdf2SecretVerifierScheme _scheme;

    public Pbkdf2SecretVerifierSchemeTests()
    {
        _scheme = CreateScheme(new Pbkdf2SecretVerifierOptions { MaxQueueLength = 1 });
    }

    public void Dispose() => _scheme.Dispose();

    // One turn, so a test that holds it saturates the gate.
    private Pbkdf2SecretVerifierScheme CreateScheme(Pbkdf2SecretVerifierOptions options)
    {
        options.Iterations = TestSecretVerifierSchemes.Iterations;
        options.MaxConcurrency = 1;
        options.QueueTimeout = QueueTimeout;
        return new Pbkdf2SecretVerifierScheme(
            Microsoft.Extensions.Options.Options.Create(options),
            _timeProvider
        );
    }

    private static SecretVerifier CreateVerifier(Pbkdf2SecretVerifierScheme scheme)
    {
        var writer = new ArrayBufferWriter<byte>();
        scheme.WriteParameters(Secret, writer);
        Assert.True(scheme.TryReadParameters(Metadata, writer.WrittenSpan, out var verifier));
        return verifier;
    }

    // Takes the only turn, as a verification that is running holds it.
    private async Task HoldTheTurnAsync() =>
        Assert.True(await _scheme.Gate.TryEnterAsync(CancellationToken.None));

    #region Constructor Tests

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenTheConcurrencyIsBelowOne_ThrowsNamingTheOption(int maxConcurrency)
    {
        var options = new Pbkdf2SecretVerifierOptions { MaxConcurrency = maxConcurrency };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new Pbkdf2SecretVerifierScheme(
                Microsoft.Extensions.Options.Options.Create(options),
                _timeProvider
            )
        );

        Assert.Equal(
            $"'Pbkdf2SecretVerifierOptions.MaxConcurrency' is {maxConcurrency}; it must be at least 1.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_WhenTheQueueLengthIsBelowZero_ThrowsNamingTheOption()
    {
        var options = new Pbkdf2SecretVerifierOptions { MaxQueueLength = -1 };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new Pbkdf2SecretVerifierScheme(
                Microsoft.Extensions.Options.Options.Create(options),
                _timeProvider
            )
        );

        Assert.Equal(
            "'Pbkdf2SecretVerifierOptions.MaxQueueLength' is -1; it must be at least 0.",
            exception.Message
        );
    }

    [Fact]
    public void Constructor_WhenTheQueueTimeoutIsZero_ThrowsNamingTheOption()
    {
        var options = new Pbkdf2SecretVerifierOptions { QueueTimeout = TimeSpan.Zero };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new Pbkdf2SecretVerifierScheme(
                Microsoft.Extensions.Options.Options.Create(options),
                _timeProvider
            )
        );

        Assert.StartsWith(
            "'Pbkdf2SecretVerifierOptions.QueueTimeout' is 00:00:00;",
            exception.Message,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region VerifyAsync Tests

    [Fact]
    public async Task VerifyAsync_WhenItFinishes_ReleasesItsTurn()
    {
        var verifier = CreateVerifier(_scheme);

        Assert.Equal(
            SecretVerificationResult.Success,
            await verifier.VerifyAsync(Secret, CancellationToken.None)
        );
        Assert.Equal(
            SecretVerificationResult.Failed,
            await verifier.VerifyAsync(Secret.AsMemory(..^1), CancellationToken.None)
        );

        Assert.Equal(1, _scheme.Gate.FreeTurnCount);
        Assert.True(verifier.HasWorkFactor);
    }

    [Fact]
    public async Task VerifyAsync_WhenEveryTurnIsTaken_WaitsUntilOneIsReleased()
    {
        var verifier = CreateVerifier(_scheme);
        await HoldTheTurnAsync();

        var verification = verifier.VerifyAsync(Secret, CancellationToken.None).AsTask();
        Assert.False(verification.IsCompleted);
        Assert.Equal(1, _scheme.Gate.WaiterCount);

        _scheme.Gate.Release();

        Assert.Equal(SecretVerificationResult.Success, await verification);
        Assert.Equal(1, _scheme.Gate.FreeTurnCount);
        Assert.Equal(0, _scheme.Gate.WaiterCount);
    }

    [Fact]
    public async Task VerifyAsync_WhenTheQueueIsFull_IsRefusedWithoutWaiting()
    {
        var verifier = CreateVerifier(_scheme);
        await HoldTheTurnAsync();
        var queued = verifier.VerifyAsync(Secret, CancellationToken.None).AsTask();
        Assert.False(queued.IsCompleted);

        // The gate is saturated: its one turn is taken and its one queue place is filled.
        var refused = verifier.VerifyAsync(Secret, CancellationToken.None).AsTask();

        Assert.True(refused.IsCompleted);
        Assert.Equal(SecretVerificationResult.Unavailable, await refused);
        Assert.Equal(1, _scheme.Gate.WaiterCount);

        _scheme.Gate.Release();
        Assert.Equal(SecretVerificationResult.Success, await queued);
    }

    [Fact]
    public async Task VerifyAsync_WhenNoTurnComesWithinTheQueueTimeout_IsRefused()
    {
        var verifier = CreateVerifier(_scheme);
        await HoldTheTurnAsync();
        var queued = verifier.VerifyAsync(Secret, CancellationToken.None).AsTask();
        Assert.False(queued.IsCompleted);

        _timeProvider.Advance(QueueTimeout);

        Assert.Equal(SecretVerificationResult.Unavailable, await queued);
        Assert.Equal(0, _scheme.Gate.WaiterCount);
        Assert.Equal(0, _scheme.Gate.FreeTurnCount);
        _scheme.Gate.Release();
    }

    [Fact]
    public async Task VerifyAsync_WhenThereIsNoQueue_IsRefusedWhileEveryTurnIsTaken()
    {
        using var scheme = CreateScheme(new Pbkdf2SecretVerifierOptions { MaxQueueLength = 0 });
        var verifier = CreateVerifier(scheme);
        Assert.True(await scheme.Gate.TryEnterAsync(CancellationToken.None));

        Assert.Equal(
            SecretVerificationResult.Unavailable,
            await verifier.VerifyAsync(Secret, CancellationToken.None)
        );

        scheme.Gate.Release();
        Assert.Equal(
            SecretVerificationResult.Success,
            await verifier.VerifyAsync(Secret, CancellationToken.None)
        );
    }

    [Fact]
    public async Task VerifyAsync_WhenCancelledWhileItWaits_ThrowsWithoutTakingATurn()
    {
        var verifier = CreateVerifier(_scheme);
        await HoldTheTurnAsync();
        using var cancellation = new CancellationTokenSource();

        var verification = verifier.VerifyAsync(Secret, cancellation.Token).AsTask();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => verification);
        Assert.Equal(0, _scheme.Gate.WaiterCount);
        _scheme.Gate.Release();
        Assert.Equal(1, _scheme.Gate.FreeTurnCount);
    }

    [Fact]
    public async Task VerifyAsync_WhenAnotherVerifierOfTheSchemeHoldsTheTurn_Waits()
    {
        // Every verifier the scheme reads shares its gate, so the bound is the process's, not the verifier's.
        var holder = CreateVerifier(_scheme);
        var waiter = CreateVerifier(_scheme);
        await HoldTheTurnAsync();

        var verification = waiter.VerifyAsync(Secret, CancellationToken.None).AsTask();
        Assert.False(verification.IsCompleted);

        _scheme.Gate.Release();
        Assert.Equal(SecretVerificationResult.Success, await verification);
        Assert.Equal(
            SecretVerificationResult.Success,
            await holder.VerifyAsync(Secret, CancellationToken.None)
        );
    }

    #endregion
}
