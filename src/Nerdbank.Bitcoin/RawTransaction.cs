// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Bitcoin;

/// <summary>
/// Represents a raw Bitcoin transaction.
/// </summary>
/// <remarks>
/// As documented <see href="https://developer.bitcoin.org/reference/transactions.html#raw-transaction-format">here</see>.
/// </remarks>
public readonly record struct RawTransaction
{
	/// <summary>
	/// Describes an input to a transaction.
	/// </summary>
	/// <remarks>
	/// As documented <see href="https://developer.bitcoin.org/reference/transactions.html#txin-a-transaction-input-non-coinbase">here</see>.
	/// </remarks>
	public readonly record struct TxIn
	{
		/// <summary>
		/// Gets the previous outpoint being spent.
		/// </summary>
		/// <remarks>
		/// May be <see langword="null" /> if this is the coinbase transaction for the block.
		/// </remarks>
		public required Outpoint? PreviousOutput { get; init; }

		/// <summary>
		/// Gets a script-language script which satisfies the conditions placed in the outpoint’s pubkey script.
		/// </summary>
		public required ReadOnlyMemory<byte> SignatureScript { get; init; }

		/// <summary>
		/// Gets the sequence number. Default for Bitcoin Core and almost all other programs is 0xffffffff.
		/// </summary>
		public required uint Sequence { get; init; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TxIn"/> record
		/// from its raw encoding.
		/// </summary>
		/// <param name="reader">A reader over the raw encoded value.</param>
		/// <returns>The initialized <see cref="TxIn"/> value.</returns>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content backing the <paramref name="reader"/> is unchanged.
		/// </remarks>
		public static TxIn Decode(ref DecodingReader reader)
		{
			Outpoint? previousOutput = Outpoint.Decode(ref reader);
			int scriptLength = reader.ReadInt32Compact();
			ReadOnlyMemory<byte> signatureScript = reader.Read(scriptLength);
			uint sequence = reader.ReadUInt32LE();

			return new()
			{
				PreviousOutput = previousOutput,
				SignatureScript = signatureScript,
				Sequence = sequence,
			};
		}
	}

	/// <summary>
	/// Represents an output (i.e. spend) of a transaction.
	/// </summary>
	public readonly record struct TxOut
	{
		/// <summary>
		/// Gets the number of satoshis to spend.
		/// May be zero; the sum of all outputs may not exceed the sum of satoshis previously spent to the outpoints provided in the input section.
		/// (Exception: coinbase transactions spend the block subsidy and collected transaction fees.)
		/// </summary>
		public required long Value { get; init; }

		/// <summary>
		/// Gets the conditions which must be satisfied to spend this output.
		/// </summary>
		public required ReadOnlyMemory<byte> Script { get; init; }

		/// <summary>
		/// Initializes a new instance of the <see cref="TxOut"/> record
		/// from its raw encoding.
		/// </summary>
		/// <param name="reader">A reader over the raw encoded value.</param>
		/// <returns>The initialized <see cref="TxOut"/> value.</returns>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content backing the <paramref name="reader"/> is unchanged.
		/// </remarks>
		public static TxOut Decode(ref DecodingReader reader)
		{
			long value = reader.ReadInt64LE();
			int scriptLength = reader.ReadInt32Compact();
			ReadOnlyMemory<byte> script = reader.Read(scriptLength);

			return new()
			{
				Value = value,
				Script = script,
			};
		}
	}

	/// <summary>
	/// Describes a transparent output of a transaction.
	/// </summary>
	/// <remarks>
	/// As documented <see href="https://developer.bitcoin.org/reference/transactions.html#outpoint-the-specific-part-of-a-specific-output">here</see>.
	/// </remarks>
	public readonly record struct Outpoint
	{
		private readonly ReadOnlyMemory<byte> txid;

		/// <summary>
		/// Initializes a new instance of the <see cref="Outpoint"/> class.
		/// </summary>
		/// <param name="txid">The 32-byte ID of the transaction whose output is being spent.</param>
		/// <param name="index">The index of the output within its containing transaction.</param>
		public Outpoint(ReadOnlyMemory<byte> txid, uint index)
		{
			SharedCryptoUtilities.CheckLength(txid, 32);
			this.txid = txid;
			this.Index = index;
		}

		/// <summary>
		/// Gets the 32-byte TXID of the transaction holding the output to spend.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Txid => this.txid;

		/// <summary>
		/// Gets the output index number of the specific output to spend from the transaction. The first output is 0.
		/// </summary>
		public readonly uint Index { get; init; }

		/// <summary>
		/// Initializes a new instance of the <see cref="Outpoint"/> record
		/// from its raw encoding.
		/// </summary>
		/// <param name="reader">A reader over the raw encoded value.</param>
		/// <returns>The initialized <see cref="Outpoint"/> value.</returns>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content backing the <paramref name="reader"/> is unchanged.
		/// </remarks>
		public static Outpoint? Decode(ref DecodingReader reader)
		{
			ReadOnlyMemory<byte> txid = reader.Read(32);
			uint index = reader.ReadUInt32LE();
			if (index == 0xffffffff && txid.Span.IndexOfAnyExcept((byte)0) == -1)
			{
				// This is a coinbase transaction, which has no outpoint for its input.
				return null;
			}

			return new(txid, index);
		}
	}
}
