// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;

namespace NCode.Identity.Bff.RemoteApis;

/// <summary>
/// Keeps a remote API mapped in code, which serves every frontend, clear of each frontend's endpoints and
/// configured remote APIs.
/// </summary>
/// <param name="key">The name that failure messages use for the remote API.</param>
/// <param name="path">The local path prefix of the remote API.</param>
// See ADR-1442.
internal sealed class BffRemoteApiPathCheck(string key, string path) : IBffFrontendSetCheck
{
    /// <inheritdoc />
    public void Check(BffFrontendSet frontends, ICollection<string> failures)
    {
        ArgumentNullException.ThrowIfNull(frontends);
        ArgumentNullException.ThrowIfNull(failures);

        foreach (var frontend in frontends.Frontends)
        {
            var prefix = frontend.Options.PathPrefix;
            if (!string.IsNullOrEmpty(prefix) && BffRemoteApiValidation.Overlaps(path, prefix))
            {
                failures.Add(
                    $"{key}:Path '{path}' must not overlap the endpoints of frontend '{frontend.Name}' at '{prefix}'."
                );
            }

            foreach (var (name, api) in frontend.Options.RemoteApis)
            {
                if (BffRemoteApiValidation.Overlaps(path, api.Path))
                {
                    failures.Add(
                        $"Bff:Frontends:{frontend.Name}:RemoteApis:{name}:Path '{api.Path}' must not overlap the remote API mapped at '{path}'."
                    );
                }
            }
        }
    }
}
