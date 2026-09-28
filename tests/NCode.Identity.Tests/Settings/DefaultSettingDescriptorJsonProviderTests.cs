#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

using System.Text.Json;

namespace NCode.Identity.Settings;

public class DefaultSettingDescriptorJsonProviderTests : BaseTests
{
    private readonly Mock<ISettingDescriptorCollectionProvider> _mockProvider;
    private readonly Mock<ISettingDescriptorCollection> _mockCollection;
    private readonly DefaultSettingDescriptorJsonProvider _provider;

    public DefaultSettingDescriptorJsonProviderTests()
    {
        _mockProvider = CreateStrictMock<ISettingDescriptorCollectionProvider>();
        _mockCollection = CreateStrictMock<ISettingDescriptorCollection>();
        _provider = new DefaultSettingDescriptorJsonProvider(_mockProvider.Object);
    }

    private void SetupNotFound()
    {
        SettingDescriptor? descriptor = null;
        _mockCollection
            .Setup(x => x.TryGet(It.IsAny<string>(), out descriptor))
            .Returns(false)
            .Verifiable();
        _mockProvider.Setup(x => x.Collection).Returns(_mockCollection.Object).Verifiable();
    }

    #region GetDescriptor (JsonTokenType) Tests

    [Theory]
    [InlineData(JsonTokenType.String, typeof(string))]
    [InlineData(JsonTokenType.True, typeof(bool))]
    [InlineData(JsonTokenType.False, typeof(bool))]
    [InlineData(JsonTokenType.Number, typeof(double))]
    [InlineData(JsonTokenType.StartArray, typeof(List<string>))]
    [InlineData(JsonTokenType.StartObject, typeof(JsonElement))]
    public void GetDescriptor_ByTokenType_WhenNotRegistered_CreatesByType(
        JsonTokenType tokenType,
        Type expectedValueType
    )
    {
        SetupNotFound();

        var descriptor = _provider.GetDescriptor("name", tokenType);

        Assert.Equal(expectedValueType, descriptor.ValueType);
        Assert.Equal("name", descriptor.Name);
    }

    #endregion

    #region GetDescriptor (JsonValueKind) Tests

    [Theory]
    [InlineData(JsonValueKind.String, typeof(string))]
    [InlineData(JsonValueKind.True, typeof(bool))]
    [InlineData(JsonValueKind.False, typeof(bool))]
    [InlineData(JsonValueKind.Number, typeof(double))]
    [InlineData(JsonValueKind.Array, typeof(List<string>))]
    [InlineData(JsonValueKind.Object, typeof(JsonElement))]
    public void GetDescriptor_ByValueKind_WhenNotRegistered_CreatesByType(
        JsonValueKind valueKind,
        Type expectedValueType
    )
    {
        SetupNotFound();

        var descriptor = _provider.GetDescriptor("name", valueKind);

        Assert.Equal(expectedValueType, descriptor.ValueType);
        Assert.Equal("name", descriptor.Name);
    }

    #endregion

    #region Registered Descriptor Tests

    [Fact]
    public void GetDescriptor_WhenRegistered_ReturnsRegisteredDescriptor()
    {
        SettingDescriptor? registered = new SettingDescriptor<int> { Name = "name" };
        _mockCollection.Setup(x => x.TryGet("name", out registered)).Returns(true).Verifiable();
        _mockProvider.Setup(x => x.Collection).Returns(_mockCollection.Object).Verifiable();

        var descriptor = _provider.GetDescriptor("name", JsonTokenType.String);

        Assert.Same(registered, descriptor);
    }

    #endregion
}
