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

using NCode.Cryptography.Keys;

namespace NCode.Identity.Jwt;

public class ValidateJwtContext
{
    /// <summary>
    /// Gets the <see cref="SecretKey"/> that was used to decode the JWT.
    /// </summary>
    public SecretKey SecretKey { get; }

    /// <summary>
    /// Gets an <see cref="DecodedJwt"/> that contains the decoded JWT header and payload.
    /// </summary>
    public DecodedJwt DecodedJwt { get; }

    /// <summary>
    /// Gets a <see cref="PropertyBag"/> that can be used to store custom state information.
    /// </summary>
    public PropertyBag PropertyBag { get; }

    public ValidateJwtContext(SecretKey secretKey, DecodedJwt decodedJwt, PropertyBag propertyBag)
    {
        SecretKey = secretKey;
        DecodedJwt = decodedJwt;
        PropertyBag = propertyBag;
    }
}
