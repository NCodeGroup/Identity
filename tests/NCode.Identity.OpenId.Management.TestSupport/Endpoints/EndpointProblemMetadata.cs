// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Reads the problem statuses an endpoint declares in its OpenAPI metadata.
/// </summary>
[ExcludeFromCodeCoverage]
public static class EndpointProblemMetadata
{
    /// <summary>
    /// Maps <paramref name="provider"/> and returns the problem statuses, in ascending order, that the route with the
    /// given HTTP method and pattern declares through <c>ProducesProblem</c> or <c>ProducesValidationProblem</c>. It
    /// counts validation problems too.
    /// </summary>
    public static async ValueTask<IReadOnlyList<int>> ReadStatusCodesAsync(
        IEndpointProvider provider,
        string httpMethod,
        string routePattern,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(httpMethod);
        ArgumentNullException.ThrowIfNull(routePattern);

        await using var app = WebApplication.CreateSlimBuilder().Build();
        provider.Map(app);

        var endpoint = FindEndpoint(app, httpMethod, routePattern);
        return ReadProblemMetadata(endpoint).Select(x => x.StatusCode).Order().ToList();
    }

    /// <summary>
    /// Maps <paramref name="provider"/> and returns the problem statuses, in ascending order, that the route with the
    /// given HTTP method and pattern declares and that its own documentation leaves undescribed. It leaves out the
    /// statuses that the route group describes for every route and the binding <c>400</c> that the route group
    /// appends to a handler that binds a body or a non-string parameter, and it leaves out the statuses that the
    /// handler method documents with a <c>response</c> element.
    /// </summary>
    public static async ValueTask<IReadOnlyList<int>> ReadUndescribedStatusCodesAsync(
        IEndpointProvider provider,
        string httpMethod,
        string routePattern,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(httpMethod);
        ArgumentNullException.ThrowIfNull(routePattern);

        await using var app = WebApplication.CreateSlimBuilder().Build();
        provider.Map(app);

        var endpoint = FindEndpoint(app, httpMethod, routePattern);
        var method = endpoint.Metadata.OfType<MethodInfo>().First();
        var documented = ReadDocumentedStatusCodes(method);
        var bindsFallibly = BindsFallibly(method);

        return ReadProblemMetadata(endpoint)
            .Where(x => !DescribedByRouteGroup.Contains(x.StatusCode))
            .Where(x =>
                !(
                    x.StatusCode == StatusCodes.Status400BadRequest
                    && x.Type == typeof(ProblemDetails)
                    && bindsFallibly
                )
            )
            .Select(x => x.StatusCode)
            .Where(x => !documented.Contains(x))
            .Distinct()
            .Order()
            .ToList();
    }

    private static HashSet<int> DescribedByRouteGroup { get; } =
    [
        StatusCodes.Status401Unauthorized,
        StatusCodes.Status403Forbidden,
        StatusCodes.Status412PreconditionFailed,
        StatusCodes.Status415UnsupportedMediaType,
        StatusCodes.Status503ServiceUnavailable,
    ];

    private static RouteEndpoint FindEndpoint(
        WebApplication app,
        string httpMethod,
        string routePattern
    ) =>
        ((IEndpointRouteBuilder)app)
            .DataSources.SelectMany(x => x.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(x =>
                string.Equals(
                    x.RoutePattern.RawText?.TrimEnd('/'),
                    routePattern.TrimEnd('/'),
                    StringComparison.Ordinal
                )
                && x.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods.Contains(httpMethod)
                    == true
            );

    private static IEnumerable<IProducesResponseTypeMetadata> ReadProblemMetadata(
        RouteEndpoint endpoint
    ) =>
        endpoint
            .Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(x => x.Type is not null && typeof(ProblemDetails).IsAssignableFrom(x.Type));

    private static bool BindsFallibly(MethodInfo method) =>
        method
            .GetParameters()
            .Any(parameter =>
            {
                var attributes = parameter.GetCustomAttributes(inherit: true);
                if (attributes.OfType<IFromBodyMetadata>().Any())
                    return true;

                var bindsRequiredValue =
                    attributes.OfType<IFromQueryMetadata>().Any()
                    || attributes.OfType<IFromHeaderMetadata>().Any();
                var bindsParameter =
                    bindsRequiredValue || attributes.OfType<IFromRouteMetadata>().Any();
                if (!bindsParameter)
                    return false;

                var type =
                    Nullable.GetUnderlyingType(parameter.ParameterType) ?? parameter.ParameterType;
                if (type != typeof(string))
                    return true;

                return bindsRequiredValue
                    && new NullabilityInfoContext().Create(parameter).ReadState
                        == NullabilityState.NotNull
                    && !parameter.HasDefaultValue;
            });

    private static HashSet<int> ReadDocumentedStatusCodes(MethodInfo method)
    {
        var declaringType =
            method.DeclaringType
            ?? throw new InvalidOperationException(
                $"The method '{method.Name}' has no declaring type."
            );

        var xmlPath = Path.ChangeExtension(declaringType.Assembly.Location, ".xml");
        if (!File.Exists(xmlPath))
            throw new InvalidOperationException(
                $"The documentation file '{xmlPath}' of the assembly of '{declaringType.FullName}' does not exist."
            );

        var parameterTypes = string.Join(
            ",",
            method
                .GetParameters()
                .Select(parameter => GetDocumentationName(parameter.ParameterType))
        );
        var memberId = $"M:{GetDocumentationName(declaringType)}.{method.Name}({parameterTypes})";

        var member =
            XDocument
                .Load(xmlPath)
                .Descendants("member")
                .FirstOrDefault(x => (string?)x.Attribute("name") == memberId)
            ?? throw new InvalidOperationException(
                $"The documentation file '{xmlPath}' has no member '{memberId}'."
            );

        return member
            .Elements("response")
            .Select(x => (string?)x.Attribute("code"))
            .Select(x =>
                int.TryParse(x, NumberStyles.None, CultureInfo.InvariantCulture, out var code)
                    ? code
                    : -1
            )
            .Where(x => x >= 0)
            .ToHashSet();
    }

    private static string GetDocumentationName(Type type)
    {
        if (type.IsGenericType)
        {
            var name = type.Name[..type.Name.IndexOf('`')];
            var arguments = string.Join(
                ",",
                type.GetGenericArguments().Select(GetDocumentationName)
            );
            return $"{type.Namespace}.{name}{{{arguments}}}";
        }

        var fullName =
            type.FullName
            ?? throw new InvalidOperationException($"The type '{type.Name}' has no full name.");
        return fullName.Replace('+', '.');
    }
}
