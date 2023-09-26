// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using static Nerdbank.Bitcoin.RawTransaction;

namespace Nerdbank.Zcash;

/// <summary>
/// Facilitates transaction format encoding.
/// </summary>
/// <remarks>
/// The encoding is as per <see href="https://zips.z.cash/zip-0225">ZIP-225</see> and
/// section 7 of the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol</see>.
/// </remarks>
public readonly record struct RawTransaction
{
	private const uint HeaderVersionMask = 0x7fffffff;

	private const uint HeaderOverwinteredMask = 0x80000000;

	/// <summary>
	/// A delegate that reads a single element from a sequence.
	/// </summary>
	/// <typeparam name="T">The element to be read.</typeparam>
	/// <param name="reader">The reader to use to read one element.</param>
	/// <param name="transactionVersion">The version of the transaction being read.</param>
	/// <returns>The decoded element.</returns>
	internal delegate T DescriptionReader<T>(ref DecodingReader reader, uint transactionVersion);

	/// <summary>
	/// Orchard pool related flags.
	/// </summary>
	[Flags]
	public enum OrchardFlags : byte
	{
		/// <summary>
		/// Enables orchand spends.
		/// </summary>
		EnableSpendsOrchard = 0x1,

		/// <summary>
		/// Enables orchard outputs.
		/// </summary>
		EnableOutputsOrchard = 0x2,
	}

	/// <summary>
	/// Gets the <see cref="Overwintered"/> and <see cref="Version"/> data.
	/// </summary>
	/// <remarks>
	/// Use the mentioned sub-properties instead for easier access.
	/// </remarks>
	public required uint Header { get; init; }

	/// <summary>
	/// Gets a value indicating whether this transaction has the "overwintered" bit set.
	/// </summary>
	public bool Overwintered => (this.Header & HeaderOverwinteredMask) != 0;

	/// <summary>
	/// Gets the transaction version.
	/// </summary>
	public uint Version => this.Header & HeaderVersionMask;

	/// <summary>
	/// Gets the version group ID (nonzero).
	/// </summary>
	public required uint VersionGroupId { get; init; }

	/// <summary>
	/// Gets the consensus branch ID (nonzero).
	/// </summary>
	public required uint ConsensusBranchId { get; init; }

	/// <summary>
	/// Gets the unix-epoch UTC time or block height, encoded as in Bitcoin.
	/// </summary>
	public required uint LockTime { get; init; }

	/// <summary>
	/// Gets a block height in the range {1 .. 499999999} after which the transaction will expire, or 0 to disable expiry.
	/// </summary>
	/// <remarks>
	/// See <see href="https://zips.z.cash/zip-0203">ZIP-203 Transaction Expiry</see>.
	/// </remarks>
	public required uint ExpiryHeight { get; init; }

	/// <summary>
	/// Gets the fields related to the Transparent pool.
	/// </summary>
	public required TransparentFields Transparent { get; init; }

	/// <summary>
	/// Gets the fields related to the Sprout pool.
	/// </summary>
	public required SproutFields Sprout { get; init; }

	/// <summary>
	/// Gets the fields related to the Sapling pool.
	/// </summary>
	public required SaplingFields Sapling { get; init; }

	/// <summary>
	/// Gets the fields related to the Orchard pool.
	/// </summary>
	public required OrchardFields Orchard { get; init; }

	/// <summary>
	/// Decodes a transaction from its raw encoded bytes.
	/// </summary>
	/// <param name="bytes">The buffer containing the transaction.</param>
	/// <returns>The decoded transaction. This relies on <paramref name="bytes"/> as its backing store for any buffers.</returns>
	/// <exception cref="NotSupportedException">Thrown if the transaction version exceeds the supported version range.</exception>
	/// <remarks>
	/// This method does not allocate any memory, and will only be valid while the content of <paramref name="bytes"/> is unchanged.
	/// </remarks>
	public static RawTransaction Decode(ReadOnlyMemory<byte> bytes)
	{
		DecodingReader reader = new(bytes);

		uint header = reader.ReadUInt32LE();
		uint version = header & HeaderVersionMask;
		if (version is > 5 or < 1)
		{
			throw new NotSupportedException(Strings.FormatUnsupportedTransactionVersion(version));
		}

		uint versionGroupId = 0, consensusBranchId = 0, lockTime = 0xcccccccc, expiryHeight = 0;

		if (version >= 3)
		{
			versionGroupId = reader.ReadUInt32LE();
		}

		if (version >= 5)
		{
			consensusBranchId = reader.ReadUInt32LE();
			lockTime = reader.ReadUInt32LE();
			expiryHeight = reader.ReadUInt32LE();
		}

		ReadOnlyMemory<TxIn> txIn = ReadTxIn(ref reader, reader.ReadInt32Compact());
		ReadOnlyMemory<TxOut> txOut = ReadTxOut(ref reader, reader.ReadInt32Compact());

		if (version < 5)
		{
			lockTime = reader.ReadUInt32LE();
			if (version >= 3)
			{
				expiryHeight = reader.ReadUInt32LE();
			}
		}

		long valueBalanceSapling = 0;
		DescriptionEnumerator<SaplingSpendDescription> spendsSapling = default;
		DescriptionEnumerator<SaplingOutputDescription> outputsSapling = default;
		if (version == 4)
		{
			valueBalanceSapling = reader.ReadInt64LE();
		}

		if (version >= 4)
		{
			spendsSapling = DescriptionEnumerator<SaplingSpendDescription>.Initialize(ref reader, SaplingSpendDescription.Decode, version);
			outputsSapling = DescriptionEnumerator<SaplingOutputDescription>.Initialize(ref reader, SaplingOutputDescription.Decode, version);
		}

		if (version == 5 && spendsSapling.Count + outputsSapling.Count > 0)
		{
			valueBalanceSapling = reader.ReadInt64LE();
		}

		ReadOnlyMemory<byte> anchorSapling = default;
		ReadOnlyMemory<byte> spendProofsSapling = default;
		ReadOnlyMemory<byte> spendAuthSigsSapling = default;
		ReadOnlyMemory<byte> outputProofsSapling = default;
		ReadOnlyMemory<JSDescriptionBCTV14> joinSplitsBCTV14 = default;
		ReadOnlyMemory<JSDescriptionGroth16> joinSplitsGroth16 = default;
		ReadOnlyMemory<byte> joinSplitPubKey = default;
		ReadOnlyMemory<byte> joinSplitSig = default;
		if (version == 5)
		{
			if (spendsSapling.Count > 0)
			{
				anchorSapling = reader.Read(32);
			}

			spendProofsSapling = reader.Read(192 * spendsSapling.Count);
			spendAuthSigsSapling = reader.Read(64 * spendsSapling.Count);
			outputProofsSapling = reader.Read(192 * outputsSapling.Count);
		}
		else if (version >= 2)
		{
			// Read sprout pool fields.
			ulong nJoinSplit = reader.ReadUInt64Compact();
			if (nJoinSplit > 0)
			{
				if (version < 4)
				{
					joinSplitsBCTV14 = ReadJSDescriptionBCTV14(ref reader, nJoinSplit, version);
				}
				else
				{
					joinSplitsGroth16 = ReadJSDescriptionGroth16(ref reader, nJoinSplit, version);
				}

				joinSplitPubKey = reader.Read(32);
				joinSplitSig = reader.Read(64);
			}
		}

		ReadOnlyMemory<byte> bindingSigSapling = default;
		if (version >= 4 && spendsSapling.Count + outputsSapling.Count > 0)
		{
			bindingSigSapling = reader.Read(64);
		}

		DescriptionEnumerator<OrchardAction> actionsOrchard = default;
		OrchardFlags flagsOrchard = default;
		long valueBalanceOrchard = 0;
		ReadOnlyMemory<byte> anchorOrchard = default;
		ReadOnlyMemory<byte> proofsOrchard = default;
		ReadOnlyMemory<byte> spendAuthSigsOrchard = default;
		ReadOnlyMemory<byte> bindingSigOrchard = default;
		if (version == 5)
		{
			actionsOrchard = DescriptionEnumerator<OrchardAction>.Initialize(ref reader, OrchardAction.Decode, version);
			if (actionsOrchard.Count > 0)
			{
				flagsOrchard = (OrchardFlags)reader.ReadByte();
				valueBalanceOrchard = reader.ReadInt64LE();
				anchorOrchard = reader.Read(32);
				int sizeProofsOrchard = reader.ReadInt32Compact();
				proofsOrchard = reader.Read(sizeProofsOrchard);
				spendAuthSigsOrchard = reader.Read(64 * actionsOrchard.Count);
				bindingSigOrchard = reader.Read(64);
			}
		}

		Assumes.True(reader.RemainingLength == 0, $"Expected to have read the full transaction, but {reader.RemainingLength} bytes remain.");

		return new()
		{
			Header = header,
			VersionGroupId = versionGroupId,
			ConsensusBranchId = consensusBranchId,
			LockTime = lockTime,
			ExpiryHeight = expiryHeight,
			Transparent = new()
			{
				Inputs = txIn,
				Outputs = txOut,
			},
			Sprout = new()
			{
				JoinSplitBCTV14 = joinSplitsBCTV14,
				JoinSplitGroth16 = joinSplitsGroth16,
				JoinSplitPublicKey = joinSplitPubKey.ToArray(),
				JoinSplitSig = joinSplitSig.ToArray(),
			},
			Sapling = new()
			{
				ValueBalance = valueBalanceSapling,
				Spends = spendsSapling,
				Outputs = outputsSapling,
				Anchor = anchorSapling.ToArray(),
				SpendProofs = spendProofsSapling.ToArray(),
				SpendAuthSigs = spendAuthSigsSapling.ToArray(),
				OutputProofsS = outputProofsSapling.ToArray(),
				BindingSig = bindingSigSapling.ToArray(),
			},
			Orchard = new()
			{
				Actions = actionsOrchard,
				Flags = flagsOrchard,
				ValueBalance = valueBalanceOrchard,
				Anchor = anchorOrchard.ToArray(),
				Proofs = proofsOrchard.ToArray(),
				SpendAuthSigs = spendAuthSigsOrchard.ToArray(),
				BindingSig = bindingSigOrchard.ToArray(),
			},
		};
	}

	private static ReadOnlyMemory<JSDescriptionGroth16> ReadJSDescriptionGroth16(ref DecodingReader reader, ulong count, uint version)
	{
		if (count == 0)
		{
			return default;
		}

		JSDescriptionGroth16[] result = new JSDescriptionGroth16[count];
		for (ulong i = 0; i < count; i++)
		{
			result[i] = JSDescriptionGroth16.Decode(ref reader, version);
		}

		return result;
	}

	private static ReadOnlyMemory<JSDescriptionBCTV14> ReadJSDescriptionBCTV14(ref DecodingReader reader, ulong count, uint version)
	{
		if (count == 0)
		{
			return default;
		}

		JSDescriptionBCTV14[] result = new JSDescriptionBCTV14[count];
		for (ulong i = 0; i < count; i++)
		{
			result[i] = JSDescriptionBCTV14.Decode(ref reader, version);
		}

		return result;
	}

	private static ReadOnlyMemory<TxIn> ReadTxIn(ref DecodingReader reader, int count)
	{
		if (count == 0)
		{
			return default;
		}

		TxIn[] result = new TxIn[count];
		for (int i = 0; i < count; i++)
		{
			result[i] = TxIn.Decode(ref reader);
		}

		return result;
	}

	private static ReadOnlyMemory<TxOut> ReadTxOut(ref DecodingReader reader, int count)
	{
		if (count == 0)
		{
			return default;
		}

		TxOut[] result = new TxOut[count];
		for (int i = 0; i < count; i++)
		{
			result[i] = TxOut.Decode(ref reader);
		}

		return result;
	}

	/// <summary>
	/// The Transparent-related fields in a transaction.
	/// </summary>
	public readonly record struct TransparentFields
	{
		/// <summary>
		/// Gets the transparent inputs.
		/// </summary>
		public required ReadOnlyMemory<TxIn> Inputs { get; init; }

		/// <summary>
		/// Gets the transparent outputs.
		/// </summary>
		public required ReadOnlyMemory<TxOut> Outputs { get; init; }
	}

	/// <summary>
	/// The Sprout-related fields in a transaction.
	/// </summary>
	public readonly record struct SproutFields
	{
		/// <summary>
		/// Gets a sequence of JoinSplit descriptions using BCTV14 proofs.
		/// </summary>
		public required ReadOnlyMemory<JSDescriptionBCTV14> JoinSplitBCTV14 { get; init; }

		/// <summary>
		/// Gets a sequence of JoinSplit descriptions using Groth16 proofs.
		/// </summary>
		public required ReadOnlyMemory<JSDescriptionGroth16> JoinSplitGroth16 { get; init; }

		/// <summary>
		/// Gets an encoding of a JoinSplitSig public validating key.
		/// </summary>
		public required ReadOnlyMemory<byte> JoinSplitPublicKey { get; init; }

		/// <summary>
		/// Gets a signature on the prefix of the transaction encoding, validated using joinSplitPubKey as specified in § 4.11.
		/// </summary>
		public required ReadOnlyMemory<byte> JoinSplitSig { get; init; }
	}

	/// <summary>
	/// The Sapling-related fields in a transaction.
	/// </summary>
	public readonly record struct SaplingFields
	{
		/// <summary>
		/// Gets the net value of Sapling spends minus outputs.
		/// </summary>
		public required long ValueBalance { get; init; }

		/// <summary>
		/// Gets a sequence of spend descriptions.
		/// </summary>
		public required DescriptionEnumerator<SaplingSpendDescription> Spends { get; init; }

		/// <summary>
		/// Gets a sequence of output descriptions.
		/// </summary>
		public required DescriptionEnumerator<SaplingOutputDescription> Outputs { get; init; }

		/// <summary>
		/// Gets a root of the Sapling note commitment tree at some block height in the past.
		/// </summary>
		public required ReadOnlyMemory<byte> Anchor { get; init; }

		/// <summary>
		/// Gets encodings of the zk-SNARK proofs for each sapling spend in <see cref="Spends"/>.
		/// </summary>
		public required ReadOnlyMemory<byte> SpendProofs { get; init; }

		/// <summary>
		/// Gets authorizing signatures for each sapling spend in <see cref="Spends"/>.
		/// </summary>
		public required ReadOnlyMemory<byte> SpendAuthSigs { get; init; }

		/// <summary>
		/// Gets encodings of the zk-SNARK proofs for each sapling output in <see cref="Outputs"/>.
		/// </summary>
		public required ReadOnlyMemory<byte> OutputProofsS { get; init; }

		/// <summary>
		/// Gets a sapling binding signature on the SIGHASH transaction hash.
		/// </summary>
		public required ReadOnlyMemory<byte> BindingSig { get; init; }
	}

	/// <summary>
	/// The Orchard-related fields in a transaction.
	/// </summary>
	public readonly record struct OrchardFields
	{
		/// <summary>
		/// Gets a sequence of Orchard Action descriptions.
		/// </summary>
		public required DescriptionEnumerator<OrchardAction> Actions { get; init; }

		/// <summary>
		/// Gets flags related to orchard.
		/// </summary>
		public required OrchardFlags Flags { get; init; }

		/// <summary>
		/// Gets the net value of Orchard spends minus outputs.
		/// </summary>
		public required long ValueBalance { get; init; }

		/// <summary>
		/// Gets a root of the Orchard note commitment tree at some block height in the past.
		/// </summary>
		public required ReadOnlyMemory<byte> Anchor { get; init; }

		/// <summary>
		/// Gets the encoding of aggregated zk-SNARK proofs for Orchard Actions.
		/// </summary>
		public required ReadOnlyMemory<byte> Proofs { get; init; }

		/// <summary>
		/// Gets authorizing signatures for each Orchard Action.
		/// </summary>
		public required ReadOnlyMemory<byte> SpendAuthSigs { get; init; }

		/// <summary>
		/// Gets an Orchard binding signature on the SIGHASH transaction hash.
		/// </summary>
		public required ReadOnlyMemory<byte> BindingSig { get; init; }
	}

	/// <summary>
	/// Describes a Sprout pool JoinSplit for transaction v2 and v3 formats (i.e. before Sapling).
	/// </summary>
	public readonly record struct JSDescriptionBCTV14
	{
		private readonly ulong vpubOld;
		private readonly ulong vpubNew;
		private readonly ReadOnlyMemory<byte> anchor;
		private readonly ReadOnlyMemory<byte> nullifiers;
		private readonly ReadOnlyMemory<byte> commitments;
		private readonly ReadOnlyMemory<byte> ephemeralKey;
		private readonly ReadOnlyMemory<byte> randomSeed;
		private readonly ReadOnlyMemory<byte> vmacs;
		private readonly ReadOnlyMemory<byte> zkproof;
		private readonly ReadOnlyMemory<byte> encCiphertexts;

		/// <summary>
		/// Initializes a new instance of the <see cref="JSDescriptionBCTV14"/> class.
		/// </summary>
		/// <param name="vpub_old">A value v_old_pub that the JoinSplit transfer removes from the transparent transaction value pool.</param>
		/// <param name="vpub_new">A value v_new_pub that the JoinSplit transfer inserts into the transparent transaction value pool.</param>
		/// <param name="anchor">A root rtSprout of the Sprout note commitment tree at some block height in the past, or the root produced by a previous JoinSplit transfer in this transaction.</param>
		/// <param name="nullifiers">A sequence of nullifiers of the input notes.</param>
		/// <param name="commitments">A sequence of note commitments for the output notes.</param>
		/// <param name="ephemeralKey">A Curve25519 public key.</param>
		/// <param name="randomSeed">A 256-bit seed that must be chosen independently at random for each JoinSplit description.</param>
		/// <param name="vmacs">A sequence of message authentication tags h_sig 1..N old binding hSig to each ask of the JoinSplit description, computed as described in § 4.11.</param>
		/// <param name="zkproof">An encoding of the zk-SNARK proof.</param>
		/// <param name="encCiphertexts">A sequence of ciphertext components for the encrypted output notes.</param>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffers provided remain the same.
		/// </remarks>
		public JSDescriptionBCTV14(
			ulong vpub_old,
			ulong vpub_new,
			ReadOnlyMemory<byte> anchor,
			ReadOnlyMemory<byte> nullifiers,
			ReadOnlyMemory<byte> commitments,
			ReadOnlyMemory<byte> ephemeralKey,
			ReadOnlyMemory<byte> randomSeed,
			ReadOnlyMemory<byte> vmacs,
			ReadOnlyMemory<byte> zkproof,
			ReadOnlyMemory<byte> encCiphertexts)
		{
			SharedCryptoUtilities.CheckLength(anchor, 32);
			SharedCryptoUtilities.CheckLength(nullifiers, 64);
			SharedCryptoUtilities.CheckLength(commitments, 64);
			SharedCryptoUtilities.CheckLength(ephemeralKey, 32);
			SharedCryptoUtilities.CheckLength(randomSeed, 32);
			SharedCryptoUtilities.CheckLength(vmacs, 64);
			SharedCryptoUtilities.CheckLength(zkproof, 296);
			SharedCryptoUtilities.CheckLength(encCiphertexts, 1202);

			this.vpubOld = vpub_old;
			this.vpubNew = vpub_new;
			this.anchor = anchor;
			this.nullifiers = nullifiers;
			this.commitments = commitments;
			this.ephemeralKey = ephemeralKey;
			this.randomSeed = randomSeed;
			this.vmacs = vmacs;
			this.zkproof = zkproof;
			this.encCiphertexts = encCiphertexts;
		}

		/// <summary>
		/// Gets a value v_old_pub that the JoinSplit transfer removes from the transparent transaction value pool.
		/// </summary>
		public ulong VpubOld => this.vpubOld;

		/// <summary>
		/// Gets a value v_new_pub that the JoinSplit transfer inserts into the transparent transaction value pool.
		/// </summary>
		public ulong VpubNew => this.vpubNew;

		/// <summary>
		/// Gets a root rtSprout of the Sprout note commitment tree at some block height in the past, or the root produced by a previous JoinSplit transfer in this transaction.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Anchor => this.anchor;

		/// <summary>
		/// Gets a sequence of nullifiers of the input notes.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Nullifiers => this.nullifiers;

		/// <summary>
		/// Gets a sequence of note commitments for the output notes.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Commitments => this.commitments;

		/// <summary>
		/// Gets a Curve25519 public key.
		/// </summary>
		public readonly ReadOnlyMemory<byte> EphemeralKey => this.ephemeralKey;

		/// <summary>
		/// Gets a 256-bit seed that must be chosen independently at random for each JoinSplit description.
		/// </summary>
		public readonly ReadOnlyMemory<byte> RandomSeed => this.randomSeed;

		/// <summary>
		/// Gets a sequence of message authentication tags h_sig 1..N old binding hSig to each ask of the JoinSplit description.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Vmacs => this.vmacs;

		/// <summary>
		/// Gets an encoding of the zk-SNARK proof.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Zkproof => this.zkproof;

		/// <summary>
		/// Gets a sequence of ciphertext components for the encrypted output notes.
		/// </summary>
		public readonly ReadOnlyMemory<byte> EncCiphertexts => this.encCiphertexts;

		/// <summary>
		/// Decodes a <see cref="JSDescriptionBCTV14"/>.
		/// </summary>
		/// <param name="reader">The reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being decoded.</param>
		/// <returns>The initialized value.</returns>
		/// <exception cref="NotSupportedException">Throw if the transaction version doesn't support this value.</exception>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffer backing the <paramref name="reader"/> remains the same.
		/// </remarks>
		public static JSDescriptionBCTV14 Decode(ref DecodingReader reader, uint transactionVersion)
		{
			if (transactionVersion is not (2 or 3))
			{
				throw new NotSupportedException(Strings.FormatUnsupportedTransactionVersion(transactionVersion));
			}

			ulong vpub_old = reader.ReadUInt64LE();
			ulong vpub_new = reader.ReadUInt64LE();
			ReadOnlyMemory<byte> anchor = reader.Read(32);
			ReadOnlyMemory<byte> nullifiers = reader.Read(64);
			ReadOnlyMemory<byte> commitments = reader.Read(64);
			ReadOnlyMemory<byte> ephemeralKey = reader.Read(32);
			ReadOnlyMemory<byte> randomSeed = reader.Read(32);
			ReadOnlyMemory<byte> vmacs = reader.Read(64);
			ReadOnlyMemory<byte> zkproof = reader.Read(296);
			ReadOnlyMemory<byte> encCiphertexts = reader.Read(1202);

			return new(
				vpub_old,
				vpub_new,
				anchor,
				nullifiers,
				commitments,
				ephemeralKey,
				randomSeed,
				vmacs,
				zkproof,
				encCiphertexts);
		}
	}

	/// <summary>
	/// Describes a Sprout pool JoinSplit for transaction v4 formats (i.e. after Sapling).
	/// </summary>
	public readonly record struct JSDescriptionGroth16
	{
		private readonly ulong vpubOld;
		private readonly ulong vpubNew;
		private readonly ReadOnlyMemory<byte> anchor;
		private readonly ReadOnlyMemory<byte> nullifiers;
		private readonly ReadOnlyMemory<byte> commitments;
		private readonly ReadOnlyMemory<byte> ephemeralKey;
		private readonly ReadOnlyMemory<byte> randomSeed;
		private readonly ReadOnlyMemory<byte> vmacs;
		private readonly ReadOnlyMemory<byte> zkproof;
		private readonly ReadOnlyMemory<byte> encCiphertexts;

		/// <summary>
		/// Initializes a new instance of the <see cref="JSDescriptionGroth16"/> class.
		/// </summary>
		/// <param name="vpub_old">A value v_old_pub that the JoinSplit transfer removes from the transparent transaction value pool.</param>
		/// <param name="vpub_new">A value v_new_pub that the JoinSplit transfer inserts into the transparent transaction value pool.</param>
		/// <param name="anchor">A root rtSprout of the Sprout note commitment tree at some block height in the past, or the root produced by a previous JoinSplit transfer in this transaction.</param>
		/// <param name="nullifiers">A sequence of nullifiers of the input notes.</param>
		/// <param name="commitments">A sequence of note commitments for the output notes.</param>
		/// <param name="ephemeralKey">A Curve25519 public key.</param>
		/// <param name="randomSeed">A 256-bit seed that must be chosen independently at random for each JoinSplit description.</param>
		/// <param name="vmacs">A sequence of message authentication tags h_sig 1..N old binding hSig to each ask of the JoinSplit description, computed as described in § 4.11.</param>
		/// <param name="zkproof">An encoding of the zk-SNARK proof.</param>
		/// <param name="encCiphertexts">A sequence of ciphertext components for the encrypted output notes.</param>
		public JSDescriptionGroth16(
			ulong vpub_old,
			ulong vpub_new,
			ReadOnlyMemory<byte> anchor,
			ReadOnlyMemory<byte> nullifiers,
			ReadOnlyMemory<byte> commitments,
			ReadOnlyMemory<byte> ephemeralKey,
			ReadOnlyMemory<byte> randomSeed,
			ReadOnlyMemory<byte> vmacs,
			ReadOnlyMemory<byte> zkproof,
			ReadOnlyMemory<byte> encCiphertexts)
		{
			SharedCryptoUtilities.CheckLength(anchor, 32);
			SharedCryptoUtilities.CheckLength(nullifiers, 32);
			SharedCryptoUtilities.CheckLength(commitments, 32);
			SharedCryptoUtilities.CheckLength(ephemeralKey, 32);
			SharedCryptoUtilities.CheckLength(randomSeed, 32);
			SharedCryptoUtilities.CheckLength(vmacs, 32);
			SharedCryptoUtilities.CheckLength(zkproof, 192);
			SharedCryptoUtilities.CheckLength(encCiphertexts, 1202);

			this.vpubOld = vpub_old;
			this.vpubNew = vpub_new;
			this.anchor = anchor;
			this.nullifiers = nullifiers;
			this.commitments = commitments;
			this.ephemeralKey = ephemeralKey;
			this.randomSeed = randomSeed;
			this.vmacs = vmacs;
			this.zkproof = zkproof;
			this.encCiphertexts = encCiphertexts;
		}

		/// <summary>
		/// Gets a value v_old_pub that the JoinSplit transfer removes from the transparent transaction value pool.
		/// </summary>
		public ulong VpubOld => this.vpubOld;

		/// <summary>
		/// Gets a value v_new_pub that the JoinSplit transfer inserts into the transparent transaction value pool.
		/// </summary>
		public ulong VpubNew => this.vpubNew;

		/// <summary>
		/// Gets a root rtSprout of the Sprout note commitment tree at some block height in the past, or the root produced by a previous JoinSplit transfer in this transaction.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Anchor => this.anchor;

		/// <summary>
		/// Gets a sequence of nullifiers of the input notes.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Nullifiers => this.nullifiers;

		/// <summary>
		/// Gets a sequence of note commitments for the output notes.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Commitments => this.commitments;

		/// <summary>
		/// Gets a Curve25519 public key.
		/// </summary>
		public readonly ReadOnlyMemory<byte> EphemeralKey => this.ephemeralKey;

		/// <summary>
		/// Gets a 256-bit seed that must be chosen independently at random for each JoinSplit description.
		/// </summary>
		public readonly ReadOnlyMemory<byte> RandomSeed => this.randomSeed;

		/// <summary>
		/// Gets a sequence of message authentication tags h_sig 1..N old binding hSig to each ask of the JoinSplit description.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Vmacs => this.vmacs;

		/// <summary>
		/// Gets an encoding of the zk-SNARK proof.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Zkproof => this.zkproof;

		/// <summary>
		/// Gets a sequence of ciphertext components for the encrypted output notes.
		/// </summary>
		public readonly ReadOnlyMemory<byte> EncCiphertexts => this.encCiphertexts;

		/// <summary>
		/// Decodes a <see cref="JSDescriptionGroth16"/>.
		/// </summary>
		/// <param name="reader">The reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being decoded.</param>
		/// <returns>The initialized value.</returns>
		/// <exception cref="NotSupportedException">Throw if the transaction version doesn't support this value.</exception>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffer backing the <paramref name="reader"/> remains the same.
		/// </remarks>
		public static JSDescriptionGroth16 Decode(ref DecodingReader reader, uint transactionVersion)
		{
			if (transactionVersion != 4)
			{
				throw new NotSupportedException(Strings.FormatUnsupportedTransactionVersion(transactionVersion));
			}

			ulong vpub_old = reader.ReadUInt64LE();
			ulong vpub_new = reader.ReadUInt64LE();
			ReadOnlyMemory<byte> anchor = reader.Read(32);
			ReadOnlyMemory<byte> nullifiers = reader.Read(64);
			ReadOnlyMemory<byte> commitments = reader.Read(64);
			ReadOnlyMemory<byte> ephemeralKey = reader.Read(32);
			ReadOnlyMemory<byte> randomSeed = reader.Read(32);
			ReadOnlyMemory<byte> vmacs = reader.Read(64);
			ReadOnlyMemory<byte> zkproof = reader.Read(192);
			ReadOnlyMemory<byte> encCiphertexts = reader.Read(length: 1202);

			return new(
				vpub_old,
				vpub_new,
				anchor,
				nullifiers,
				commitments,
				ephemeralKey,
				randomSeed,
				vmacs,
				zkproof,
				encCiphertexts);
		}
	}

	/// <summary>
	/// Describes a spend input from the Sapling pool created by a transaction.
	/// </summary>
	/// <remarks>
	/// As described in <see href="https://zips.z.cash/protocol/protocol.pdf">the Zcash protocol</see>, §7.3.
	/// </remarks>
	public readonly record struct SaplingSpendDescription
	{
		private readonly ReadOnlyMemory<byte> cv;
		private readonly ReadOnlyMemory<byte> anchor;
		private readonly ReadOnlyMemory<byte> nullifier;
		private readonly ReadOnlyMemory<byte> rk;
		private readonly ReadOnlyMemory<byte> zkproof;
		private readonly ReadOnlyMemory<byte> spendAuthSig;

		/// <summary>
		/// Initializes a new instance of the <see cref="SaplingSpendDescription"/> class
		/// for a v5 transaction.
		/// </summary>
		/// <inheritdoc cref="SaplingSpendDescription(ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, ReadOnlyMemory{byte})"/>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffers provided remain the same.
		/// </remarks>
		public SaplingSpendDescription(ReadOnlyMemory<byte> cv, ReadOnlyMemory<byte> nullifier, ReadOnlyMemory<byte> rk)
		{
			SharedCryptoUtilities.CheckLength(cv.Span, 32);
			SharedCryptoUtilities.CheckLength(nullifier.Span, 32);
			SharedCryptoUtilities.CheckLength(rk.Span, 32);

			this.TransactionVersion = 5;
			this.cv = cv;
			this.nullifier = nullifier;
			this.rk = rk;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SaplingSpendDescription"/> class
		/// for a v4 transaction.
		/// </summary>
		/// <param name="cv">The 32-byte value commitment.</param>
		/// <param name="anchor">The 32-byte root of the Sapling note commitment tree at some block height in the past.</param>
		/// <param name="nullifier">The 32-byte nullifier.</param>
		/// <param name="rk">The 32-byte validating key.</param>
		/// <param name="zkproof">The 192-byte encoding of the zk-SNARK proof.</param>
		/// <param name="spendAuthSig">The 64-byte signature authorizing this spend.</param>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffers provided remain the same.
		/// </remarks>
		public SaplingSpendDescription(ReadOnlyMemory<byte> cv, ReadOnlyMemory<byte> anchor, ReadOnlyMemory<byte> nullifier, ReadOnlyMemory<byte> rk, ReadOnlyMemory<byte> zkproof, ReadOnlyMemory<byte> spendAuthSig)
		{
			SharedCryptoUtilities.CheckLength(cv.Span, 32);
			SharedCryptoUtilities.CheckLength(nullifier.Span, 32);
			SharedCryptoUtilities.CheckLength(rk.Span, 32);
			SharedCryptoUtilities.CheckLength(anchor.Span, 32);
			SharedCryptoUtilities.CheckLength(zkproof.Span, 192);
			SharedCryptoUtilities.CheckLength(spendAuthSig.Span, 64);

			this.TransactionVersion = 4;
			this.cv = cv;
			this.anchor = anchor;
			this.nullifier = nullifier;
			this.rk = rk;
			this.zkproof = zkproof;
			this.spendAuthSig = spendAuthSig;
		}

		/// <summary>
		/// Gets the transaction version this value is configured for.
		/// </summary>
		public uint TransactionVersion { get; }

		/// <summary>
		/// Gets a value commitment to the value of the input note.
		/// </summary>
		public readonly ReadOnlyMemory<byte> ValueCommitment => this.cv;

		/// <summary>
		/// Gets the nullifier of the input note.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Nullifier => this.nullifier;

		/// <summary>
		/// Gets the randomized validating key for the element of <c>spendAuthSigsSapling</c> corresponding to this Spend.
		/// </summary>
		public readonly ReadOnlyMemory<byte> ValidatingKey => this.rk;

		/// <summary>
		/// Gets the root of the Sapling note commitment tree at some block height in the past.
		/// </summary>
		/// <remarks>
		/// Only applicable when <see cref="TransactionVersion"/> is 4.
		/// </remarks>
		public readonly ReadOnlyMemory<byte> Anchor => this.anchor;

		/// <summary>
		/// Gets an encoding of the zk-SNARK proof.
		/// </summary>
		/// <remarks>
		/// Only applicable when <see cref="TransactionVersion"/> is 4.
		/// </remarks>
		public readonly ReadOnlyMemory<byte> ZkProof => this.zkproof;

		/// <summary>
		/// Gets a signature authorizing this spend.
		/// </summary>
		/// <remarks>
		/// Only applicable when <see cref="TransactionVersion"/> is 4.
		/// </remarks>
		public readonly ReadOnlyMemory<byte> SpendAuthSig => this.spendAuthSig;

		/// <summary>
		/// Decodes a <see cref="SaplingSpendDescription"/>.
		/// </summary>
		/// <param name="reader">The decoding reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being read from.</param>
		/// <returns>The initialized <see cref="SaplingSpendDescription"/>.</returns>
		/// <exception cref="NotSupportedException">Thrown if the <paramref name="transactionVersion"/> is not 4 or 5.</exception>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffer backing the <paramref name="reader"/> remains the same.
		/// </remarks>
		public static SaplingSpendDescription Decode(ref DecodingReader reader, uint transactionVersion)
		{
			if (transactionVersion is > 5 or < 4)
			{
				throw new NotSupportedException(Strings.FormatUnsupportedTransactionVersion(transactionVersion));
			}

			ReadOnlyMemory<byte> cv = reader.Read(32);
			ReadOnlyMemory<byte> anchor = transactionVersion == 4 ? reader.Read(32) : default;
			ReadOnlyMemory<byte> nullifier = reader.Read(32);
			ReadOnlyMemory<byte> rk = reader.Read(32);
			ReadOnlyMemory<byte> zkproof = transactionVersion == 4 ? reader.Read(192) : default;
			ReadOnlyMemory<byte> spendAuthSig = transactionVersion == 4 ? reader.Read(64) : default;

			return transactionVersion == 5
				? new(cv, nullifier, rk)
				: new(cv, anchor, nullifier, rk, zkproof, spendAuthSig);
		}
	}

	/// <summary>
	/// Describes a spend output into the Sapling pool created by a transaction.
	/// </summary>
	/// <remarks>
	/// As described in <see href="https://zips.z.cash/protocol/protocol.pdf">the Zcash protocol</see>, §7.4.
	/// </remarks>
	public readonly record struct SaplingOutputDescription
	{
		private readonly ReadOnlyMemory<byte> cv;
		private readonly ReadOnlyMemory<byte> cmu;
		private readonly ReadOnlyMemory<byte> ephemeralKey;
		private readonly ReadOnlyMemory<byte> encCiphertext;
		private readonly ReadOnlyMemory<byte> outCiphertext;
		private readonly ReadOnlyMemory<byte> zkproof;

		/// <summary>
		/// Initializes a new instance of the <see cref="SaplingOutputDescription"/> class
		/// for a v5 transaction.
		/// </summary>
		/// <inheritdoc cref="SaplingOutputDescription(ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, ReadOnlyMemory{byte}, ReadOnlyMemory{byte})"/>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffers provided remain the same.
		/// </remarks>
		public SaplingOutputDescription(ReadOnlyMemory<byte> cv, ReadOnlyMemory<byte> cmu, ReadOnlyMemory<byte> ephemeralKey, ReadOnlyMemory<byte> encCiphertext, ReadOnlyMemory<byte> outCiphertext)
		{
			SharedCryptoUtilities.CheckLength(cv, 32);
			SharedCryptoUtilities.CheckLength(cmu, 32);
			SharedCryptoUtilities.CheckLength(ephemeralKey, 32);
			SharedCryptoUtilities.CheckLength(encCiphertext, 580);
			SharedCryptoUtilities.CheckLength(outCiphertext, 80);

			this.Version = 5;
			this.cv = cv;
			this.cmu = cmu;
			this.ephemeralKey = ephemeralKey;
			this.encCiphertext = encCiphertext;
			this.outCiphertext = outCiphertext;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="SaplingOutputDescription"/> class
		/// for a v4 transaction.
		/// </summary>
		/// <param name="cv">A value commitment to the net value of the output note.</param>
		/// <param name="cmu">The u-coordinate of the note commitment for the output note.</param>
		/// <param name="ephemeralKey">An encoding of an ephemeral Jubjub public key.</param>
		/// <param name="encCiphertext">The encrypted contents of the note plaintext.</param>
		/// <param name="outCiphertext">The encrypted contents of the byte string created by concatenation of the transmission key with the ephemeral secret key.</param>
		/// <param name="zkproof">An encoding of the zk-SNARK proof.</param>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffers provided remain the same.
		/// </remarks>
		public SaplingOutputDescription(ReadOnlyMemory<byte> cv, ReadOnlyMemory<byte> cmu, ReadOnlyMemory<byte> ephemeralKey, ReadOnlyMemory<byte> encCiphertext, ReadOnlyMemory<byte> outCiphertext, ReadOnlyMemory<byte> zkproof)
		{
			SharedCryptoUtilities.CheckLength(cv, 32);
			SharedCryptoUtilities.CheckLength(cmu, 32);
			SharedCryptoUtilities.CheckLength(ephemeralKey, 32);
			SharedCryptoUtilities.CheckLength(encCiphertext, 580);
			SharedCryptoUtilities.CheckLength(outCiphertext, 80);
			SharedCryptoUtilities.CheckLength(zkproof, 192);

			this.Version = 5;
			this.cv = cv;
			this.cmu = cmu;
			this.ephemeralKey = ephemeralKey;
			this.encCiphertext = encCiphertext;
			this.outCiphertext = outCiphertext;
			this.zkproof = zkproof;
		}

		/// <summary>
		/// Gets the transaction version this value is configured for.
		/// </summary>
		public uint Version { get; }

		/// <summary>
		/// Gets a value commitment to the net value of the output note.
		/// </summary>
		public readonly ReadOnlyMemory<byte> ValueCommitment => this.cv;

		/// <summary>
		/// Gets the u-coordinate of the note commitment for the output note.
		/// </summary>
		public readonly ReadOnlyMemory<byte> NoteCommitmentUCoord => this.cmu;

		/// <summary>
		/// Gets an encoding of an ephemeral Jubjub public key.
		/// </summary>
		public readonly ReadOnlyMemory<byte> EphemeralKey => this.ephemeralKey;

		/// <summary>
		/// Gets the encrypted contents of the note plaintext.
		/// </summary>
		public readonly ReadOnlyMemory<byte> EncryptedCiphertext => this.encCiphertext;

		/// <summary>
		/// Gets the encrypted contents of the byte string created by concatenation of the transmission key with the ephemeral secret key.
		/// </summary>
		public readonly ReadOnlyMemory<byte> OutCiphertext => this.outCiphertext;

		/// <summary>
		/// Gets an encoding of the zk-SNARK proof.
		/// </summary>
		/// <remarks>
		/// This property only applies when <see cref="Version"/> is 4.
		/// </remarks>
		public readonly ReadOnlyMemory<byte> ZkProof => this.zkproof;

		/// <summary>
		/// Decodes a <see cref="SaplingOutputDescription"/>.
		/// </summary>
		/// <param name="reader">The decoding reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being read from.</param>
		/// <returns>The initialized <see cref="SaplingOutputDescription"/>.</returns>
		/// <exception cref="NotSupportedException">Thrown if the <paramref name="transactionVersion"/> is not 4 or 5.</exception>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffer backing the <paramref name="reader"/> remains the same.
		/// </remarks>
		public static SaplingOutputDescription Decode(ref DecodingReader reader, uint transactionVersion)
		{
			if (transactionVersion is > 5 or < 4)
			{
				throw new NotSupportedException(Strings.FormatUnsupportedTransactionVersion(transactionVersion));
			}

			ReadOnlyMemory<byte> cv = reader.Read(32);
			ReadOnlyMemory<byte> cmu = reader.Read(32);
			ReadOnlyMemory<byte> ephemeralKey = reader.Read(32);
			ReadOnlyMemory<byte> encCiphertext = reader.Read(580);
			ReadOnlyMemory<byte> outCiphertext = reader.Read(80);
			ReadOnlyMemory<byte> zkproof = transactionVersion == 4 ? reader.Read(192) : default;

			return transactionVersion == 5
				? new(cv, cmu, ephemeralKey, encCiphertext, outCiphertext)
				: new(cv, cmu, ephemeralKey, encCiphertext, outCiphertext, zkproof);
		}
	}

	/// <summary>
	/// An Orchard Action.
	/// </summary>
	/// <remarks>
	/// As described in <see href="https://zips.z.cash/protocol/protocol.pdf">the Zcash protocol</see>, §7.5.
	/// </remarks>
	public readonly record struct OrchardAction
	{
		private readonly ReadOnlyMemory<byte> cv;
		private readonly ReadOnlyMemory<byte> nullifier;
		private readonly ReadOnlyMemory<byte> rk;
		private readonly ReadOnlyMemory<byte> cmx;
		private readonly ReadOnlyMemory<byte> ephemeralKey;
		private readonly ReadOnlyMemory<byte> encCiphertext;
		private readonly ReadOnlyMemory<byte> outCiphertext;

		/// <summary>
		/// Initializes a new instance of the <see cref="OrchardAction"/> class.
		/// </summary>
		/// <param name="cv">A value commitment to the net value of the input note minus the output note.</param>
		/// <param name="nullifier">The nullifier of the input note.</param>
		/// <param name="rk">A randomized validating key for spendAuthSig.</param>
		/// <param name="cmx">The x-coordinate of the note commitment for the output note.</param>
		/// <param name="ephemeralKey">An encoding of an ephemeral Pallas public key.</param>
		/// <param name="encCiphertext">A ciphertext component for the encrypted output note.</param>
		/// <param name="outCiphertext">A ciphertext component that allows the holder of the outgoing cipher key (which can be derived from a full viewing key) to recover the recipient diversified transmission key pk_d and the ephemeral private key <c>esk</c>, hence the entire note plaintext.</param>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffers provided remain the same.
		/// </remarks>
		public OrchardAction(ReadOnlyMemory<byte> cv, ReadOnlyMemory<byte> nullifier, ReadOnlyMemory<byte> rk, ReadOnlyMemory<byte> cmx, ReadOnlyMemory<byte> ephemeralKey, ReadOnlyMemory<byte> encCiphertext, ReadOnlyMemory<byte> outCiphertext)
		{
			SharedCryptoUtilities.CheckLength(cv, 32);
			SharedCryptoUtilities.CheckLength(nullifier, 32);
			SharedCryptoUtilities.CheckLength(rk, 32);
			SharedCryptoUtilities.CheckLength(cmx, 32);
			SharedCryptoUtilities.CheckLength(ephemeralKey, 32);
			SharedCryptoUtilities.CheckLength(encCiphertext, 580);
			SharedCryptoUtilities.CheckLength(outCiphertext, 80);

			this.cv = cv;
			this.nullifier = nullifier;
			this.rk = rk;
			this.cmx = cmx;
			this.ephemeralKey = ephemeralKey;
			this.encCiphertext = encCiphertext;
			this.outCiphertext = outCiphertext;
		}

		/// <summary>
		/// Gets a value commitment to the net value of the input note minus the output note.
		/// </summary>
		public readonly ReadOnlyMemory<byte> ValueCommitment => this.cv;

		/// <summary>
		/// Gets the nullifier of the input note.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Nullifier => this.nullifier;

		/// <summary>
		/// Gets a randomized validating key for spendAuthSig.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Rk => this.rk;

		/// <summary>
		/// Gets the x-coordinate of the note commitment for the output note.
		/// </summary>
		public readonly ReadOnlyMemory<byte> Cmx => this.cmx;

		/// <summary>
		/// Gets an encoding of an ephemeral Pallas public key.
		/// </summary>
		public readonly ReadOnlyMemory<byte> EphemeralKey => this.ephemeralKey;

		/// <summary>
		/// Gets a ciphertext component for the encrypted output note.
		/// </summary>
		public readonly ReadOnlyMemory<byte> EncryptedCiphertext => this.encCiphertext;

		/// <summary>
		/// Gets a ciphertext component that allows the holder of the outgoing cipher key (which can be derived from a full viewing key) to recover the recipient diversified transmission key pk_d and the ephemeral private key esk, hence the entire note plaintext.
		/// </summary>
		public readonly ReadOnlyMemory<byte> OutCiphertext => this.outCiphertext;

		/// <summary>
		/// Decodes an <see cref="OrchardAction"/>.
		/// </summary>
		/// <param name="reader">The reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being decoded.</param>
		/// <returns>The initialized <see cref="OrchardAction"/>.</returns>
		/// <remarks>
		/// This method does not allocate any memory, and will only be valid while the content of the buffer backing the <paramref name="reader"/> remains the same.
		/// </remarks>
		public static OrchardAction Decode(ref DecodingReader reader, uint transactionVersion)
		{
			ReadOnlyMemory<byte> cv = reader.Read(32);
			ReadOnlyMemory<byte> nullifier = reader.Read(32);
			ReadOnlyMemory<byte> rk = reader.Read(32);
			ReadOnlyMemory<byte> cmx = reader.Read(32);
			ReadOnlyMemory<byte> ephemeralKey = reader.Read(32);
			ReadOnlyMemory<byte> encCiphertext = reader.Read(580);
			ReadOnlyMemory<byte> outCiphertext = reader.Read(80);

			return new(cv, nullifier, rk, cmx, ephemeralKey, encCiphertext, outCiphertext);
		}
	}

	/// <summary>
	/// An alloc-free enumerator of lists in the transaction.
	/// </summary>
	/// <typeparam name="T">The type of element that is enumerated.</typeparam>
	public struct DescriptionEnumerator<T> : IEnumerator<T>
		where T : struct
	{
		private readonly DecodingReader start;
		private readonly int count;
		private readonly DescriptionReader<T> reader;
		private readonly uint transactionVersion;
		private DecodingReader currentReader;
		private int currentIndex;

		/// <summary>
		/// Initializes a new instance of the <see cref="DescriptionEnumerator{T}"/> struct.
		/// </summary>
		/// <param name="start">The reader, positioned at the first element.</param>
		/// <param name="count">The number of elements in the sequence.</param>
		/// <param name="reader">The delegate to decode each element.</param>
		/// <param name="transactionVersion">The transaction version.</param>
		internal DescriptionEnumerator(DecodingReader start, int count, DescriptionReader<T> reader, uint transactionVersion)
		{
			this.start = start;
			this.currentReader = start;
			this.count = count;
			this.reader = reader;
			this.transactionVersion = transactionVersion;
		}

		/// <inheritdoc/>
		public T Current { get; set; }

		/// <inheritdoc/>
		object IEnumerator.Current => this.Current;

		/// <summary>
		/// Gets the number of elements in the sequence.
		/// </summary>
		public int Count => this.count;

		/// <inheritdoc/>
		public void Dispose()
		{
		}

		/// <inheritdoc/>
		public bool MoveNext()
		{
			if (this.currentIndex < this.count)
			{
				this.Current = this.reader(ref this.currentReader, this.transactionVersion);
				this.currentIndex++;
				return true;
			}

			return false;
		}

		/// <inheritdoc/>
		public void Reset()
		{
			this.currentReader = this.start;
			this.currentIndex = 0;
			this.Current = default;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DescriptionEnumerator{T}"/> struct
		/// by reading the compact count of elements and fast forwarding past all elements for the caller.
		/// </summary>
		/// <param name="reader">The reader to use.</param>
		/// <param name="itemReader">The delegate that reads one element.</param>
		/// <param name="transactionVersion">The transaction version.</param>
		/// <returns>The initialized enumerator.</returns>
		internal static DescriptionEnumerator<T> Initialize(ref DecodingReader reader, DescriptionReader<T> itemReader, uint transactionVersion)
		{
			int count = reader.ReadInt32Compact();

			// Initialize the enumerator at the start of the sequence.
			DescriptionEnumerator<T> result = new(reader, count, itemReader, transactionVersion);

			// Fast forward the reader to the end so our caller can proceed to skip over the whole sequence.
			while (result.MoveNext())
			{
			}

			// Set our ref argument so our caller knows where the end of the sequence is.
			reader = result.currentReader;

			// Now reset our enumerator to the start of the sequence so it's ready for use.
			result.Reset();
			return result;
		}
	}
}
