#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using NCode.Registration;

namespace NCode.Persistence;

/// <summary>
/// A registration marker announcing that a persistence store-manager framework (an <c>IStoreManager</c> provider, such
/// as the Entity Framework one) has been registered. A store slice — which registers a store factory the store manager
/// resolves — requires this builder as its receiver, so a slice cannot be composed without the framework it depends on.
/// </summary>
[PublicAPI]
public readonly struct StoreManagerLibrary : IRegistrationMarker<StoreManagerLibrary>;
