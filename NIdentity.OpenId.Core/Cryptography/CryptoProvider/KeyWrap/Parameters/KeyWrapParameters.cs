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

namespace NIdentity.OpenId.Cryptography.CryptoProvider.KeyWrap.Parameters;

/// <summary>
/// Represents the parameters for all cryptographic key unwrap operations.
/// Derived classes will contain the actual algorithm specific arguments.
/// </summary>
public record KeyWrapParameters;

// TODO

/// <summary>
/// Represents the parameters for all cryptographic key wrap operations that return a new content encryption key (CEK).
/// Derived classes will contain the actual algorithm specific arguments.
/// </summary>
public abstract record WrapNewKeyParameters;

/// <summary>
/// Represents the parameters for all cryptographic key wrap operations that use an existing content encryption key (CEK).
/// Derived classes will contain the actual algorithm specific arguments.
/// </summary>
public abstract record WrapKeyParameters;
