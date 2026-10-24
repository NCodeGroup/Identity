#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Moq;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;

namespace NCode.Identity.OpenId.Core.Tests.Accounts;

// Makes a strict store mock ask the gate about the stored user name, as a store does after its lookup.
internal static class LocalAccountStoreSetup
{
    public static void SetupVerification(
        Mock<ILocalAccountStore> mockStore,
        string submittedUserName,
        string storedUserName,
        PersistedLocalAccount? verified
    ) =>
        mockStore
            .Setup(x =>
                x.VerifyCredentialAsync(
                    submittedUserName,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<Func<string, CancellationToken, ValueTask<bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    ReadOnlyMemory<byte> _,
                    Func<string, CancellationToken, ValueTask<bool>> isRefusedAsync,
                    CancellationToken cancellationToken
                ) => VerifyAsync(isRefusedAsync, storedUserName, verified, cancellationToken)
            )
            .Verifiable();

    // Makes a strict store mock ask the gate and then verify the password whatever the gate answered, as a store that
    // ignores a refusal does.
    public static void SetupVerificationIgnoringARefusal(
        Mock<ILocalAccountStore> mockStore,
        string submittedUserName,
        string storedUserName,
        PersistedLocalAccount verified
    ) =>
        mockStore
            .Setup(x =>
                x.VerifyCredentialAsync(
                    submittedUserName,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<Func<string, CancellationToken, ValueTask<bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (
                    string _,
                    ReadOnlyMemory<byte> _,
                    Func<string, CancellationToken, ValueTask<bool>> isRefusedAsync,
                    CancellationToken cancellationToken
                ) =>
                    VerifyIgnoringARefusalAsync(
                        isRefusedAsync,
                        storedUserName,
                        verified,
                        cancellationToken
                    )
            )
            .Verifiable();

    // Makes a strict store mock verify the password without ever asking the gate, as a store that skips the limit does.
    public static void SetupVerificationWithoutTheGate(
        Mock<ILocalAccountStore> mockStore,
        string submittedUserName,
        string storedUserName,
        PersistedLocalAccount verified
    ) =>
        mockStore
            .Setup(x =>
                x.VerifyCredentialAsync(
                    submittedUserName,
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<Func<string, CancellationToken, ValueTask<bool>>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(
                new LocalAccountVerificationResult { UserName = storedUserName, Account = verified }
            )
            .Verifiable();

    private static async ValueTask<LocalAccountVerificationResult> VerifyAsync(
        Func<string, CancellationToken, ValueTask<bool>> isRefusedAsync,
        string storedUserName,
        PersistedLocalAccount? verified,
        CancellationToken cancellationToken
    )
    {
        var isRefused = await isRefusedAsync(storedUserName, cancellationToken);
        return new LocalAccountVerificationResult
        {
            UserName = storedUserName,
            Account = isRefused ? null : verified,
            IsRefused = isRefused,
        };
    }

    private static async ValueTask<LocalAccountVerificationResult> VerifyIgnoringARefusalAsync(
        Func<string, CancellationToken, ValueTask<bool>> isRefusedAsync,
        string storedUserName,
        PersistedLocalAccount verified,
        CancellationToken cancellationToken
    )
    {
        await isRefusedAsync(storedUserName, cancellationToken);
        return new LocalAccountVerificationResult { UserName = storedUserName, Account = verified };
    }
}
