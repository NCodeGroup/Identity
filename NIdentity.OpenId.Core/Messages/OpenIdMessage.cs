#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System.Collections;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Messages;

internal abstract class OpenIdMessage : IOpenIdMessage
{
    private bool IsInitialized { get; set; }
    private IOpenIdMessageContext? ContextOrNull { get; set; }
    private Dictionary<string, Parameter>? ParametersOrNull { get; set; }

    /// <inheritdoc />
    public IOpenIdMessageContext Context => ContextOrNull ?? throw new InvalidOperationException("TODO");

    public IReadOnlyDictionary<string, Parameter> Parameters => ParametersOrNull ?? throw new InvalidOperationException("TODO");

    protected internal void Initialize(IOpenIdMessageContext context, IEnumerable<Parameter> parameters)
    {
        if (IsInitialized) throw new InvalidOperationException("TODO");

        ContextOrNull = context;
        ParametersOrNull = parameters.ToDictionary(parameter => parameter.Descriptor.ParameterName, StringComparer.Ordinal);
        IsInitialized = true;
    }

    /// <inheritdoc />
    public int Count => Parameters.Count;

    /// <inheritdoc />
    public IEnumerable<string> Keys => Parameters.Keys;

    /// <inheritdoc />
    public IEnumerable<StringValues> Values => Parameters.Values.Select(_ => _.StringValues);

    /// <inheritdoc />
    public StringValues this[string key] => Parameters[key].StringValues;

    /// <inheritdoc />
    public bool ContainsKey(string key) => Parameters.ContainsKey(key);

    /// <inheritdoc />
    public bool TryGetValue(string key, out StringValues value)
    {
        if (!Parameters.TryGetValue(key, out var parameter))
        {
            value = default;
            return false;
        }

        value = parameter.StringValues;
        return true;
    }

    /// <inheritdoc />
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() => Parameters
        .Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value.StringValues))
        .GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    protected internal T? GetKnownParameter<T>(KnownParameter<T> knownParameter) =>
        Parameters.TryGetValue(knownParameter.Name, out var parameter) &&
        parameter is Parameter<T> typedParameter ?
            typedParameter.ParsedValue :
            default;

    protected internal void SetKnownParameter<T>(KnownParameter<T> knownParameter, T? parsedValue)
    {
        var context = ContextOrNull ?? throw new InvalidOperationException("TODO");
        var parameters = ParametersOrNull ?? throw new InvalidOperationException("TODO");

        var parameterName = knownParameter.Name;
        if (parsedValue is null)
        {
            parameters.Remove(parameterName);
            return;
        }

        var stringValues = knownParameter.Parser.Serialize(context, parsedValue);
        if (StringValues.IsNullOrEmpty(stringValues))
        {
            parameters.Remove(parameterName);
            return;
        }

        var descriptor = new ParameterDescriptor(knownParameter);
        var parameter = descriptor.Loader.Load(context, descriptor, stringValues, parsedValue);
        parameters[knownParameter.Name] = parameter;
    }
}

internal abstract class OpenIdMessage<T> : OpenIdMessage
    where T : OpenIdMessage, new()
{
    public static T Load(IOpenIdMessageContext context, IEnumerable<Parameter> parameters)
    {
        var message = new T();
        message.Initialize(context, parameters);
        return message;
    }

    public static T Load(IOpenIdMessageContext context, IEnumerable<KeyValuePair<string, StringValues>> parameterStringValues)
    {
        var parameters = parameterStringValues
            .GroupBy(
                kvp => kvp.Key,
                kvp => kvp.Value.AsEnumerable(),
                StringComparer.Ordinal)
            .Select(grouping => Parameter.Load(
                context,
                grouping.Key,
                grouping.SelectMany(stringValues => stringValues)));

        return Load(context, parameters);
    }

    public static async ValueTask<T> LoadAsync(IOpenIdMessageContext messageContext, CancellationToken cancellationToken)
    {
        var httpRequest = messageContext.HttpContext.Request;

        IEnumerable<KeyValuePair<string, StringValues>> parameterStringValues;
        if (HttpMethods.IsGet(httpRequest.Method))
        {
            parameterStringValues = httpRequest.Query;
        }
        else if (HttpMethods.IsPost(httpRequest.Method))
        {
            const string expectedContentType = "application/x-www-form-urlencoded";
            if (!httpRequest.ContentType?.StartsWith(expectedContentType, StringComparison.OrdinalIgnoreCase) ?? false)
            {
                throw OpenIdException.Factory
                    .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
                    .WithErrorDescription($"The content type of the request must be '{expectedContentType}'. Received '{httpRequest.ContentType}'.")
                    .WithStatusCode(StatusCodes.Status415UnsupportedMediaType);
            }

            parameterStringValues = await httpRequest.ReadFormAsync(cancellationToken);
        }
        else
        {
            throw OpenIdException.Factory
                .Create(OpenIdConstants.ErrorCodes.InvalidRequest)
                .WithStatusCode(StatusCodes.Status405MethodNotAllowed);
        }

        return Load(messageContext, parameterStringValues);
    }
}
