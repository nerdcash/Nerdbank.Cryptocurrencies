// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1600 // Elements should be documented

using Nerdbank.Zcash.FixedLengthStructs;

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		/// <summary>
		/// A spending key.
		/// </summary>
		internal readonly struct SpendingKey
		{
			private readonly Bytes32 value;

			internal SpendingKey(ReadOnlySpan<byte> value)
			{
				this.value = new(value);
			}

			/// <summary>
			/// Gets the buffer. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> Value => this.value.Value;
		}

		/// <summary>
		/// A spend validating key.
		/// </summary>
		internal readonly struct SpendValidatingKey
		{
			private readonly Bytes32 value;

			internal SpendValidatingKey(ReadOnlySpan<byte> value)
			{
				this.value = new(value);
			}

			/// <summary>
			/// Gets the buffer. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> Value => this.value.Value;
		}

		/// <summary>
		/// The IVK commit randomness.
		/// </summary>
		internal readonly struct CommitIvkRandomness
		{
			private readonly Bytes32 value;

			internal CommitIvkRandomness(ReadOnlySpan<byte> value)
			{
				this.value = new(value);
			}

			/// <summary>
			/// Gets the buffer. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> Value => this.value.Value;
		}

		/// <summary>
		/// The IVK..
		/// </summary>
		internal readonly struct KeyAgreementPrivateKey
		{
			private readonly Bytes32 value;

			internal KeyAgreementPrivateKey(ReadOnlySpan<byte> value)
			{
				this.value = new(value);
			}

			/// <summary>
			/// Gets the buffer. Always 32 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> Value => this.value.Value;
		}

		/// <summary>
		/// The diversifier used when constructing an <see cref="OrchardReceiver"/> from a <see cref="FullViewingKey"/>.
		/// </summary>
		internal readonly struct Diversifier
		{
			private readonly Bytes11 value;

			internal Diversifier(ReadOnlySpan<byte> value)
			{
				this.value = new(value);
			}

			/// <summary>
			/// Gets the buffer. Always 11 bytes in length.
			/// </summary>
			internal readonly ReadOnlySpan<byte> Value => this.value.Value;
		}
	}
}
