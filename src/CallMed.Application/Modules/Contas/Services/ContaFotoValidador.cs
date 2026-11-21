using System.Buffers.Binary;
namespace MKSANCrud.Services.Usuarios;
public static class ContaFotoValidador
{
    public const int LimiteBytes = 2 * 1024 * 1024;
    private static bool Dimensoes(int w, int h) => w > 0 && h > 0 && w <= 8192 && h <= 8192 && (long)w * h <= 33554432;
    public static string? Tipo(byte[] b)
    {
        if (b.Length == 0 || b.Length > LimiteBytes) return null;
        if (b.Length >= 45 && b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) &&
            b.AsSpan(12,4).SequenceEqual("IHDR"u8) && BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(8,4)) == 13 &&
            b.AsSpan(b.Length-12).SequenceEqual(new byte[]{0,0,0,0,73,69,78,68,174,66,96,130}))
        {
            var w = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(16,4)); var h = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(20,4));
            var pos = 8; var dados = false;
            while (pos + 12 <= b.Length)
            {
                var len = BinaryPrimitives.ReadInt32BigEndian(b.AsSpan(pos,4));
                if (len < 0 || (long)pos + 12 + len > b.Length) return null;
                if (b.AsSpan(pos+4,4).SequenceEqual("IDAT"u8) && len > 0) dados = true;
                pos += 12 + len;
            }
            return pos == b.Length && dados && Dimensoes(w,h) ? "image/png" : null;
        }
        if (b.Length >= 10 && b[0] == 255 && b[1] == 216 && b[^2] == 255 && b[^1] == 217)
        {
            var pos = 2;
            while (pos + 4 < b.Length)
            {
                if (b[pos++] != 255) return null;
                while (pos < b.Length && b[pos] == 255) pos++;
                if (pos >= b.Length) return null;
                var marker = b[pos++]; if (marker == 218 || marker == 217) break;
                if (marker is >= 208 and <= 215) continue;
                if (pos+2 > b.Length) return null;
                var len = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos,2));
                if (len < 2 || pos+len > b.Length) return null;
                if (marker is 192 or 193 or 194 or 195 or 197 or 198 or 199 or 201 or 202 or 203 or 205 or 206 or 207)
                {
                    if (len < 8) return null;
                    var h = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos+3,2)); var w = BinaryPrimitives.ReadUInt16BigEndian(b.AsSpan(pos+5,2));
                    return Dimensoes(w,h) ? "image/jpeg" : null;
                }
                pos += len;
            }
        }
        return null;
    }
}
