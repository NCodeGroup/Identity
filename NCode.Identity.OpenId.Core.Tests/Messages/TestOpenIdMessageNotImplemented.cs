#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Tests.Messages;

internal class TestOpenIdMessageNotImplemented : ITestOpenIdMessage
{
    public IEnumerator<KeyValuePair<string, StringValues>> GetEnumerator() =>
        throw new NotImplementedException();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();

    public int Count => throw new NotImplementedException();

    public bool ContainsKey(string key) =>
        throw new NotImplementedException();

    public bool TryGetValue(string key, out StringValues value) =>
        throw new NotImplementedException();

    public StringValues this[string key] => throw new NotImplementedException();

    public IEnumerable<string> Keys => throw new NotImplementedException();

    public IEnumerable<StringValues> Values => throw new NotImplementedException();

    public OpenIdEnvironment OpenIdEnvironment => throw new NotImplementedException();

    public string TypeDiscriminator => throw new NotImplementedException();

    public SerializationFormat SerializationFormat
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public IReadOnlyDictionary<string, IParameter> Parameters => throw new NotImplementedException();

    public void Initialize(OpenIdEnvironment openIdEnvironment) =>
        throw new NotImplementedException();

    public void Initialize(OpenIdEnvironment openIdEnvironment, IEnumerable<IParameter> parameters, bool cloneParameters = false) =>
        throw new NotImplementedException();

    public IOpenIdMessage Clone() =>
        throw new NotImplementedException();
}
