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

namespace NCode.Jose.SecretKeys;

/// <summary>
/// Provides a <see cref="SecretKey"/> implementation that is empty.
/// </summary>
public sealed class EmptySecretKey : SecretKey
{
    /// <summary>
    /// Gets a singleton instance of <see cref="EmptySecretKey"/>.
    /// </summary>
    public static EmptySecretKey Singleton { get; } = new();

    private EmptySecretKey()
        : base(new KeyMetadata(string.Empty, null, null, null))
    {
        // nothing
    }

    /// <inheritdoc />
    public override int KeySizeBits => 0;

    /// <inheritdoc />
    public override int KeySizeBytes => 0;

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        // nothing
    }
}
