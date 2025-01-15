namespace FallGuy.Extensions;

public static class UIntExtensions
{
    public static byte GetHighByte(this uint value)
        => (byte) ((value >> 24) & 0xFF);
}
