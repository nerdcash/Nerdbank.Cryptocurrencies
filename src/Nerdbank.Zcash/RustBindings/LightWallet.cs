


using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
namespace uniffi.LightWallet;



// This is a helper for safely working with byte buffers returned from the Rust code.
// A rust-owned buffer is represented by its capacity, its current length, and a
// pointer to the underlying data.

[StructLayout(LayoutKind.Sequential)]
internal struct RustBuffer {
    public int capacity;
    public int len;
    public IntPtr data;

    public static RustBuffer Alloc(int size) {
        return _UniffiHelpers.RustCall((ref RustCallStatus status) => {
            var buffer = _UniFFILib.ffi_nerdbank_zcash_rust_rustbuffer_alloc(size, ref status);
            if (buffer.data == IntPtr.Zero) {
                throw new AllocationException($"RustBuffer.Alloc() returned null data pointer (size={size})");
            }
            return buffer;
        });
    }

    public static void Free(RustBuffer buffer) {
        _UniffiHelpers.RustCall((ref RustCallStatus status) => {
            _UniFFILib.ffi_nerdbank_zcash_rust_rustbuffer_free(buffer, ref status);
        });
    }

    public static BigEndianStream MemoryStream(IntPtr data, int length) {
        unsafe {
            return new BigEndianStream(new UnmanagedMemoryStream((byte*)data.ToPointer(), length));
        }
    }

    public BigEndianStream AsStream() {
        unsafe {
            return new BigEndianStream(new UnmanagedMemoryStream((byte*)data.ToPointer(), len));
        }
    }

    public BigEndianStream AsWriteableStream() {
        unsafe {
            return new BigEndianStream(new UnmanagedMemoryStream((byte*)data.ToPointer(), capacity, capacity, FileAccess.Write));
        }
    }
}

// This is a helper for safely passing byte references into the rust code.
// It's not actually used at the moment, because there aren't many things that you
// can take a direct pointer to managed memory, and if we're going to copy something
// then we might as well copy it into a `RustBuffer`. But it's here for API
// completeness.

[StructLayout(LayoutKind.Sequential)]
internal struct ForeignBytes {
    public int length;
    public IntPtr data;
}


// The FfiConverter interface handles converter types to and from the FFI
//
// All implementing objects should be public to support external types.  When a
// type is external we need to import it's FfiConverter.
internal abstract class FfiConverter<CsType, FfiType> {
    // Convert an FFI type to a C# type
    public abstract CsType Lift(FfiType value);

    // Convert C# type to an FFI type
    public abstract FfiType Lower(CsType value);

    // Read a C# type from a `ByteBuffer`
    public abstract CsType Read(BigEndianStream stream);

    // Calculate bytes to allocate when creating a `RustBuffer`
    //
    // This must return at least as many bytes as the write() function will
    // write. It can return more bytes than needed, for example when writing
    // Strings we can't know the exact bytes needed until we the UTF-8
    // encoding, so we pessimistically allocate the largest size possible (3
    // bytes per codepoint).  Allocating extra bytes is not really a big deal
    // because the `RustBuffer` is short-lived.
    public abstract int AllocationSize(CsType value);

    // Write a C# type to a `ByteBuffer`
    public abstract void Write(CsType value, BigEndianStream stream);

    // Lower a value into a `RustBuffer`
    //
    // This method lowers a value into a `RustBuffer` rather than the normal
    // FfiType.  It's used by the callback interface code.  Callback interface
    // returns are always serialized into a `RustBuffer` regardless of their
    // normal FFI type.
    public RustBuffer LowerIntoRustBuffer(CsType value) {
        var rbuf = RustBuffer.Alloc(AllocationSize(value));
        try {
            var stream = rbuf.AsWriteableStream();
            Write(value, stream);
            rbuf.len = Convert.ToInt32(stream.Position);
            return rbuf;
        } catch {
            RustBuffer.Free(rbuf);
            throw;
        }
    }

    // Lift a value from a `RustBuffer`.
    //
    // This here mostly because of the symmetry with `lowerIntoRustBuffer()`.
    // It's currently only used by the `FfiConverterRustBuffer` class below.
    protected CsType LiftFromRustBuffer(RustBuffer rbuf) {
        var stream = rbuf.AsStream();
        try {
           var item = Read(stream);
           if (stream.HasRemaining()) {
               throw new InternalException("junk remaining in buffer after lifting, something is very wrong!!");
           }
           return item;
        } finally {
            RustBuffer.Free(rbuf);
        }
    }
}

// FfiConverter that uses `RustBuffer` as the FfiType
internal abstract class FfiConverterRustBuffer<CsType>: FfiConverter<CsType, RustBuffer> {
    public override CsType Lift(RustBuffer value) {
        return LiftFromRustBuffer(value);
    }
    public override RustBuffer Lower(CsType value) {
        return LowerIntoRustBuffer(value);
    }
}


// A handful of classes and functions to support the generated data structures.
// This would be a good candidate for isolating in its own ffi-support lib.
// Error runtime.
[StructLayout(LayoutKind.Sequential)]
struct RustCallStatus {
    public sbyte code;
    public RustBuffer error_buf;

    public bool IsSuccess() {
        return code == 0;
    }

    public bool IsError() {
        return code == 1;
    }

    public bool IsPanic() {
        return code == 2;
    }
}

