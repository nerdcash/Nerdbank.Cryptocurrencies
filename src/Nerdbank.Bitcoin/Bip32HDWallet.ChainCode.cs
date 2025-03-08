// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace Nerdbank.Bitcoin;

public static partial class Bip32HDWallet
{
	/// <summary>
	/// A 32-byte chain code that all extended keys in a derivation path share.
	/// </summary>
	[InlineArray(Length)]
	public struct ChainCode : IEquatable<ChainCode>
	{
		/// <summary>
		/// The length of the value in bytes.
		/// </summary>
		public const int Length = 32;

		private byte element;

		/// <summary>
		/// Initializes a new instance of the <see cref="ChainCode"/> struct.
		/// </summary>
		/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
		public ChainCode(ReadOnlySpan<byte> value)
		{
			value.CopyToWithLengthCheck(this);
		}

		/// <summary>
		/// Returns a strongly-typed struct over a span of bytes without incuring the cost of a memory copy.
		/// </summary>
		/// <param name="value">The bytes containing the value. This should have a length equal to <see cref="Length"/>.</param>
		/// <returns>The strongly-typed element.</returns>
		public static ref readonly ChainCode From(ReadOnlySpan<byte> value) => ref MemoryMarshal.GetReference(MemoryMarshal.Cast<byte, ChainCode>(value));

		/// <inheritdoc/>
		readonly bool IEquatable<ChainCode>.Equals(ChainCode other) => this[..].SequenceEqual(other);

		/// <inheritdoc cref="IEquatable{T}.Equals"/>
		public readonly bool Equals(in ChainCode other) => this[..].SequenceEqual(other);
	}
}
