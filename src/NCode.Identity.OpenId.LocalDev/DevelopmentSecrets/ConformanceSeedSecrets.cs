// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.LocalDev.DevelopmentSecrets;

/// <summary>
/// Generates the per-machine credentials of the conformance seed — the client secret of each conformance client that
/// authenticates with one, and the test user's password — and hands them to an OpenID host through the environment
/// variables its <c>ConformanceSeed</c> configuration section reads.
/// </summary>
/// <remarks>
/// Each credential is a development secret file beside the development root key, generated on the first run and reused
/// on every later one. The conformance runner reads and generates the same files, so the suite presents the credentials
/// that the host seeded. A client secret is configured <c>Base64Url</c>-encoded, so the file's text is the secret a
/// client presents, and the password is the file's text.
/// </remarks>
// See ADR-1370. The runner's side is build/conformance/Get-ConformanceSeedCredentials.ps1, which keeps the same file
// names and client ids.
internal static class ConformanceSeedSecrets
{
    /// <summary>
    /// The name of the file that holds the test user's password.
    /// </summary>
    internal const string TestUserPasswordFileName = "conformance-user-password";

    /// <summary>
    /// The environment variable that configures the test user's password.
    /// </summary>
    internal const string TestUserPasswordVariable = "ConformanceSeed__TestUserPassword";

    /// <summary>
    /// Gets the ids of the conformance clients that authenticate with a client secret, as the host's
    /// <c>appsettings.Conformance.json</c> lists them.
    /// </summary>
    internal static IReadOnlyList<string> ClientIds { get; } =
    ["client", "client2", "client-secret-post"];

    /// <summary>
    /// Gets the name of the file that holds a conformance client's secret.
    /// </summary>
    /// <param name="clientId">The id of the client.</param>
    /// <returns>The name of the file within the development secrets directory.</returns>
    internal static string GetClientSecretFileName(string clientId) =>
        $"conformance-secret-{clientId}";

    /// <summary>
    /// Gets the environment variable that configures a conformance client's secret.
    /// </summary>
    /// <param name="clientId">The id of the client.</param>
    /// <returns>The name of the environment variable.</returns>
    internal static string GetClientSecretVariable(string clientId) =>
        $"ConformanceSeed__Clients__{clientId}__ClientSecret";

    /// <summary>
    /// Makes sure every credential of the conformance seed exists, generating each one on the first run, and gets the
    /// environment variables that hand them to an OpenID host.
    /// </summary>
    /// <param name="directory">The directory that holds the development secret files, created when missing.</param>
    /// <returns>The value of each environment variable, keyed by its name.</returns>
    internal static IReadOnlyDictionary<string, string> GetEnvironment(string directory)
    {
        var environment = new Dictionary<string, string>(
            ClientIds.Count + 1,
            StringComparer.Ordinal
        );

        foreach (var clientId in ClientIds)
        {
            var path = DevelopmentSecretFile.Ensure(directory, GetClientSecretFileName(clientId));
            environment[GetClientSecretVariable(clientId)] =
                DevelopmentSecretFile.ReadBase64UrlEncoded(path);
        }

        var passwordPath = DevelopmentSecretFile.Ensure(directory, TestUserPasswordFileName);
        environment[TestUserPasswordVariable] = DevelopmentSecretFile.ReadText(passwordPath);

        return environment;
    }

    /// <summary>
    /// Hands the conformance seed's credentials to a resource as environment variables.
    /// </summary>
    /// <param name="builder">The builder of the resource that runs the OpenID host.</param>
    /// <param name="environment">The environment variables that <see cref="GetEnvironment"/> returned.</param>
    /// <typeparam name="T">The type of the resource.</typeparam>
    /// <returns>The <paramref name="builder"/>, for method chaining.</returns>
    internal static IResourceBuilder<T> WithConformanceSeedSecrets<T>(
        this IResourceBuilder<T> builder,
        IReadOnlyDictionary<string, string> environment
    )
        where T : IResourceWithEnvironment
    {
        foreach (var (name, value) in environment)
        {
            builder.WithEnvironment(name, value);
        }

        return builder;
    }
}
