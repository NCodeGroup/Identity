#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.PropertyBag;

// TODO: register in DI

/// <summary>
/// Provides a default implementation of the <see cref="IPropertyBagFactory"/> abstraction.
/// </summary>
[PublicAPI]
public class DefaultPropertyBagFactory : IPropertyBagFactory
{
    /// <summary>
    /// Gets a singleton instance for the <see cref="IPropertyBagFactory"/> abstraction.
    /// </summary>
    public static IPropertyBagFactory Singleton { get; set; } = new DefaultPropertyBagFactory();

    /// <inheritdoc />
    public IPropertyBag Create() => new DefaultPropertyBag();
}
