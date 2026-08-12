using System.Runtime.InteropServices;

namespace K617Mod.Core.Suppression.Native;

/// <summary>
/// Small helpers for converting blittable structs to/from the byte[]
/// buffers DeviceIoControl needs, keeping the rest of this namespace
/// free of manual pointer/marshaling code.
/// </summary>
internal static class StructMarshal
{
    public static byte[] ToBytes<T>(T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var buffer = new byte[size];
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false);
        }
        finally
        {
            handle.Free();
        }
        return buffer;
    }

    public static T FromBytes<T>(byte[] bytes) where T : struct
    {
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject())!;
        }
        finally
        {
            handle.Free();
        }
    }
}