// Base class for all uniffi exceptions
public class UniffiException: Exception {
    public UniffiException(): base() {}
    public UniffiException(string message): base(message) {}
}

public class UndeclaredErrorException: UniffiException {
    public UndeclaredErrorException(string message): base(message) {}
}

public class PanicException: UniffiException {
    public PanicException(string message): base(message) {}
}

public class AllocationException: UniffiException {
    public AllocationException(string message): base(message) {}
}

public class InternalException: UniffiException {
    public InternalException(string message): base(message) {}
}

public class InvalidEnumException: InternalException {
    public InvalidEnumException(string message): base(message) {
    }
}

public class UniffiContractVersionException: UniffiException {
    public UniffiContractVersionException(string message): base(message) {
    }
}

public class UniffiContractChecksumException: UniffiException {
    public UniffiContractChecksumException(string message): base(message) {
    }
}

// Each top-level error class has a companion object that can lift the error from the call status's rust buffer
interface CallStatusErrorHandler<E> where E: Exception {
    E Lift(RustBuffer error_buf);
}

// CallStatusErrorHandler implementation for times when we don't expect a CALL_ERROR
class NullCallStatusErrorHandler: CallStatusErrorHandler<UniffiException> {
    public static NullCallStatusErrorHandler INSTANCE = new NullCallStatusErrorHandler();

    public UniffiException Lift(RustBuffer error_buf) {
        RustBuffer.Free(error_buf);
        return new UndeclaredErrorException("library has returned an error not declared in UNIFFI interface file");
    }
}

// Helpers for calling Rust
// In practice we usually need to be synchronized to call this safely, so it doesn't
// synchronize itself
class _UniffiHelpers {
    public delegate void RustCallAction(ref RustCallStatus status);
    public delegate U RustCallFunc<out U>(ref RustCallStatus status);

    // Call a rust function that returns a Result<>.  Pass in the Error class companion that corresponds to the Err
    public static U RustCallWithError<U, E>(CallStatusErrorHandler<E> errorHandler, RustCallFunc<U> callback)
        where E: UniffiException
    {
        var status = new RustCallStatus();
        var return_value = callback(ref status);
        if (status.IsSuccess()) {
            return return_value;
        } else if (status.IsError()) {
            throw errorHandler.Lift(status.error_buf);
        } else if (status.IsPanic()) {
            // when the rust code sees a panic, it tries to construct a rustbuffer
            // with the message.  but if that code panics, then it just sends back
            // an empty buffer.
            if (status.error_buf.len > 0) {
                throw new PanicException(FfiConverterString.INSTANCE.Lift(status.error_buf));
            } else {
                throw new PanicException("Rust panic");
            }
        } else {
            throw new InternalException($"Unknown rust call status: {status.code}");
        }
    }

    // Call a rust function that returns a Result<>.  Pass in the Error class companion that corresponds to the Err
    public static void RustCallWithError<E>(CallStatusErrorHandler<E> errorHandler, RustCallAction callback)
        where E: UniffiException
    {
        _UniffiHelpers.RustCallWithError(errorHandler, (ref RustCallStatus status) => {
            callback(ref status);
            return 0;
        });
    }

    // Call a rust function that returns a plain value
    public static U RustCall<U>(RustCallFunc<U> callback) {
        return _UniffiHelpers.RustCallWithError(NullCallStatusErrorHandler.INSTANCE, callback);
    }

    // Call a rust function that returns a plain value
    public static void RustCall(RustCallAction callback) {
        _UniffiHelpers.RustCall((ref RustCallStatus status) => {
            callback(ref status);
            return 0;
        });
    }
}


// Big endian streams are not yet available in dotnet :'(
// https://github.com/dotnet/runtime/issues/26904

class StreamUnderflowException: Exception {
    public StreamUnderflowException() {
    }
}

class BigEndianStream {
    Stream stream;
    public BigEndianStream(Stream stream) {
        this.stream = stream;
    }

    public bool HasRemaining() {
        return (stream.Length - stream.Position) > 0;
    }

    public long Position {
        get => stream.Position;
        set => stream.Position = value;
    }

    public void WriteBytes(byte[] value) {
        stream.Write(value, 0, value.Length);
    }

    public void WriteByte(byte value) {
        stream.WriteByte(value);
    }

