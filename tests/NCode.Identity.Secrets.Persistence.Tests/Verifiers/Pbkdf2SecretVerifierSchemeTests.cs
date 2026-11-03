// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
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

    private readonly DefaultSecretVerificationGate _gate;

    private readonly Pbkdf2SecretVerifierScheme _scheme;

    public Pbkdf2SecretVerifierSchemeTests()
    {
        _gate = CreateGate(maxQueueLength: 1);
        _scheme = TestSecretVerifierSchemes.CreatePbkdf2Scheme(gate: _gate);
    }

    public void Dispose() => _gate.Dispose();

    // One turn, so a test that holds it saturates the gate.
    private DefaultSecretVerificationGate CreateGate(int maxQueueLength) =>
        TestSecretVerifierSchemes.CreateGate(
            new SecretVerificationGateOptions
            {
                MaxConcurrency = 1,
                MaxQueueLength = maxQueueLength,
                QueueTimeout = QueueTimeout,
            },
            _timeProvider
        );

    private static SecretVerifier CreateVerifier(Pbkdf2SecretVerifierScheme scheme)
    {
        var writer = new ArrayBufferWriter<byte>();
        scheme.WriteParameters(Secret, writer);
        Assert.True(scheme.TryReadParameters(Metadata, writer.WrittenSpan, out var verifier));
        return verifier;
    }

    // Takes the only turn, as a running verification of any scheme holds it.
    private async Task HoldTheTurnAsync() =>
        Assert.True(await _gate.TryEnterAsync(CancellationToken.None));

    #region Constructor Tests

    [Fact]
    public void Constructor_ReadsNoOptions()
    {
        // The options that name a scheme are validated against the catalog, which constructs every scheme, so a scheme
        // reads its own options only when it makes or reads a verifier.
        var optionsAccessor = new Mock<IOptions<Pbkdf2SecretVerifierOptions>>(MockBehavior.Strict);

        var scheme = new Pbkdf2SecretVerifierScheme(optionsAccessor.Object, _gate);

        Assert.Equal(SecretVerifierSchemes.Pbkdf2Sha256, scheme.Name);
        optionsAccessor.VerifyNoOtherCalls();
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

        Assert.Equal(1, _gate.FreeTurnCount);
        Assert.True(verifier.HasWorkFactor);
    }

    [Fact]
    public async Task VerifyAsync_WhenEveryTurnIsTaken_WaitsUntilOneIsReleased()
    {
        var verifier = CreateVerifier(_scheme);
        await HoldTheTurnAsync();

        var verification = verifier.VerifyAsync(Secret, CancellationToken.None).AsTask();
        Assert.False(verification.IsCompleted);
        Assert.Equal(1, _gate.WaiterCount);

        _gate.Release();

        Assert.Equal(SecretVerificationResult.Success, await verification);
        Assert.Equal(1, _gate.FreeTurnCount);
        Assert.Equal(0, _gate.WaiterCount);
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
        Assert.Equal(1, _gate.WaiterCount);

        _gate.Release();
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
        Assert.Equal(0, _gate.WaiterCount);
        Assert.Equal(0, _gate.FreeTurnCount);
        _gate.Release();
    }

    [Fact]
    public async Task VerifyAsync_WhenThereIsNoQueue_IsRefusedWhileEveryTurnIsTaken()
    {
        using var gate = CreateGate(maxQueueLength: 0);
        var scheme = TestSecretVerifierSchemes.CreatePbkdf2Scheme(gate: gate);
        var verifier = CreateVerifier(scheme);
        Assert.True(await gate.TryEnterAsync(CancellationToken.None));

        Assert.Equal(
            SecretVerificationResult.Unavailable,
            await verifier.VerifyAsync(Secret, CancellationToken.None)
        );

        gate.Release();
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
        Assert.Equal(0, _gate.WaiterCount);
        _gate.Release();
        Assert.Equal(1, _gate.FreeTurnCount);
    }

    [Fact]
    public async Task VerifyAsync_WhenAnotherVerifierHoldsTheTurn_Waits()
    {
        // Every verifier with a work factor shares the process's gate, so the bound is the process's, not the
        // verifier's or the scheme's.
        var holder = CreateVerifier(_scheme);
        var waiter = CreateVerifier(_scheme);
        await HoldTheTurnAsync();

        var verification = waiter.VerifyAsync(Secret, CancellationToken.None).AsTask();
        Assert.False(verification.IsCompleted);

        _gate.Release();
        Assert.Equal(SecretVerificationResult.Success, await verification);
        Assert.Equal(
            SecretVerificationResult.Success,
            await holder.VerifyAsync(Secret, CancellationToken.None)
        );
    }

    #endregion
}
