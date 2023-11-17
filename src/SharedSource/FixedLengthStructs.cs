// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.FixedLengthStructs;

#pragma warning disable SA1649 // File name should match first type name
#pragma warning disable SA1600 // Elements should be documented

internal unsafe struct Bytes20
{
	private const int Length = 20;
	private fixed byte value[Length];

	internal Bytes20(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this.ValueWritable);
	}

	[UnscopedRef]
	internal readonly ReadOnlySpan<byte> Value => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.value[0]), Length);

	[UnscopedRef]
	internal Span<byte> ValueWritable => MemoryMarshal.CreateSpan(ref this.value[0], Length);
}

internal unsafe struct Bytes32
{
	private const int Length = 32;
	private fixed byte value[Length];

	internal Bytes32(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this.ValueWritable);
	}

	[UnscopedRef]
	internal readonly ReadOnlySpan<byte> Value => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.value[0]), Length);

	[UnscopedRef]
	private Span<byte> ValueWritable => MemoryMarshal.CreateSpan(ref this.value[0], Length);
}

internal unsafe struct Bytes33
{
	private const int Length = 33;
	private fixed byte value[Length];

	internal Bytes33(ReadOnlySpan<byte> value, bool allowShorterInput = false)
	{
		value.CopyToWithLengthCheck(this.ValueWritable, allowShorterInput: allowShorterInput);
	}

	[UnscopedRef]
	internal readonly ReadOnlySpan<byte> Value => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.value[0]), Length);

	[UnscopedRef]
	private Span<byte> ValueWritable => MemoryMarshal.CreateSpan(ref this.value[0], Length);
}

internal unsafe struct Bytes64
{
	private const int Length = 64;
	private fixed byte value[Length];

	internal Bytes64(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this.ValueWritable);
	}

	[UnscopedRef]
	internal readonly ReadOnlySpan<byte> Value => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.value[0]), Length);

	[UnscopedRef]
	private Span<byte> ValueWritable => MemoryMarshal.CreateSpan(ref this.value[0], Length);
}

internal unsafe struct Bytes96
{
	private const int Length = 96;
	private fixed byte value[Length];

	internal Bytes96(ReadOnlySpan<byte> value)
	{
		value.CopyToWithLengthCheck(this.ValueWritable);
	}

	[UnscopedRef]
	internal readonly ReadOnlySpan<byte> Value => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this.value[0]), Length);

	[UnscopedRef]
	private Span<byte> ValueWritable => MemoryMarshal.CreateSpan(ref this.value[0], Length);
}
