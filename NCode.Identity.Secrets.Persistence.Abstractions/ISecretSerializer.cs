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

using NCode.Identity.Secrets.Persistence.DataContracts;

namespace NCode.Identity.Secrets.Persistence;

/// <summary>
/// Provides the ability to serialize and deserialize secrets from persisted storage.
/// </summary>
public interface ISecretSerializer
{
    /// <summary>
    /// Deserializes a collection of <see cref="PersistedSecret"/> instances by loading and converting them into a collection of
    /// <see cref="SecretKey"/> instances.
    /// The resulting collection is sorted descending by the <see cref="KeyMetadata.ExpiresWhen"/> property.
    /// </summary>
    /// <param name="persistedSecrets">The <see cref="PersistedSecret"/> instances to deserialize into <see cref="SecretKey"/> instances.</param>
    /// <param name="requiresMigration"><c>true</c> if at least one <see cref="PersistedSecret"/>
    /// should be reprotected before being persisted back to long-term storage,
    /// <c>false</c> otherwise. Migration might be requested when the default
    /// protection key has changed, for instance.</param>
    /// <returns>The collection of <see cref="SecretKey"/> instances.</returns>
    IReadOnlyCollection<SecretKey> DeserializeSecrets(IEnumerable<PersistedSecret> persistedSecrets, out bool requiresMigration);

    /// <summary>
    /// Deserializes a <see cref="PersistedSecret"/> instance by loading and converting it into a disposable <see cref="SecretKey"/> instance.
    /// </summary>
    /// <param name="persistedSecret">The <see cref="PersistedSecret"/> instance to deserialize into an <see cref="SecretKey"/> instance.</param>
    /// <param name="requiresMigration"><c>true</c> if the <see cref="PersistedSecret"/>
    /// should be reprotected before being persisted back to long-term storage,
    /// <c>false</c> otherwise. Migration might be requested when the default
    /// protection key has changed, for instance.</param>
    /// <returns>The deserialized <see cref="SecretKey"/> instance.</returns>
    SecretKey DeserializeSecret(PersistedSecret persistedSecret, out bool requiresMigration);
}
