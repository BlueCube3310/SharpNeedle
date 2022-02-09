﻿namespace SharpNeedle.Utilities;
using System.IO;

public static class BinaryHelper
{
    public static readonly Endianness PlatformEndianness =
        BitConverter.IsLittleEndian ? Endianness.Little : Endianness.Big;

    public static unsafe TSize MakeSignature<TSize>(string sig, byte placeholder = 0) where TSize : unmanaged
    {
        if (string.IsNullOrEmpty(sig))
            return default;

        Span<byte> result = stackalloc byte[Unsafe.SizeOf<TSize>()];
        result.Fill(placeholder);

        for (int i = 0; i < Math.Min(sig.Length, Unsafe.SizeOf<TSize>()); i++)
            result[i] = (byte)sig[i];

        return Unsafe.As<byte, TSize>(ref result[0]);
    }

    public static string ReadStringOffset(this BinaryObjectReader reader, StringBinaryFormat format = StringBinaryFormat.NullTerminated, int fixedLength = -1)
    {
        var offset = reader.ReadOffsetValue();
        if (offset == 0)
            return null;

        using var token = reader.AtOffset(offset);
        return reader.ReadString(format, fixedLength);
    }

    public static bool EnsureSignatureNative<TSignature>(this BinaryValueReader reader, 
        TSignature expected, bool throwOnFail = true) where TSignature: unmanaged
    {
        var sig = reader.ReadNative<TSignature>();
        if (sig.Equals(expected)) return true;
        
        if (throwOnFail)
            throw new BadImageFormatException($"Signature mismatch. Expected: {expected}. Got: {sig}");

        return false;
    }

    public static bool EnsureSignature<TSignature>(this BinaryValueReader reader,
        TSignature expected, bool throwOnFail = true) where TSignature : unmanaged
    {
        var sig = reader.Read<TSignature>();
        if (sig.Equals(expected)) return true;

        if (throwOnFail)
            throw new BadImageFormatException($"Signature mismatch. Expected: {expected}. Got: {sig}");

        return false;
    }

    public static bool EnsureSignature<TSignature>(TSignature sig, bool throwOnFail, params TSignature[] expected)
    {
        if (expected.Any(x => x.Equals(sig))) return true;
        
        if (throwOnFail)
            throw new BadImageFormatException($"Signature mismatch. Expected: {expected}. Got: {sig}");

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteOffsetValue(this BinaryObjectWriter writer, long value)
    {
        if (writer.OffsetBinaryFormat == OffsetBinaryFormat.U32)
            writer.Write((uint)value);
        else
            writer.Write(value);
    }

    public static List<T> Unwind<T>(this BinaryList<BinaryPointer<T>> list) where T : IBinarySerializable, new()
    {
        var result = new List<T>(list.Count);
        foreach (var item in list)
            result.Add(item);

        return result;
    }

    public static unsafe T* Pointer<T>(this T[] data) where T : unmanaged
    {
        if (data == null || data.Length == 0)
            return null;

        return (T*)Unsafe.AsPointer(ref data[0]);
    }

    public static SeekToken At(this BinaryValueReader reader)
        => new SeekToken(reader.GetBaseStream(), reader.Position, SeekOrigin.Begin);

    public static SeekToken At(this BinaryValueWriter reader)
        => new SeekToken(reader.GetBaseStream(), reader.Position, SeekOrigin.Begin);
}