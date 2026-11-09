// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Secrets;
using NCode.Identity.Secrets.Collections;
using Xunit;

namespace NCode.Identity.Bff.Tests.Secrets;

public sealed class BffKeyRingsTests : BaseTests
{
    #region Add Tests

    [Fact]
    public void Add_WithANameAlreadyLoaded_Throws()
    {
        var keyRings = new BffKeyRings();
        keyRings.Add("ring", CreateStrictMock<ISecretKeyCollectionProvider>().Object);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            keyRings.Add("ring", CreateStrictMock<ISecretKeyCollectionProvider>().Object)
        );

        Assert.Equal("The key ring 'ring' is loaded more than once.", exception.Message);
    }

    #endregion

    #region Get Tests

    [Fact]
    public void Get_WithALoadedKeyRing_ReturnsIt()
    {
        var keyRing = CreateStrictMock<ISecretKeyCollectionProvider>().Object;
        var keyRings = new BffKeyRings();
        keyRings.Add("ring", keyRing);

        var result = keyRings.Get("ring");

        Assert.Same(keyRing, result);
    }

    [Fact]
    public void Get_WithAKeyRingThatIsNotLoaded_Throws()
    {
        var keyRings = new BffKeyRings();

        var exception = Assert.Throws<InvalidOperationException>(() => keyRings.Get("missing"));

        Assert.Equal(
            $"The key ring 'missing' is not loaded: the BFF loads the key rings of '{BffDefaults.ConfigurationSectionName}:KeyRings' when the host starts.",
            exception.Message
        );
    }

    #endregion

    #region DisposeAsync Tests

    [Fact]
    public async Task DisposeAsync_DisposesEveryLoadedKeyRing()
    {
        var first = CreateStrictMock<ISecretKeyCollectionProvider>();
        first.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        var second = CreateStrictMock<ISecretKeyCollectionProvider>();
        second.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        var keyRings = new BffKeyRings();
        keyRings.Add("first", first.Object);
        keyRings.Add("second", second.Object);

        await keyRings.DisposeAsync();
    }

    #endregion
}
