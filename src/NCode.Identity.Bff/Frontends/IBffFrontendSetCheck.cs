// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Bff.Frontends;

/// <summary>
/// Checks a candidate set of frontends against what the host registered or mapped; the catalog refuses a set for which
/// any check adds a failure.
/// </summary>
// See ADR-1442.
internal interface IBffFrontendSetCheck
{
    /// <summary>
    /// Checks a candidate set of frontends and adds one message per failure.
    /// </summary>
    /// <param name="frontends">The candidate set.</param>
    /// <param name="failures">The collection that receives one message per failure.</param>
    void Check(BffFrontendSet frontends, ICollection<string> failures);
}
