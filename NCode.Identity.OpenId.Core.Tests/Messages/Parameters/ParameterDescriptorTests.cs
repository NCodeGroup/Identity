#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

using Moq;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parameters;

public class ParameterDescriptorTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Constructor_GivenKnownParameter_ThenValid()
    {
        const string parameterName = "parameterName";
        const bool allowMissingStringValues = false;

        var mockParser = MockRepository.Create<ParameterParser<string>>();

        var knownParameter = new KnownParameter<string>(
            parameterName,
            mockParser.Object)
        {
            AllowMissingStringValues = allowMissingStringValues,
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Equal(parameterName, descriptor.ParameterName);
        Assert.Same(knownParameter, descriptor.KnownParameter);
        Assert.Equal(allowMissingStringValues, descriptor.AllowMissingStringValues);
        Assert.Same(mockParser.Object, descriptor.Loader);
    }

    [Fact]
    public void Constructor_GivenUnknownParameter_ThenValid()
    {
        const string parameterName = "parameterName";
        const bool allowMissingStringValues = true;

        var descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default);

        Assert.Equal(parameterName, descriptor.ParameterName);
        Assert.Null(descriptor.KnownParameter);
        Assert.Equal(allowMissingStringValues, descriptor.AllowMissingStringValues);
        Assert.Same(ParameterLoader.Default, descriptor.Loader);
    }

    [Fact]
    public void Equals_GivenIdenticalObject_WhenKnownParameter_ThenEqual()
    {
        const string parameterName = "parameterName";
        const bool allowMissingStringValues = false;

        var mockParser = MockRepository.Create<ParameterParser<string>>();

        var knownParameter = new KnownParameter<string>(
            parameterName,
            mockParser.Object)
        {
            AllowMissingStringValues = allowMissingStringValues,
        };

        var descriptor1 = new ParameterDescriptor(knownParameter);
        object descriptor2 = new ParameterDescriptor(knownParameter);
        var result = descriptor1.Equals(descriptor2);
        Assert.True(result);
    }

    [Fact]
    public void Equals_GivenIdenticalObject_WhenUnknownParameter_ThenEqual()
    {
        const string parameterName = "parameterName";

        var descriptor1 = new ParameterDescriptor(parameterName, ParameterLoader.Default);
        object descriptor2 = new ParameterDescriptor(parameterName, ParameterLoader.Default);
        var result = descriptor1.Equals(descriptor2);
        Assert.True(result);
    }

    [Fact]
    public void Equals_GivenIdenticalStruct_WhenKnownParameter_ThenEqual()
    {
        const string parameterName = "parameterName";
        const bool allowMissingStringValues = false;

        var mockParser = MockRepository.Create<ParameterParser<string>>();

        var knownParameter = new KnownParameter<string>(
            parameterName,
            mockParser.Object)
        {
            AllowMissingStringValues = allowMissingStringValues,
        };

        var descriptor1 = new ParameterDescriptor(knownParameter);
        var descriptor2 = new ParameterDescriptor(knownParameter);
        var result = descriptor1.Equals(descriptor2);
        Assert.True(result);
    }

    [Fact]
    public void Equals_GivenIdenticalStruct_WhenUnknownParameter_ThenEqual()
    {
        const string parameterName = "parameterName";

        var descriptor1 = new ParameterDescriptor(parameterName, ParameterLoader.Default);
        var descriptor2 = new ParameterDescriptor(parameterName, ParameterLoader.Default);
        var result = descriptor1.Equals(descriptor2);
        Assert.True(result);
    }

    [Fact]
    public void GetHashCode_WhenKnownParameter_ThenValid()
    {
        const string parameterName = "parameterName";
        const bool allowMissingStringValues = false;

        var mockParser = MockRepository.Create<ParameterParser<string>>();

        var knownParameter = new KnownParameter<string>(
            parameterName,
            mockParser.Object)
        {
            AllowMissingStringValues = allowMissingStringValues,
        };

        var descriptor1 = new ParameterDescriptor(knownParameter);
        var descriptor2 = new ParameterDescriptor(knownParameter);
        Assert.Equal(descriptor1.GetHashCode(), descriptor2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_WhenUnknownParameter_ThenValid()
    {
        const string parameterName = "parameterName";

        var descriptor1 = new ParameterDescriptor(parameterName, ParameterLoader.Default);
        var descriptor2 = new ParameterDescriptor(parameterName, ParameterLoader.Default);
        Assert.Equal(descriptor1.GetHashCode(), descriptor2.GetHashCode());
    }
}
