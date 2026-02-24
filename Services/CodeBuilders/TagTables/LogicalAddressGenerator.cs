using System;

namespace OpennessCopy.Services.CodeBuilders.TagTables;

public enum AddressArea
{
    Input,
    Output,
    Merker
}

public sealed class LogicalAddressGenerator
{
    private readonly string _areaPrefix;
    private int _byteIndex;
    private int _bitIndex;

    public LogicalAddressGenerator(AddressArea area, int startByte, int startBit = 0)
    {
        if (startByte < 0) throw new ArgumentOutOfRangeException(nameof(startByte), "Start byte cannot be negative.");
        if (startBit is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(startBit), "Start bit must be between 0 and 7.");

        _areaPrefix = area switch
        {
            AddressArea.Input => "%I",
            AddressArea.Output => "%Q",
            AddressArea.Merker => "%M",
            _ => throw new ArgumentOutOfRangeException(nameof(area), "Unsupported address area.")
        };

        _byteIndex = startByte;
        _bitIndex = startBit;
    }

    public string Next(int bitStep = 1)
    {
        var address = Format();
        AdvanceBits(bitStep);
        return address;
    }

    public string Peek() => Format();

    public void AdvanceBits(int bitCount)
    {
        if (bitCount < 0) throw new ArgumentOutOfRangeException(nameof(bitCount), "Bit increment must be non-negative.");

        var totalBits = _bitIndex + bitCount;
        _byteIndex += totalBits / 8;
        _bitIndex = totalBits % 8;
    }

    public void AdvanceBytes(int byteCount)
    {
        if (byteCount < 0) throw new ArgumentOutOfRangeException(nameof(byteCount), "Byte increment must be non-negative.");
        AlignToByteBoundary();
        _byteIndex += byteCount;
    }

    public string NextByteAligned(int byteStep = 1)
    {
        AlignToByteBoundary();
        var address = Format();
        AdvanceBytes(byteStep);
        return address;
    }

    private void AlignToByteBoundary()
    {
        if (_bitIndex != 0)
        {
            _byteIndex += 1;
            _bitIndex = 0;
        }
    }

    private string Format() => $"{_areaPrefix}{_byteIndex}.{_bitIndex}";
}
