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

namespace NCode.Identity.Dispatch;

/// <summary>
/// Represents the payload of a unit of background work handed to the <see cref="IJobDispatcher"/> for out-of-band
/// execution by an <see cref="IDispatchJobHandler{TJob}"/>. A job is a plain data contract with no behaviour and
/// should be serializable so that durable and distributed dispatcher adapters (an outbox, a broker, a scheduling
/// cluster) can persist, transport, and re-materialize it; the in-process adapter holds it by reference.
/// </summary>
[PublicAPI]
public interface IDispatchJob;