    public void WriteUShort(ushort value) {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    public void WriteUInt(uint value) {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    public void WriteULong(ulong value) {
        WriteUInt((uint)(value >> 32));
        WriteUInt((uint)value);
    }

    public void WriteSByte(sbyte value) {
        stream.WriteByte((byte)value);
    }

    public void WriteShort(short value) {
        WriteUShort((ushort)value);
    }

    public void WriteInt(int value) {
        WriteUInt((uint)value);
    }

    public void WriteFloat(float value) {
        WriteInt(BitConverter.SingleToInt32Bits(value));
    }

    public void WriteLong(long value) {
        WriteULong((ulong)value);
    }

    public void WriteDouble(double value) {
        WriteLong(BitConverter.DoubleToInt64Bits(value));
    }

    public byte[] ReadBytes(int length) {
        CheckRemaining(length);
        byte[] result = new byte[length];
        stream.Read(result, 0, length);
        return result;
    }

    public byte ReadByte() {
        CheckRemaining(1);
        return Convert.ToByte(stream.ReadByte());
    }

    public ushort ReadUShort() {
        CheckRemaining(2);
        return (ushort)(stream.ReadByte() << 8 | stream.ReadByte());
    }

    public uint ReadUInt() {
        CheckRemaining(4);
        return (uint)(stream.ReadByte() << 24
            | stream.ReadByte() << 16
            | stream.ReadByte() << 8
            | stream.ReadByte());
    }

    public ulong ReadULong() {
        return (ulong)ReadUInt() << 32 | (ulong)ReadUInt();
    }

    public sbyte ReadSByte() {
        return (sbyte)ReadByte();
    }

    public short ReadShort() {
        return (short)ReadUShort();
    }

    public int ReadInt() {
        return (int)ReadUInt();
    }

    public float ReadFloat() {
        return BitConverter.Int32BitsToSingle(ReadInt());
    }

    public long ReadLong() {
        return (long)ReadULong();
    }

    public double ReadDouble() {
        return BitConverter.Int64BitsToDouble(ReadLong());
    }

    private void CheckRemaining(int length) {
        if (stream.Length - stream.Position < length) {
            throw new StreamUnderflowException();
        }
    }
}

// Contains loading, initialization code,
// and the FFI Function declarations in a com.sun.jna.Library.


// This is an implementation detail which will be called internally by the public API.
static class _UniFFILib {
    static _UniFFILib() {
        _UniFFILib.uniffiCheckContractApiVersion();
        _UniFFILib.uniffiCheckApiChecksums();
        
        }

    [DllImport("nerdbank_zcash_rust")]
    public static extern sbyte uniffi_nerdbank_zcash_rust_fn_func_lightwallet_disconnect_server(RustBuffer @uri,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern ulong uniffi_nerdbank_zcash_rust_fn_func_lightwallet_get_block_height(RustBuffer @uri,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void uniffi_nerdbank_zcash_rust_fn_func_lightwallet_init(RustBuffer @walletDir,RustBuffer @network,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer uniffi_nerdbank_zcash_rust_fn_func_lightwallet_sync(RustBuffer @uri,RustBuffer @walletDir,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer ffi_nerdbank_zcash_rust_rustbuffer_alloc(int @size,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer ffi_nerdbank_zcash_rust_rustbuffer_from_bytes(ForeignBytes @bytes,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rustbuffer_free(RustBuffer @buf,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer ffi_nerdbank_zcash_rust_rustbuffer_reserve(RustBuffer @buf,int @additional,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_continuation_callback_set(IntPtr @callback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_u8(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_u8(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_u8(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern byte ffi_nerdbank_zcash_rust_rust_future_complete_u8(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_i8(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_i8(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_i8(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern sbyte ffi_nerdbank_zcash_rust_rust_future_complete_i8(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_u16(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_u16(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_u16(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern ushort ffi_nerdbank_zcash_rust_rust_future_complete_u16(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_i16(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_i16(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_i16(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern short ffi_nerdbank_zcash_rust_rust_future_complete_i16(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_u32(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_u32(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_u32(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern uint ffi_nerdbank_zcash_rust_rust_future_complete_u32(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_i32(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_i32(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_i32(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern int ffi_nerdbank_zcash_rust_rust_future_complete_i32(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_u64(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_u64(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_u64(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern ulong ffi_nerdbank_zcash_rust_rust_future_complete_u64(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_i64(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_i64(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_i64(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern long ffi_nerdbank_zcash_rust_rust_future_complete_i64(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_f32(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_f32(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_f32(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern float ffi_nerdbank_zcash_rust_rust_future_complete_f32(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_f64(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_f64(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_f64(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern double ffi_nerdbank_zcash_rust_rust_future_complete_f64(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_pointer(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_pointer(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_pointer(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern SafeHandle ffi_nerdbank_zcash_rust_rust_future_complete_pointer(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_rust_buffer(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_rust_buffer(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_rust_buffer(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer ffi_nerdbank_zcash_rust_rust_future_complete_rust_buffer(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_poll_void(IntPtr @handle,IntPtr @uniffiCallback
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_cancel_void(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_free_void(IntPtr @handle
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_nerdbank_zcash_rust_rust_future_complete_void(IntPtr @handle,ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern ushort uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_disconnect_server(
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern ushort uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_get_block_height(
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern ushort uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_init(
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern ushort uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_sync(
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern uint ffi_nerdbank_zcash_rust_uniffi_contract_version(
    );

    

    static void uniffiCheckContractApiVersion() {
        var scaffolding_contract_version = _UniFFILib.ffi_nerdbank_zcash_rust_uniffi_contract_version();
        if (24 != scaffolding_contract_version) {
            throw new UniffiContractVersionException($"uniffi.LightWallet: uniffi bindings expected version `24`, library returned `{scaffolding_contract_version}`");
        }
    }

    static void uniffiCheckApiChecksums() {
        {
            var checksum = _UniFFILib.uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_disconnect_server();
            if (checksum != 4027) {
                throw new UniffiContractChecksumException($"uniffi.LightWallet: uniffi bindings expected function `uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_disconnect_server` checksum `4027`, library returned `{checksum}`");
            }
        }
        {
            var checksum = _UniFFILib.uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_get_block_height();
            if (checksum != 62832) {
                throw new UniffiContractChecksumException($"uniffi.LightWallet: uniffi bindings expected function `uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_get_block_height` checksum `62832`, library returned `{checksum}`");
            }
        }
        {
            var checksum = _UniFFILib.uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_init();
            if (checksum != 19878) {
                throw new UniffiContractChecksumException($"uniffi.LightWallet: uniffi bindings expected function `uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_init` checksum `19878`, library returned `{checksum}`");
            }
        }
        {
            var checksum = _UniFFILib.uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_sync();
            if (checksum != 16995) {
                throw new UniffiContractChecksumException($"uniffi.LightWallet: uniffi bindings expected function `uniffi_nerdbank_zcash_rust_checksum_func_lightwallet_sync` checksum `16995`, library returned `{checksum}`");
            }
        }
    }
}

// Public interface members begin here.

#pragma warning disable 8625




class FfiConverterUInt32: FfiConverter<uint, uint> {
    public static FfiConverterUInt32 INSTANCE = new FfiConverterUInt32();

    public override uint Lift(uint value) {
        return value;
    }

    public override uint Read(BigEndianStream stream) {
        return stream.ReadUInt();
    }

    public override uint Lower(uint value) {
        return value;
    }

    public override int AllocationSize(uint value) {
        return 4;
    }

    public override void Write(uint value, BigEndianStream stream) {
        stream.WriteUInt(value);
    }
}



class FfiConverterUInt64: FfiConverter<ulong, ulong> {
    public static FfiConverterUInt64 INSTANCE = new FfiConverterUInt64();

    public override ulong Lift(ulong value) {
        return value;
    }

    public override ulong Read(BigEndianStream stream) {
        return stream.ReadULong();
    }

    public override ulong Lower(ulong value) {
        return value;
    }

    public override int AllocationSize(ulong value) {
        return 8;
    }

    public override void Write(ulong value, BigEndianStream stream) {
        stream.WriteULong(value);
    }
}



class FfiConverterDouble: FfiConverter<double, double> {
    public static FfiConverterDouble INSTANCE = new FfiConverterDouble();

    public override double Lift(double value) {
        return value;
    }

    public override double Read(BigEndianStream stream) {
        return stream.ReadDouble();
    }

    public override double Lower(double value) {
        return value;
    }

    public override int AllocationSize(double value) {
        return 8;
    }

    public override void Write(double value, BigEndianStream stream) {
        stream.WriteDouble(value);
    }
}



class FfiConverterBoolean: FfiConverter<bool, sbyte> {
    public static FfiConverterBoolean INSTANCE = new FfiConverterBoolean();

    public override bool Lift(sbyte value) {
        return value != 0;
    }

    public override bool Read(BigEndianStream stream) {
        return Lift(stream.ReadSByte());
    }

    public override sbyte Lower(bool value) {
        return value ? (sbyte)1 : (sbyte)0;
    }

    public override int AllocationSize(bool value) {
        return (sbyte)1;
    }

    public override void Write(bool value, BigEndianStream stream) {
        stream.WriteSByte(Lower(value));
    }
}



class FfiConverterString: FfiConverter<string, RustBuffer> {
    public static FfiConverterString INSTANCE = new FfiConverterString();

    // Note: we don't inherit from FfiConverterRustBuffer, because we use a
    // special encoding when lowering/lifting.  We can use `RustBuffer.len` to
    // store our length and avoid writing it out to the buffer.
    public override string Lift(RustBuffer value) {
        try {
            var bytes = value.AsStream().ReadBytes(value.len);
            return System.Text.Encoding.UTF8.GetString(bytes);
        } finally {
            RustBuffer.Free(value);
        }
    }

    public override string Read(BigEndianStream stream) {
        var length = stream.ReadInt();
        var bytes = stream.ReadBytes(length);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public override RustBuffer Lower(string value) {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var rbuf = RustBuffer.Alloc(bytes.Length);
        rbuf.AsWriteableStream().WriteBytes(bytes);
        return rbuf;
    }

    // TODO(CS)
    // We aren't sure exactly how many bytes our string will be once it's UTF-8
    // encoded.  Allocate 3 bytes per unicode codepoint which will always be
    // enough.
    public override int AllocationSize(string value) {
        const int sizeForLength = 4;
        var sizeForString = value.Length * 3;
        return sizeForLength + sizeForString;
    }

    public override void Write(string value, BigEndianStream stream) {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        stream.WriteInt(bytes.Length);
        stream.WriteBytes(bytes);
    }
}




class FfiConverterByteArray: FfiConverterRustBuffer<byte[]> {
    public static FfiConverterByteArray INSTANCE = new FfiConverterByteArray();

    public override byte[] Read(BigEndianStream stream) {
        var length = stream.ReadInt();
        return stream.ReadBytes(length);
    }

    public override int AllocationSize(byte[] value) {
        return 4 + value.Length;
    }

    public override void Write(byte[] value, BigEndianStream stream) {
        stream.WriteInt(value.Length);
        stream.WriteBytes(value);
    }
}



public record BirthdayHeights (
    ulong @originalBirthdayHeight, 
    ulong @birthdayHeight, 
    ulong? @rebirthHeight
) {
}

class FfiConverterTypeBirthdayHeights: FfiConverterRustBuffer<BirthdayHeights> {
    public static FfiConverterTypeBirthdayHeights INSTANCE = new FfiConverterTypeBirthdayHeights();

    public override BirthdayHeights Read(BigEndianStream stream) {
        return new BirthdayHeights(
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterOptionalUInt64.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(BirthdayHeights value) {
        return
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@originalBirthdayHeight) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@birthdayHeight) +
            FfiConverterOptionalUInt64.INSTANCE.AllocationSize(value.@rebirthHeight);
    }

    public override void Write(BirthdayHeights value, BigEndianStream stream) {
            FfiConverterUInt64.INSTANCE.Write(value.@originalBirthdayHeight, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@birthdayHeight, stream);
            FfiConverterOptionalUInt64.INSTANCE.Write(value.@rebirthHeight, stream);
    }
}



public record OrchardNote (
    ulong @value, 
    byte[] @memo, 
    bool @isChange, 
    byte[] @recipient
) {
}

class FfiConverterTypeOrchardNote: FfiConverterRustBuffer<OrchardNote> {
    public static FfiConverterTypeOrchardNote INSTANCE = new FfiConverterTypeOrchardNote();

    public override OrchardNote Read(BigEndianStream stream) {
        return new OrchardNote(
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterByteArray.INSTANCE.Read(stream),
            FfiConverterBoolean.INSTANCE.Read(stream),
            FfiConverterByteArray.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(OrchardNote value) {
        return
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@value) +
            FfiConverterByteArray.INSTANCE.AllocationSize(value.@memo) +
            FfiConverterBoolean.INSTANCE.AllocationSize(value.@isChange) +
            FfiConverterByteArray.INSTANCE.AllocationSize(value.@recipient);
    }

    public override void Write(OrchardNote value, BigEndianStream stream) {
            FfiConverterUInt64.INSTANCE.Write(value.@value, stream);
            FfiConverterByteArray.INSTANCE.Write(value.@memo, stream);
            FfiConverterBoolean.INSTANCE.Write(value.@isChange, stream);
            FfiConverterByteArray.INSTANCE.Write(value.@recipient, stream);
    }
}



public record SaplingNote (
    ulong @value, 
    byte[] @memo, 
    bool @isChange, 
    byte[] @recipient
) {
}

class FfiConverterTypeSaplingNote: FfiConverterRustBuffer<SaplingNote> {
    public static FfiConverterTypeSaplingNote INSTANCE = new FfiConverterTypeSaplingNote();

    public override SaplingNote Read(BigEndianStream stream) {
        return new SaplingNote(
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterByteArray.INSTANCE.Read(stream),
            FfiConverterBoolean.INSTANCE.Read(stream),
            FfiConverterByteArray.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(SaplingNote value) {
        return
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@value) +
            FfiConverterByteArray.INSTANCE.AllocationSize(value.@memo) +
            FfiConverterBoolean.INSTANCE.AllocationSize(value.@isChange) +
            FfiConverterByteArray.INSTANCE.AllocationSize(value.@recipient);
    }

    public override void Write(SaplingNote value, BigEndianStream stream) {
            FfiConverterUInt64.INSTANCE.Write(value.@value, stream);
            FfiConverterByteArray.INSTANCE.Write(value.@memo, stream);
            FfiConverterBoolean.INSTANCE.Write(value.@isChange, stream);
            FfiConverterByteArray.INSTANCE.Write(value.@recipient, stream);
    }
}



public record SyncResult (
    ulong @tipHeight
) {
}

class FfiConverterTypeSyncResult: FfiConverterRustBuffer<SyncResult> {
    public static FfiConverterTypeSyncResult INSTANCE = new FfiConverterTypeSyncResult();

    public override SyncResult Read(BigEndianStream stream) {
        return new SyncResult(
            FfiConverterUInt64.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(SyncResult value) {
        return
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@tipHeight);
    }

    public override void Write(SyncResult value, BigEndianStream stream) {
            FfiConverterUInt64.INSTANCE.Write(value.@tipHeight, stream);
    }
}



public record Transaction (
    String @txid, 
    ulong @datetime, 
    uint @blockHeight, 
    bool @isIncoming, 
    ulong @spent, 
    ulong @received, 
    double? @price, 
    bool @unconfirmed, 
    List<TransactionSendDetail> @sends, 
    List<SaplingNote> @saplingNotes, 
    List<OrchardNote> @orchardNotes
) {
}

class FfiConverterTypeTransaction: FfiConverterRustBuffer<Transaction> {
    public static FfiConverterTypeTransaction INSTANCE = new FfiConverterTypeTransaction();

    public override Transaction Read(BigEndianStream stream) {
        return new Transaction(
            FfiConverterString.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterUInt32.INSTANCE.Read(stream),
            FfiConverterBoolean.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterOptionalDouble.INSTANCE.Read(stream),
            FfiConverterBoolean.INSTANCE.Read(stream),
            FfiConverterSequenceTypeTransactionSendDetail.INSTANCE.Read(stream),
            FfiConverterSequenceTypeSaplingNote.INSTANCE.Read(stream),
            FfiConverterSequenceTypeOrchardNote.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(Transaction value) {
        return
            FfiConverterString.INSTANCE.AllocationSize(value.@txid) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@datetime) +
            FfiConverterUInt32.INSTANCE.AllocationSize(value.@blockHeight) +
            FfiConverterBoolean.INSTANCE.AllocationSize(value.@isIncoming) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@spent) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@received) +
            FfiConverterOptionalDouble.INSTANCE.AllocationSize(value.@price) +
            FfiConverterBoolean.INSTANCE.AllocationSize(value.@unconfirmed) +
            FfiConverterSequenceTypeTransactionSendDetail.INSTANCE.AllocationSize(value.@sends) +
            FfiConverterSequenceTypeSaplingNote.INSTANCE.AllocationSize(value.@saplingNotes) +
            FfiConverterSequenceTypeOrchardNote.INSTANCE.AllocationSize(value.@orchardNotes);
    }

    public override void Write(Transaction value, BigEndianStream stream) {
            FfiConverterString.INSTANCE.Write(value.@txid, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@datetime, stream);
            FfiConverterUInt32.INSTANCE.Write(value.@blockHeight, stream);
            FfiConverterBoolean.INSTANCE.Write(value.@isIncoming, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@spent, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@received, stream);
            FfiConverterOptionalDouble.INSTANCE.Write(value.@price, stream);
            FfiConverterBoolean.INSTANCE.Write(value.@unconfirmed, stream);
            FfiConverterSequenceTypeTransactionSendDetail.INSTANCE.Write(value.@sends, stream);
            FfiConverterSequenceTypeSaplingNote.INSTANCE.Write(value.@saplingNotes, stream);
            FfiConverterSequenceTypeOrchardNote.INSTANCE.Write(value.@orchardNotes, stream);
    }
}



public record TransactionSendDetail (
    String @toAddress, 
    ulong @value, 
    byte[] @memo
) {
}

class FfiConverterTypeTransactionSendDetail: FfiConverterRustBuffer<TransactionSendDetail> {
    public static FfiConverterTypeTransactionSendDetail INSTANCE = new FfiConverterTypeTransactionSendDetail();

    public override TransactionSendDetail Read(BigEndianStream stream) {
        return new TransactionSendDetail(
            FfiConverterString.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterByteArray.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(TransactionSendDetail value) {
        return
            FfiConverterString.INSTANCE.AllocationSize(value.@toAddress) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@value) +
            FfiConverterByteArray.INSTANCE.AllocationSize(value.@memo);
    }

    public override void Write(TransactionSendDetail value, BigEndianStream stream) {
            FfiConverterString.INSTANCE.Write(value.@toAddress, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@value, stream);
            FfiConverterByteArray.INSTANCE.Write(value.@memo, stream);
    }
}



public record UserBalances (
    ulong @spendable, 
    ulong @immatureChange, 
    ulong @minimumFees, 
    ulong @immatureIncome, 
    ulong @dust, 
    ulong @incoming, 
    ulong @incomingDust
) {
}

class FfiConverterTypeUserBalances: FfiConverterRustBuffer<UserBalances> {
    public static FfiConverterTypeUserBalances INSTANCE = new FfiConverterTypeUserBalances();

    public override UserBalances Read(BigEndianStream stream) {
        return new UserBalances(
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(UserBalances value) {
        return
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@spendable) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@immatureChange) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@minimumFees) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@immatureIncome) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@dust) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@incoming) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@incomingDust);
    }

    public override void Write(UserBalances value, BigEndianStream stream) {
            FfiConverterUInt64.INSTANCE.Write(value.@spendable, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@immatureChange, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@minimumFees, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@immatureIncome, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@dust, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@incoming, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@incomingDust, stream);
    }
}



public record WalletInfo (
    String? @ufvk, 
    byte[]? @unifiedSpendingKey, 
    ulong @birthdayHeight
) {
}

class FfiConverterTypeWalletInfo: FfiConverterRustBuffer<WalletInfo> {
    public static FfiConverterTypeWalletInfo INSTANCE = new FfiConverterTypeWalletInfo();

    public override WalletInfo Read(BigEndianStream stream) {
        return new WalletInfo(
            FfiConverterOptionalString.INSTANCE.Read(stream),
            FfiConverterOptionalByteArray.INSTANCE.Read(stream),
            FfiConverterUInt64.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(WalletInfo value) {
        return
            FfiConverterOptionalString.INSTANCE.AllocationSize(value.@ufvk) +
            FfiConverterOptionalByteArray.INSTANCE.AllocationSize(value.@unifiedSpendingKey) +
            FfiConverterUInt64.INSTANCE.AllocationSize(value.@birthdayHeight);
    }

    public override void Write(WalletInfo value, BigEndianStream stream) {
            FfiConverterOptionalString.INSTANCE.Write(value.@ufvk, stream);
            FfiConverterOptionalByteArray.INSTANCE.Write(value.@unifiedSpendingKey, stream);
            FfiConverterUInt64.INSTANCE.Write(value.@birthdayHeight, stream);
    }
}





public enum ChainType: int {
    
    Testnet,
    Mainnet
}

class FfiConverterTypeChainType: FfiConverterRustBuffer<ChainType> {
    public static FfiConverterTypeChainType INSTANCE = new FfiConverterTypeChainType();

    public override ChainType Read(BigEndianStream stream) {
        var value = stream.ReadInt() - 1;
        if (Enum.IsDefined(typeof(ChainType), value)) {
            return (ChainType)value;
        } else {
            throw new InternalException(String.Format("invalid enum value '{0}' in FfiConverterTypeChainType.Read()", value));
        }
    }

    public override int AllocationSize(ChainType value) {
        return 4;
    }

    public override void Write(ChainType value, BigEndianStream stream) {
        stream.WriteInt((int)value + 1);
    }
}







public class LightWalletException: UniffiException {
    LightWalletException(string message): base(message) {}

    // Each variant is a nested class
    // Flat enums carries a string error message, so no special implementation is necessary.
    
    public class InvalidUri: LightWalletException {
        public InvalidUri(string message): base(message) {}
    }
    
    public class Other: LightWalletException {
        public Other(string message): base(message) {}
    }
    
}

class FfiConverterTypeLightWalletException : FfiConverterRustBuffer<LightWalletException>, CallStatusErrorHandler<LightWalletException> {
    public static FfiConverterTypeLightWalletException INSTANCE = new FfiConverterTypeLightWalletException();

    public override LightWalletException Read(BigEndianStream stream) {
        var value = stream.ReadInt();
        switch (value) {
            case 1: return new LightWalletException.InvalidUri(FfiConverterString.INSTANCE.Read(stream));
            case 2: return new LightWalletException.Other(FfiConverterString.INSTANCE.Read(stream));
            default:
                throw new InternalException(String.Format("invalid error value '{0}' in FfiConverterTypeLightWalletException.Read()", value));
        }
    }

    public override int AllocationSize(LightWalletException value) {
        return 4 + FfiConverterString.INSTANCE.AllocationSize(value.Message);
    }

    public override void Write(LightWalletException value, BigEndianStream stream) {
        switch (value) {
            case LightWalletException.InvalidUri:
                stream.WriteInt(1);
                break;
            case LightWalletException.Other:
                stream.WriteInt(2);
                break;
            default:
                throw new InternalException(String.Format("invalid error value '{0}' in FfiConverterTypeLightWalletException.Write()", value));
        }
    }
}




class FfiConverterOptionalUInt64: FfiConverterRustBuffer<ulong?> {
    public static FfiConverterOptionalUInt64 INSTANCE = new FfiConverterOptionalUInt64();

    public override ulong? Read(BigEndianStream stream) {
        if (stream.ReadByte() == 0) {
            return null;
        }
        return FfiConverterUInt64.INSTANCE.Read(stream);
    }

    public override int AllocationSize(ulong? value) {
        if (value == null) {
            return 1;
        } else {
            return 1 + FfiConverterUInt64.INSTANCE.AllocationSize((ulong)value);
        }
    }

    public override void Write(ulong? value, BigEndianStream stream) {
        if (value == null) {
            stream.WriteByte(0);
        } else {
            stream.WriteByte(1);
            FfiConverterUInt64.INSTANCE.Write((ulong)value, stream);
        }
    }
}




class FfiConverterOptionalDouble: FfiConverterRustBuffer<double?> {
    public static FfiConverterOptionalDouble INSTANCE = new FfiConverterOptionalDouble();

    public override double? Read(BigEndianStream stream) {
        if (stream.ReadByte() == 0) {
            return null;
        }
        return FfiConverterDouble.INSTANCE.Read(stream);
    }

    public override int AllocationSize(double? value) {
        if (value == null) {
            return 1;
        } else {
            return 1 + FfiConverterDouble.INSTANCE.AllocationSize((double)value);
        }
    }

    public override void Write(double? value, BigEndianStream stream) {
        if (value == null) {
            stream.WriteByte(0);
        } else {
            stream.WriteByte(1);
            FfiConverterDouble.INSTANCE.Write((double)value, stream);
        }
    }
}




class FfiConverterOptionalString: FfiConverterRustBuffer<String?> {
    public static FfiConverterOptionalString INSTANCE = new FfiConverterOptionalString();

    public override String? Read(BigEndianStream stream) {
        if (stream.ReadByte() == 0) {
            return null;
        }
        return FfiConverterString.INSTANCE.Read(stream);
    }

    public override int AllocationSize(String? value) {
        if (value == null) {
            return 1;
        } else {
            return 1 + FfiConverterString.INSTANCE.AllocationSize((String)value);
        }
    }

    public override void Write(String? value, BigEndianStream stream) {
        if (value == null) {
            stream.WriteByte(0);
        } else {
            stream.WriteByte(1);
            FfiConverterString.INSTANCE.Write((String)value, stream);
        }
    }
}




class FfiConverterOptionalByteArray: FfiConverterRustBuffer<byte[]?> {
    public static FfiConverterOptionalByteArray INSTANCE = new FfiConverterOptionalByteArray();

    public override byte[]? Read(BigEndianStream stream) {
        if (stream.ReadByte() == 0) {
            return null;
        }
        return FfiConverterByteArray.INSTANCE.Read(stream);
    }

    public override int AllocationSize(byte[]? value) {
        if (value == null) {
            return 1;
        } else {
            return 1 + FfiConverterByteArray.INSTANCE.AllocationSize((byte[])value);
        }
    }

    public override void Write(byte[]? value, BigEndianStream stream) {
        if (value == null) {
            stream.WriteByte(0);
        } else {
            stream.WriteByte(1);
            FfiConverterByteArray.INSTANCE.Write((byte[])value, stream);
        }
    }
}




class FfiConverterSequenceTypeOrchardNote: FfiConverterRustBuffer<List<OrchardNote>> {
    public static FfiConverterSequenceTypeOrchardNote INSTANCE = new FfiConverterSequenceTypeOrchardNote();

    public override List<OrchardNote> Read(BigEndianStream stream) {
        var length = stream.ReadInt();
        var result = new List<OrchardNote>(length);
        for (int i = 0; i < length; i++) {
            result.Add(FfiConverterTypeOrchardNote.INSTANCE.Read(stream));
        }
        return result;
    }

    public override int AllocationSize(List<OrchardNote> value) {
        var sizeForLength = 4;

        // details/1-empty-list-as-default-method-parameter.md
        if (value == null) {
            return sizeForLength;
        }

        var sizeForItems = value.Select(item => FfiConverterTypeOrchardNote.INSTANCE.AllocationSize(item)).Sum();
        return sizeForLength + sizeForItems;
    }

    public override void Write(List<OrchardNote> value, BigEndianStream stream) {
        // details/1-empty-list-as-default-method-parameter.md
        if (value == null) {
            stream.WriteInt(0);
            return;
        }

        stream.WriteInt(value.Count);
        value.ForEach(item => FfiConverterTypeOrchardNote.INSTANCE.Write(item, stream));
    }
}




class FfiConverterSequenceTypeSaplingNote: FfiConverterRustBuffer<List<SaplingNote>> {
    public static FfiConverterSequenceTypeSaplingNote INSTANCE = new FfiConverterSequenceTypeSaplingNote();

    public override List<SaplingNote> Read(BigEndianStream stream) {
        var length = stream.ReadInt();
        var result = new List<SaplingNote>(length);
        for (int i = 0; i < length; i++) {
            result.Add(FfiConverterTypeSaplingNote.INSTANCE.Read(stream));
        }
        return result;
    }

    public override int AllocationSize(List<SaplingNote> value) {
        var sizeForLength = 4;

        // details/1-empty-list-as-default-method-parameter.md
        if (value == null) {
            return sizeForLength;
        }

        var sizeForItems = value.Select(item => FfiConverterTypeSaplingNote.INSTANCE.AllocationSize(item)).Sum();
        return sizeForLength + sizeForItems;
    }

    public override void Write(List<SaplingNote> value, BigEndianStream stream) {
        // details/1-empty-list-as-default-method-parameter.md
        if (value == null) {
            stream.WriteInt(0);
            return;
        }

        stream.WriteInt(value.Count);
        value.ForEach(item => FfiConverterTypeSaplingNote.INSTANCE.Write(item, stream));
    }
}




class FfiConverterSequenceTypeTransactionSendDetail: FfiConverterRustBuffer<List<TransactionSendDetail>> {
    public static FfiConverterSequenceTypeTransactionSendDetail INSTANCE = new FfiConverterSequenceTypeTransactionSendDetail();

    public override List<TransactionSendDetail> Read(BigEndianStream stream) {
        var length = stream.ReadInt();
        var result = new List<TransactionSendDetail>(length);
        for (int i = 0; i < length; i++) {
            result.Add(FfiConverterTypeTransactionSendDetail.INSTANCE.Read(stream));
        }
        return result;
    }

    public override int AllocationSize(List<TransactionSendDetail> value) {
        var sizeForLength = 4;

        // details/1-empty-list-as-default-method-parameter.md
        if (value == null) {
            return sizeForLength;
        }

        var sizeForItems = value.Select(item => FfiConverterTypeTransactionSendDetail.INSTANCE.AllocationSize(item)).Sum();
        return sizeForLength + sizeForItems;
    }

    public override void Write(List<TransactionSendDetail> value, BigEndianStream stream) {
        // details/1-empty-list-as-default-method-parameter.md
        if (value == null) {
            stream.WriteInt(0);
            return;
        }

        stream.WriteInt(value.Count);
        value.ForEach(item => FfiConverterTypeTransactionSendDetail.INSTANCE.Write(item, stream));
    }
}
#pragma warning restore 8625
public static class LightWalletMethods {
    /// <exception cref="LightWalletException"></exception>
    public static bool LightwalletDisconnectServer(String @uri) {
        return FfiConverterBoolean.INSTANCE.Lift(
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletException.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.uniffi_nerdbank_zcash_rust_fn_func_lightwallet_disconnect_server(FfiConverterString.INSTANCE.Lower(@uri), ref _status)
));
    }

    /// <exception cref="LightWalletException"></exception>
    public static ulong LightwalletGetBlockHeight(String @uri) {
        return FfiConverterUInt64.INSTANCE.Lift(
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletException.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.uniffi_nerdbank_zcash_rust_fn_func_lightwallet_get_block_height(FfiConverterString.INSTANCE.Lower(@uri), ref _status)
));
    }

    /// <exception cref="LightWalletException"></exception>
    public static void LightwalletInit(String @walletDir, ChainType @network) {
        
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletException.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.uniffi_nerdbank_zcash_rust_fn_func_lightwallet_init(FfiConverterString.INSTANCE.Lower(@walletDir), FfiConverterTypeChainType.INSTANCE.Lower(@network), ref _status)
);
    }

    /// <exception cref="LightWalletException"></exception>
    public static SyncResult LightwalletSync(String @uri, String @walletDir) {
        return FfiConverterTypeSyncResult.INSTANCE.Lift(
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletException.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.uniffi_nerdbank_zcash_rust_fn_func_lightwallet_sync(FfiConverterString.INSTANCE.Lower(@uri), FfiConverterString.INSTANCE.Lower(@walletDir), ref _status)
));
    }

}

