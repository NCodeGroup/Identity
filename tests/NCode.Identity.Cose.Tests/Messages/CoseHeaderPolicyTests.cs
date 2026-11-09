// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Cose.Cbor;
using NCode.Identity.Cose.Messages;

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseHeaderPolicyTests
{
    private static IReadOnlyDictionary<CoseLabel, CoseValue> Empty { get; } =
        new Dictionary<CoseLabel, CoseValue>();

    private static CoseValue Value(string hex) =>
        CoseValue.FromEncodedValue(Convert.FromHexString(hex));

    private static Dictionary<CoseLabel, CoseValue> Map(
        params (CoseLabel Label, CoseValue Value)[] entries
    )
    {
        var map = new Dictionary<CoseLabel, CoseValue>();
        foreach (var (label, value) in entries)
            map[label] = value;

        return map;
    }

    private static ParsedCoseLayer Layer(
        IReadOnlyDictionary<CoseLabel, CoseValue> protectedHeaders,
        IReadOnlyDictionary<CoseLabel, CoseValue>? unprotectedHeaders = null
    ) =>
        new()
        {
            ProtectedBytes = CborMapWriter.EncodeProtected(protectedHeaders),
            Protected = protectedHeaders,
            Unprotected = unprotectedHeaders ?? Empty,
        };

    private static string FailureCode(Action action) =>
        Assert.Throws<CoseException>(action).FailureCode;

    #region ValidateRead Tests

    [Theory]
    [InlineData(1, "4101")]
    [InlineData(2, "01")]
    [InlineData(2, "80")]
    [InlineData(2, "814101")]
    [InlineData(3, "20")]
    [InlineData(16, "4101")]
    [InlineData(4, "6161")]
    [InlineData(5, "01")]
    public void ValidateRead_ParameterOfTheWrongShape_ThrowsInvalidHeader(int label, string hex)
    {
        var protectedHeaders = Map((label, Value(hex)));

        Assert.Equal(
            CoseFailureCodes.InvalidHeader,
            FailureCode(() => CoseHeaderPolicy.ValidateRead(protectedHeaders, Empty))
        );
    }

    [Theory]
    [InlineData(3, "00")]
    [InlineData(3, "6161")]
    [InlineData(16, "183D")]
    [InlineData(16, "6161")]
    public void ValidateRead_ContentTypeOrTypeOfAnAllowedShape_IsAccepted(int label, string hex)
    {
        var protectedHeaders = Map((label, Value(hex)));

        CoseHeaderPolicy.ValidateRead(protectedHeaders, Empty);
    }

    [Fact]
    public void ValidateRead_CritListingATextLabel_IsAccepted()
    {
        var protectedHeaders = Map(
            (CoseHeaderLabels.Critical, CoseValue.FromLabels(["x"])),
            ("x", CoseValue.FromInt64(1))
        );

        CoseHeaderPolicy.ValidateRead(protectedHeaders, Empty);
    }

    [Fact]
    public void ValidateRead_TextLabel_IsNotShapeChecked()
    {
        var protectedHeaders = Map(("alg", CoseValue.FromByteString([1])));

        CoseHeaderPolicy.ValidateRead(protectedHeaders, Empty);
    }

    #endregion

    #region EnsureCriticalUnderstood Tests

    [Fact]
    public void EnsureCriticalUnderstood_NoCrit_Returns()
    {
        var protectedHeaders = Map((CoseHeaderLabels.Algorithm, CoseValue.FromInt64(-7)));

        CoseHeaderPolicy.EnsureCriticalUnderstood(protectedHeaders, []);
    }

    [Fact]
    public void EnsureCriticalUnderstood_LibraryLabel_IsUnderstood()
    {
        var protectedHeaders = Map(
            (CoseHeaderLabels.Critical, CoseValue.FromLabels([CoseHeaderLabels.KeyId])),
            (CoseHeaderLabels.KeyId, CoseValue.FromByteString([1]))
        );

        CoseHeaderPolicy.EnsureCriticalUnderstood(protectedHeaders, []);
    }

    [Fact]
    public void EnsureCriticalUnderstood_TextLabelTheCallerUnderstands_Returns()
    {
        var protectedHeaders = Map(
            (CoseHeaderLabels.Critical, CoseValue.FromLabels(["x"])),
            ("x", CoseValue.FromInt64(1))
        );

        CoseHeaderPolicy.EnsureCriticalUnderstood(protectedHeaders, ["x"]);
    }

    [Fact]
    public void EnsureCriticalUnderstood_TextLabelNotUnderstood_ThrowsCriticalHeaderNotUnderstood()
    {
        var protectedHeaders = Map(
            (CoseHeaderLabels.Critical, CoseValue.FromLabels(["x"])),
            ("x", CoseValue.FromInt64(1))
        );

        Assert.Equal(
            CoseFailureCodes.CriticalHeaderNotUnderstood,
            FailureCode(() => CoseHeaderPolicy.EnsureCriticalUnderstood(protectedHeaders, []))
        );
    }

    #endregion

    #region ValidateWrite Tests

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void ValidateWrite_IvOrPartialIv_Throws(int label)
    {
        var protectedHeaders = Map((label, CoseValue.FromByteString([0])));

        Assert.Throws<ArgumentException>(() =>
            CoseHeaderPolicy.ValidateWrite(protectedHeaders, Empty, 32)
        );
    }

    [Fact]
    public void ValidateWrite_LabelInBothBuckets_ThrowsWrappingTheRule()
    {
        var protectedHeaders = Map((CoseHeaderLabels.KeyId, CoseValue.FromByteString([1])));
        var unprotectedHeaders = Map((CoseHeaderLabels.KeyId, CoseValue.FromByteString([1])));

        var exception = Assert.Throws<ArgumentException>(() =>
            CoseHeaderPolicy.ValidateWrite(protectedHeaders, unprotectedHeaders, 32)
        );

        var inner = Assert.IsType<CoseException>(exception.InnerException);
        Assert.Equal(CoseFailureCodes.InvalidHeader, inner.FailureCode);
    }

    #endregion

    #region ResolveLayerAlgorithm Tests

    [Fact]
    public async Task ResolveLayerAlgorithm_NoAlgorithm_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();
        var layer = Layer(Map());

        Assert.Equal(
            CoseFailureCodes.InvalidHeader,
            FailureCode(() =>
                CoseHeaderPolicy.ResolveLayerAlgorithm(
                    layer,
                    [-7],
                    CoseAlgorithmKind.Signature,
                    host.Algorithms
                )
            )
        );
    }

    [Fact]
    public async Task ResolveLayerAlgorithm_TextAlgorithm_ThrowsUnsupportedAlgorithm()
    {
        await using var host = new CoseTestHost();
        var layer = Layer(Map((CoseHeaderLabels.Algorithm, CoseValue.FromTextString("ES256"))));

        Assert.Equal(
            CoseFailureCodes.UnsupportedAlgorithm,
            FailureCode(() =>
                CoseHeaderPolicy.ResolveLayerAlgorithm(
                    layer,
                    [-7],
                    CoseAlgorithmKind.Signature,
                    host.Algorithms
                )
            )
        );
    }

    [Fact]
    public async Task ResolveLayerAlgorithm_AlgorithmBeyondInt32_ThrowsAlgorithmNotPermitted()
    {
        await using var host = new CoseTestHost();
        var layer = Layer(Map((CoseHeaderLabels.Algorithm, Value("1B0000000100000000"))));

        Assert.Equal(
            CoseFailureCodes.AlgorithmNotPermitted,
            FailureCode(() =>
                CoseHeaderPolicy.ResolveLayerAlgorithm(
                    layer,
                    [-7],
                    CoseAlgorithmKind.Signature,
                    host.Algorithms
                )
            )
        );
    }

    [Fact]
    public async Task ResolveLayerAlgorithm_AlgorithmOfAnotherKind_ThrowsUnsupportedAlgorithm()
    {
        await using var host = new CoseTestHost();
        var layer = Layer(Map((CoseHeaderLabels.Algorithm, CoseValue.FromInt64(5))));

        Assert.Equal(
            CoseFailureCodes.UnsupportedAlgorithm,
            FailureCode(() =>
                CoseHeaderPolicy.ResolveLayerAlgorithm(
                    layer,
                    [5],
                    CoseAlgorithmKind.Signature,
                    host.Algorithms
                )
            )
        );
    }

    [Fact]
    public async Task ResolveLayerAlgorithm_PermittedSignatureAlgorithm_ReturnsTheBinding()
    {
        await using var host = new CoseTestHost();
        var layer = Layer(Map((CoseHeaderLabels.Algorithm, CoseValue.FromInt64(-7))));

        var binding = CoseHeaderPolicy.ResolveLayerAlgorithm(
            layer,
            [-7],
            CoseAlgorithmKind.Signature,
            host.Algorithms
        );

        Assert.Equal(-7, binding.Algorithm.Identifier);
    }

    #endregion
}
