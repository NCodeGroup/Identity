// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Cose.Tests.Messages;

public class CoseInspectTests
{
    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static string FailureCode(
        CoseTestHost host,
        string hex,
        CoseMessageType? untagged = null
    ) =>
        Assert.Throws<CoseException>(() => host.Serializer.Inspect(Hex(hex), untagged)).FailureCode;

    #region Inspect Tests

    [Fact]
    public async Task Inspect_Sign1_ReportsTypeTaggingAndHeaders()
    {
        await using var host = new CoseTestHost();

        var info = host.Serializer.Inspect(Hex(CoseVectors.V2Sign1ExternalAad), null);

        Assert.Equal(CoseMessageType.Sign1, info.MessageType);
        Assert.True(info.IsTagged);
        Assert.False(info.IsContentDetached);
        Assert.True(
            info.ProtectedHeaders[CoseHeaderLabels.Algorithm].TryGetInt64(out var algorithm)
        );
        Assert.Equal(-7, algorithm);
        Assert.True(info.UnprotectedHeaders[CoseHeaderLabels.KeyId].TryGetByteString(out var kid));
        Assert.Equal("11"u8.ToArray(), kid.ToArray());
        Assert.Empty(info.Signatures);
        Assert.Empty(info.Recipients);
    }

    [Fact]
    public async Task Inspect_Sign_ReportsTheSignatureHeaders()
    {
        await using var host = new CoseTestHost();

        var info = host.Serializer.Inspect(Hex(CoseVectors.V4Sign), null);

        Assert.Equal(CoseMessageType.Sign, info.MessageType);
        var signature = Assert.Single(info.Signatures);
        Assert.True(
            signature.ProtectedHeaders[CoseHeaderLabels.Algorithm].TryGetInt64(out var algorithm)
        );
        Assert.Equal(-7, algorithm);
    }

    [Fact]
    public async Task Inspect_Mac_ReportsTheRecipientHeaders()
    {
        await using var host = new CoseTestHost();

        var info = host.Serializer.Inspect(Hex(CoseVectors.V8Mac), null);

        Assert.Equal(CoseMessageType.Mac, info.MessageType);
        var recipient = Assert.Single(info.Recipients);
        Assert.Empty(recipient.ProtectedHeaders);
        Assert.True(
            recipient.UnprotectedHeaders[CoseHeaderLabels.Algorithm].TryGetInt64(out var algorithm)
        );
        Assert.Equal(-6, algorithm);
    }

    [Fact]
    public async Task Inspect_Encrypt_ReportsTheRecipientHeaders()
    {
        await using var host = new CoseTestHost();

        var info = host.Serializer.Inspect(Hex(CoseVectors.V12Encrypt), null);

        Assert.Equal(CoseMessageType.Encrypt, info.MessageType);
        Assert.Single(info.Recipients);
        Assert.True(info.UnprotectedHeaders.ContainsKey(CoseHeaderLabels.InitializationVector));
    }

    [Fact]
    public async Task Inspect_DetachedContent_IsReported()
    {
        await using var host = new CoseTestHost();

        var info = host.Serializer.Inspect(Hex("D28440A0F640"), null);

        Assert.True(info.IsContentDetached);
    }

    [Fact]
    public async Task Inspect_Untagged_RequiresTheMessageType()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(CoseFailureCodes.UntaggedMessage, FailureCode(host, CoseVectors.V3Untagged));

        var info = host.Serializer.Inspect(Hex(CoseVectors.V3Untagged), CoseMessageType.Sign1);
        Assert.False(info.IsTagged);
        Assert.Equal(CoseMessageType.Sign1, info.MessageType);
    }

    [Fact]
    public async Task Inspect_UnknownTag_ThrowsUnsupportedMessageType()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(
            CoseFailureCodes.UnsupportedMessageType,
            FailureCode(host, CoseVectors.V17UnknownTag)
        );
    }

    [Fact]
    public async Task Inspect_InputOverTheSizeLimit_ThrowsLimitExceeded()
    {
        await using var host = new CoseTestHost(options => options.MaxMessageSizeBytes = 16);

        Assert.Equal(
            CoseFailureCodes.LimitExceeded,
            FailureCode(host, CoseVectors.V2Sign1ExternalAad)
        );
    }

    [Fact]
    public async Task Inspect_WrongArrayLength_ThrowsInvalidMessage()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(CoseFailureCodes.InvalidMessage, FailureCode(host, "D28540A04040" + "40"));
    }

    [Fact]
    public async Task Inspect_TrailingBytes_ThrowsInvalidMessage()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(CoseFailureCodes.InvalidMessage, FailureCode(host, "D28440A0404000"));
    }

    [Fact]
    public async Task Inspect_LabelInBothBuckets_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(CoseFailureCodes.InvalidHeader, FailureCode(host, "D28443A10126A101264040"));
    }

    [Fact]
    public async Task Inspect_CritInTheUnprotectedBucket_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(CoseFailureCodes.InvalidHeader, FailureCode(host, "D28440A1028101" + "4040"));
    }

    [Fact]
    public async Task Inspect_CritNamingAnAbsentLabel_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(CoseFailureCodes.InvalidHeader, FailureCode(host, "D28445A102811864A04040"));
    }

    [Fact]
    public async Task Inspect_PartialIv_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(CoseFailureCodes.InvalidHeader, FailureCode(host, "D28440A10641004040"));
    }

    [Fact]
    public async Task Inspect_RecipientWithProtectedHeaders_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(
            CoseFailureCodes.InvalidHeader,
            FailureCode(host, "D8608440A04081" + "8343A10125A040")
        );
    }

    [Fact]
    public async Task Inspect_RecipientWithRecipients_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(
            CoseFailureCodes.InvalidHeader,
            FailureCode(host, "D8608440A04081" + "8440A04080")
        );
    }

    [Fact]
    public async Task Inspect_SignBodyWithAlgorithm_ThrowsInvalidHeader()
    {
        await using var host = new CoseTestHost();

        Assert.Equal(
            CoseFailureCodes.InvalidHeader,
            FailureCode(host, "D8628443A10126A04081" + "8340A040")
        );
    }

    [Fact]
    public async Task Inspect_MoreRecipientsThanTheLimit_ThrowsLimitExceeded()
    {
        await using var host = new CoseTestHost();
        var recipients = string.Concat(Enumerable.Repeat("8340A040", 17));

        Assert.Equal(
            CoseFailureCodes.LimitExceeded,
            FailureCode(host, "D8608440A040" + "91" + recipients)
        );
    }

    [Fact]
    public async Task Inspect_EmptyProtectedMapBytes_IsAccepted()
    {
        await using var host = new CoseTestHost();

        var info = host.Serializer.Inspect(Hex(CoseVectors.V1UnprotectedAlgorithm), null);

        Assert.Empty(info.ProtectedHeaders);
        Assert.Contains(
            CoseHeaderLabels.Algorithm,
            info.UnprotectedHeaders.Keys.Select(label => label.IntegerValue)
        );
    }

    #endregion
}
