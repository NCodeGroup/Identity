// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.SdJwt.Tests;

public class SelectiveDisclosureJwtTests
{
    private const string IssuerJwt = "eyJhbGciOiJFUzI1NiJ9.eyJfc2RfYWxnIjoic2hhLTI1NiJ9.c2ln";
    private const string KeyBindingJwt = "eyJhbGciOiJFUzI1NiIsInR5cCI6ImtiK2p3dCJ9.e30.a2I";

    #region Disclosures Property Tests

    [Fact]
    public void Disclosures_WithAOneShotSequence_HoldsASnapshot()
    {
        var disclosure = Disclosure.Parse(SpecVectors.EmailDisclosure);
        var yields = 0;
        IEnumerable<Disclosure> Once()
        {
            yields++;
            yield return disclosure;
        }

        var sdJwt = new SelectiveDisclosureJwt(IssuerJwt, Once(), keyBindingJwt: null);

        Assert.Same(disclosure, Assert.Single(sdJwt.Disclosures));
        Assert.Same(disclosure, Assert.Single(sdJwt.Disclosures));
        Assert.Equal(1, yields);
    }

    #endregion

    #region HasKeyBinding Property Tests

    [Fact]
    public void HasKeyBinding_WithKeyBindingJwt_IsTrue()
    {
        var sdJwt = new SelectiveDisclosureJwt(IssuerJwt, [], KeyBindingJwt);

        Assert.True(sdJwt.HasKeyBinding);
        Assert.Equal(KeyBindingJwt, sdJwt.KeyBindingJwt);
    }

    [Fact]
    public void HasKeyBinding_WithoutKeyBindingJwt_IsFalse()
    {
        var sdJwt = new SelectiveDisclosureJwt(IssuerJwt, [], keyBindingJwt: null);

        Assert.False(sdJwt.HasKeyBinding);
        Assert.Null(sdJwt.KeyBindingJwt);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_WithoutKeyBinding_EndsWithSeparator()
    {
        var disclosure = Disclosure.Parse(SpecVectors.EmailDisclosure);
        var sdJwt = new SelectiveDisclosureJwt(IssuerJwt, [disclosure], keyBindingJwt: null);

        Assert.Equal($"{IssuerJwt}~{SpecVectors.EmailDisclosure}~", sdJwt.ToString());
    }

    [Fact]
    public void ToString_WithKeyBinding_EndsWithTheKeyBindingJwt()
    {
        var disclosure = Disclosure.Parse(SpecVectors.EmailDisclosure);
        var sdJwt = new SelectiveDisclosureJwt(IssuerJwt, [disclosure], KeyBindingJwt);

        Assert.Equal(
            $"{IssuerJwt}~{SpecVectors.EmailDisclosure}~{KeyBindingJwt}",
            sdJwt.ToString()
        );
    }

    #endregion
}
