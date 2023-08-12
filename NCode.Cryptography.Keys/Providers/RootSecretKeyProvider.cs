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

namespace NCode.Cryptography.Keys.Providers;

/// <summary>
/// Provides the root (i.e. top-level) collection of <see cref="SecretKey"/> instances by aggregating multiple <see cref="ISecretKeyProvider"/> instances.
/// </summary>
public interface IRootSecretKeyProvider : ISecretKeyProvider
{
    // nothing
}

/// <summary>
/// Provides the default implementation of <see cref="IRootSecretKeyProvider"/>.
/// </summary>
public class RootSecretKeyProvider : CompositeSecretKeyProvider, IRootSecretKeyProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RootSecretKeyProvider"/> class with the specified collection of <see cref="ISecretKeyProvider"/> instances.
    /// </summary>
    /// <param name="providers">A collection of <see cref="ISecretKeyProvider"/> instances to aggregate.</param>
    public RootSecretKeyProvider(IEnumerable<ISecretKeyProvider> providers)
        : base(providers)
    {
        // nothing
    }
}
