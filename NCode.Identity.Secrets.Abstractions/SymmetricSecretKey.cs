#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using JetBrains.Annotations;

namespace NCode.Identity.Secrets;

/// <summary>
/// Represents an <see cref="SecretKey"/> implementation using <c>symmetric</c> cryptographic keys.
/// </summary>
[PublicAPI]
public abstract class SymmetricSecretKey : SecretKey
{
    /// <inheritdoc />
    public override string KeyType => SecretKeyTypes.Symmetric;

    /// <summary>
    /// Exports the private key to a <see cref="byte"/> buffer.
    /// </summary>
    public abstract bool TryExportPrivateKey(Span<byte> buffer, out int bytesWritten);
}
