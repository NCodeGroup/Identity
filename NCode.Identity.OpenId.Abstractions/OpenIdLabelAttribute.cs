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

namespace NCode.Identity.OpenId;

/// <summary>
/// Allows to specify a <see cref="string"/> label for a field.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class OpenIdLabelAttribute : Attribute
{
    /// <summary>
    /// Gets the <see cref="string"/> label for the field.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdLabelAttribute"/> class.
    /// </summary>
    /// <param name="label">The <see cref="string"/> label for the field.</param>
    public OpenIdLabelAttribute(string label)
    {
        Label = label;
    }
}
