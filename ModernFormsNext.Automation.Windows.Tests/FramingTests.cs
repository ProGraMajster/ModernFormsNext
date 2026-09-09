using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace ModernFormsNext.Automation.Windows.Tests;

[Trait("Category", "Framing")]
public sealed class FramingTests
{
    [Fact]
    public async Task PrefixAndBodyMayArriveOneByteAtATime()
    {
        byte[] body = Encoding.UTF8.GetBytes("{\"value\":\"line\\nvalue\"}");
        using var frame = new MemoryStream(); await Protocol.WriteFrame(frame, body, default);
        using var fragmented = new FragmentedStream(frame.ToArray());
        Assert.Equal(body, await Protocol.ReadFrame(fragmented, 4096, default));
        Assert.Null(await Protocol.ReadFrame(fragmented, 4096, default));
    }

    [Theory]
    [InlineData(0, AutomationTransportError.InvalidRequest)]
    [InlineData(-1, AutomationTransportError.InvalidRequest)]
    [InlineData(int.MaxValue, AutomationTransportError.PayloadTooLarge)]
    public async Task InvalidLengthRejectedBeforeReadingOrAllocatingBody(int length, AutomationTransportError expected)
    {
        byte[] prefix = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(prefix, length);
        using var stream = new MemoryStream(prefix);
        var error = await Assert.ThrowsAsync<AutomationTransportException>(() => Protocol.ReadFrame(stream, 4096, default));
        Assert.Equal(expected, error.Error); Assert.Equal(4, stream.Position);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(7)]
    public async Task TruncatedPrefixOrBodyIsEof(int bytes)
    {
        byte[] frame = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(frame, 4);
        using var stream = new MemoryStream(frame[..bytes]);
        await Assert.ThrowsAsync<EndOfStreamException>(() => Protocol.ReadFrame(stream, 4096, default));
    }

    [Fact]
    public void ResponseBudgetRejectsDuringSerialization()
    {
        var error = Assert.Throws<AutomationTransportException>(() => Protocol.Encode(new { Value = new string('x', 100000) }, 4096));
        Assert.Equal(AutomationTransportError.PayloadTooLarge, error.Error);
    }

    [Fact]
    public void DuplicateEnvelopeFieldsAreRejected()
    {
        var bytes = Encoding.UTF8.GetBytes("{\"version\":1,\"id\":1,\"id\":2,\"kind\":0,\"deadlineMilliseconds\":1000,\"payload\":{}}");
        Assert.Equal(AutomationTransportError.InvalidRequest, Assert.Throws<AutomationTransportException>(() => Protocol.DecodeRequest(bytes)).Error);
    }

    private sealed class FragmentedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
