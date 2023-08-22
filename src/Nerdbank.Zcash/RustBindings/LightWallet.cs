

using System.IO;
using System.Runtime.InteropServices;
using System;
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
            var buffer = _UniFFILib.ffi_LightWallet_a7b0_rustbuffer_alloc(size, ref status);
            if (buffer.data == IntPtr.Zero) {
                throw new AllocationException($"RustBuffer.Alloc() returned null data pointer (size={size})");
            }
            return buffer;
        });
    }

    public static void Free(RustBuffer buffer) {
        _UniffiHelpers.RustCall((ref RustCallStatus status) => {
            _UniFFILib.ffi_LightWallet_a7b0_rustbuffer_free(buffer, ref status);
        });
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
    public int code;
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
        
        }

    [DllImport("nerdbank_zcash_rust")]
    public static extern ulong LightWallet_a7b0_lightwallet_get_block_height(RustBuffer @serverUri,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern ulong LightWallet_a7b0_lightwallet_initialize(RustBuffer @config,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern sbyte LightWallet_a7b0_lightwallet_deinitialize(ulong @handle,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer LightWallet_a7b0_lightwallet_sync(ulong @handle,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void LightWallet_a7b0_lightwallet_sync_interrupt(ulong @handle,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer LightWallet_a7b0_lightwallet_sync_status(ulong @handle,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern ulong LightWallet_a7b0_lightwallet_get_birthday_height(ulong @handle,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer ffi_LightWallet_a7b0_rustbuffer_alloc(int @size,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer ffi_LightWallet_a7b0_rustbuffer_from_bytes(ForeignBytes @bytes,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern void ffi_LightWallet_a7b0_rustbuffer_free(RustBuffer @buf,
    ref RustCallStatus _uniffi_out_err
    );

    [DllImport("nerdbank_zcash_rust")]
    public static extern RustBuffer ffi_LightWallet_a7b0_rustbuffer_reserve(RustBuffer @buf,int @additional,
    ref RustCallStatus _uniffi_out_err
    );

    
}

// Public interface members begin here.

#pragma warning disable 8625




class FfiConverterULong: FfiConverter<ulong, ulong> {
    public static FfiConverterULong INSTANCE = new FfiConverterULong();

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



public record Config (
    String @serverUri, 
    Network @network, 
    String @dataDir, 
    String @walletName, 
    String @logName, 
    Boolean @monitorMempool
) {
}

class FfiConverterTypeConfig: FfiConverterRustBuffer<Config> {
    public static FfiConverterTypeConfig INSTANCE = new FfiConverterTypeConfig();

    public override Config Read(BigEndianStream stream) {
        return new Config(
            FfiConverterString.INSTANCE.Read(stream),
            FfiConverterTypeNetwork.INSTANCE.Read(stream),
            FfiConverterString.INSTANCE.Read(stream),
            FfiConverterString.INSTANCE.Read(stream),
            FfiConverterString.INSTANCE.Read(stream),
            FfiConverterBoolean.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(Config value) {
        return
            FfiConverterString.INSTANCE.AllocationSize(value.@serverUri) +
            FfiConverterTypeNetwork.INSTANCE.AllocationSize(value.@network) +
            FfiConverterString.INSTANCE.AllocationSize(value.@dataDir) +
            FfiConverterString.INSTANCE.AllocationSize(value.@walletName) +
            FfiConverterString.INSTANCE.AllocationSize(value.@logName) +
            FfiConverterBoolean.INSTANCE.AllocationSize(value.@monitorMempool);
    }

    public override void Write(Config value, BigEndianStream stream) {
            FfiConverterString.INSTANCE.Write(value.@serverUri, stream);
            FfiConverterTypeNetwork.INSTANCE.Write(value.@network, stream);
            FfiConverterString.INSTANCE.Write(value.@dataDir, stream);
            FfiConverterString.INSTANCE.Write(value.@walletName, stream);
            FfiConverterString.INSTANCE.Write(value.@logName, stream);
            FfiConverterBoolean.INSTANCE.Write(value.@monitorMempool, stream);
    }
}



public record SyncStatus (
    Boolean @inProgress, 
    String? @lastError, 
    UInt64 @syncId, 
    UInt64 @startBlock, 
    UInt64 @endBlock, 
    UInt64 @blocksDone, 
    UInt64 @trialDecDone, 
    UInt64 @txnScanDone, 
    UInt64 @blocksTotal, 
    UInt64 @batchNum, 
    UInt64 @batchTotal
) {
}

class FfiConverterTypeSyncStatus: FfiConverterRustBuffer<SyncStatus> {
    public static FfiConverterTypeSyncStatus INSTANCE = new FfiConverterTypeSyncStatus();

    public override SyncStatus Read(BigEndianStream stream) {
        return new SyncStatus(
            FfiConverterBoolean.INSTANCE.Read(stream),
            FfiConverterOptionalString.INSTANCE.Read(stream),
            FfiConverterULong.INSTANCE.Read(stream),
            FfiConverterULong.INSTANCE.Read(stream),
            FfiConverterULong.INSTANCE.Read(stream),
            FfiConverterULong.INSTANCE.Read(stream),
            FfiConverterULong.INSTANCE.Read(stream),
            FfiConverterULong.INSTANCE.Read(stream),
            FfiConverterULong.INSTANCE.Read(stream),
            FfiConverterULong.INSTANCE.Read(stream),
            FfiConverterULong.INSTANCE.Read(stream)
        );
    }

    public override int AllocationSize(SyncStatus value) {
        return
            FfiConverterBoolean.INSTANCE.AllocationSize(value.@inProgress) +
            FfiConverterOptionalString.INSTANCE.AllocationSize(value.@lastError) +
            FfiConverterULong.INSTANCE.AllocationSize(value.@syncId) +
            FfiConverterULong.INSTANCE.AllocationSize(value.@startBlock) +
            FfiConverterULong.INSTANCE.AllocationSize(value.@endBlock) +
            FfiConverterULong.INSTANCE.AllocationSize(value.@blocksDone) +
            FfiConverterULong.INSTANCE.AllocationSize(value.@trialDecDone) +
            FfiConverterULong.INSTANCE.AllocationSize(value.@txnScanDone) +
            FfiConverterULong.INSTANCE.AllocationSize(value.@blocksTotal) +
            FfiConverterULong.INSTANCE.AllocationSize(value.@batchNum) +
            FfiConverterULong.INSTANCE.AllocationSize(value.@batchTotal);
    }

    public override void Write(SyncStatus value, BigEndianStream stream) {
            FfiConverterBoolean.INSTANCE.Write(value.@inProgress, stream);
            FfiConverterOptionalString.INSTANCE.Write(value.@lastError, stream);
            FfiConverterULong.INSTANCE.Write(value.@syncId, stream);
            FfiConverterULong.INSTANCE.Write(value.@startBlock, stream);
            FfiConverterULong.INSTANCE.Write(value.@endBlock, stream);
            FfiConverterULong.INSTANCE.Write(value.@blocksDone, stream);
            FfiConverterULong.INSTANCE.Write(value.@trialDecDone, stream);
            FfiConverterULong.INSTANCE.Write(value.@txnScanDone, stream);
            FfiConverterULong.INSTANCE.Write(value.@blocksTotal, stream);
            FfiConverterULong.INSTANCE.Write(value.@batchNum, stream);
            FfiConverterULong.INSTANCE.Write(value.@batchTotal, stream);
    }
}





public enum Network: int {
    
    MAIN_NET,
    TEST_NET
}

class FfiConverterTypeNetwork: FfiConverterRustBuffer<Network> {
    public static FfiConverterTypeNetwork INSTANCE = new FfiConverterTypeNetwork();

    public override Network Read(BigEndianStream stream) {
        var value = stream.ReadInt() - 1;
        if (Enum.IsDefined(typeof(Network), value)) {
            return (Network)value;
        } else {
            throw new InternalException(String.Format("invalid enum value '{}' in FfiConverterTypeNetwork.Read()", value));
        }
    }

    public override int AllocationSize(Network value) {
        return 4;
    }

    public override void Write(Network value, BigEndianStream stream) {
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
    
    public class InvalidHandle: LightWalletException {
        public InvalidHandle(string message): base(message) {}
    }
    
    public class Other: LightWalletException {
        public Other(string message): base(message) {}
    }
    
}

class FfiConverterTypeLightWalletError : FfiConverterRustBuffer<LightWalletException>, CallStatusErrorHandler<LightWalletException> {
    public static FfiConverterTypeLightWalletError INSTANCE = new FfiConverterTypeLightWalletError();

    public override LightWalletException Read(BigEndianStream stream) {
        var value = stream.ReadInt();
        switch (value) {
            case 1: return new LightWalletException.InvalidUri(FfiConverterString.INSTANCE.Read(stream));
            case 2: return new LightWalletException.InvalidHandle(FfiConverterString.INSTANCE.Read(stream));
            case 3: return new LightWalletException.Other(FfiConverterString.INSTANCE.Read(stream));
            default:
                throw new InternalException(String.Format("invalid enum value '{}' in FfiConverterTypeLightWalletError.Read()", value));
        }
    }

    public override int AllocationSize(LightWalletException value) {
        return 4 + FfiConverterString.INSTANCE.AllocationSize(value.Message);
    }

    public override void Write(LightWalletException value, BigEndianStream stream) {
        switch (value) {
            case LightWalletException.InvalidUri:
                stream.WriteInt(1);
                FfiConverterString.INSTANCE.Write(value.Message, stream);
                break;
            case LightWalletException.InvalidHandle:
                stream.WriteInt(2);
                FfiConverterString.INSTANCE.Write(value.Message, stream);
                break;
            case LightWalletException.Other:
                stream.WriteInt(3);
                FfiConverterString.INSTANCE.Write(value.Message, stream);
                break;
            default:
                throw new InternalException(String.Format("invalid enum value '{}' in FfiConverterTypeLightWalletError.Write()", value));
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
#pragma warning restore 8625

public static class LightWalletMethods {
    /// <exception cref="LightWalletException"></exception>
    public static UInt64 LightwalletGetBlockHeight(String @serverUri) {
        return FfiConverterULong.INSTANCE.Lift(
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletError.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.LightWallet_a7b0_lightwallet_get_block_height(FfiConverterString.INSTANCE.Lower(@serverUri), ref _status)
));
    }

    /// <exception cref="LightWalletException"></exception>
    public static UInt64 LightwalletInitialize(Config @config) {
        return FfiConverterULong.INSTANCE.Lift(
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletError.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.LightWallet_a7b0_lightwallet_initialize(FfiConverterTypeConfig.INSTANCE.Lower(@config), ref _status)
));
    }

    public static Boolean LightwalletDeinitialize(UInt64 @handle) {
        return FfiConverterBoolean.INSTANCE.Lift(
    _UniffiHelpers.RustCall( (ref RustCallStatus _status) =>
    _UniFFILib.LightWallet_a7b0_lightwallet_deinitialize(FfiConverterULong.INSTANCE.Lower(@handle), ref _status)
));
    }

    /// <exception cref="LightWalletException"></exception>
    public static String LightwalletSync(UInt64 @handle) {
        return FfiConverterString.INSTANCE.Lift(
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletError.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.LightWallet_a7b0_lightwallet_sync(FfiConverterULong.INSTANCE.Lower(@handle), ref _status)
));
    }

    /// <exception cref="LightWalletException"></exception>
    public static void LightwalletSyncInterrupt(UInt64 @handle) {
        
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletError.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.LightWallet_a7b0_lightwallet_sync_interrupt(FfiConverterULong.INSTANCE.Lower(@handle), ref _status)
);
    }

    /// <exception cref="LightWalletException"></exception>
    public static SyncStatus LightwalletSyncStatus(UInt64 @handle) {
        return FfiConverterTypeSyncStatus.INSTANCE.Lift(
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletError.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.LightWallet_a7b0_lightwallet_sync_status(FfiConverterULong.INSTANCE.Lower(@handle), ref _status)
));
    }

    /// <exception cref="LightWalletException"></exception>
    public static UInt64 LightwalletGetBirthdayHeight(UInt64 @handle) {
        return FfiConverterULong.INSTANCE.Lift(
    _UniffiHelpers.RustCallWithError(FfiConverterTypeLightWalletError.INSTANCE, (ref RustCallStatus _status) =>
    _UniFFILib.LightWallet_a7b0_lightwallet_get_birthday_height(FfiConverterULong.INSTANCE.Lower(@handle), ref _status)
));
    }

}

