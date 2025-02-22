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

namespace NCode.PropertyBag;

/// <summary>
/// Provides a default implementation of the <see cref="IPropertyBagScope"/> abstraction.
/// </summary>
public class DefaultPropertyBagScope(IPropertyBag propertyBag, PropertyBagKey key) : IPropertyBagScope
{
    /// <inheritdoc />
    public IPropertyBag PropertyBag { get; } = propertyBag;

    /// <inheritdoc />
    public PropertyBagKey Key { get; } = key;

    /// <inheritdoc />
    public void Dispose()
    {
        PropertyBag.Remove(Key);
        GC.SuppressFinalize(this);
    }
}
