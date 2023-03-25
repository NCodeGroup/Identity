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

using Microsoft.IdentityModel.Tokens;

namespace NIdentity.OpenId.Cryptography.archive;

internal class NoneKeyWrapProvider : KeyWrapProvider
{
    public override string? Context { get; set; }
    public override string Algorithm { get; }
    public override SecurityKey Key { get; }

    public NoneKeyWrapProvider(string algorithm, SecurityKey key)
    {
        Algorithm = algorithm;
        Key = key;
    }

    protected override void Dispose(bool disposing)
    {
        // nothing
    }

    public override byte[] UnwrapKey(byte[] keyBytes)
    {
        return keyBytes;
    }

    public override byte[] WrapKey(byte[] keyBytes)
    {
        return keyBytes;
    }
}
