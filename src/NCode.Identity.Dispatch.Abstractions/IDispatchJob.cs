// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
