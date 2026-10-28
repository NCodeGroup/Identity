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

using NCode.Identity.Secrets.KeyWrapping;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the ability to wrap a master key under the offline recovery public key, so the stored secrets can still
/// be read after the root key is lost. Only the public half of the recovery key is ever deployed; the private half
/// is kept offline.
/// </summary>
// See ADR-0235.
internal interface IRecoveryKeyWrapper : IKeyWrapper
{
    /// <summary>
    /// Gets a value indicating whether a recovery public key is configured. When it is not,
    /// <see cref="IKeyWrapper.WrapAsync"/> throws an <see cref="InvalidOperationException"/>.
    /// </summary>
    bool IsConfigured { get; }
}
