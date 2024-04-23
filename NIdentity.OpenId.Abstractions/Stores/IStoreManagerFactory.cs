#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

namespace NIdentity.OpenId.Stores;

/// <summary>
/// Provides an abstraction for a factory that creates <see cref="IStoreManager"/> instances.
/// </summary>
public interface IStoreManagerFactory
{
    /// <summary>
    /// Creates a new instance of the <see cref="IStoreManager"/> instance.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the
    /// asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// newly created <see cref="IStoreManager"/> instance.</returns>
    ValueTask<IStoreManager> CreateAsync(CancellationToken cancellationToken);
}
