﻿#region Copyright Preamble

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

using System.Runtime.ExceptionServices;
using JetBrains.Annotations;

namespace NCode.Collections.Providers.PeriodicPolling;

/// <summary>
/// Represents a method that is called to handle exceptions when they occur during the refresh of a collection.
/// </summary>
[PublicAPI]
public delegate ValueTask HandleExceptionAsyncDelegate(
    ExceptionDispatchInfo exception,
    CancellationToken cancellationToken);
