// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using NBitcoin.Secp256k1;
using Nerdbank.Zcash.FixedLengthStructs;
using static Nerdbank.Bitcoin.RawTransaction;
using static Nerdbank.Zcash.Zip32HDWallet;

namespace Nerdbank.Zcash;

/// <summary>
/// Facilitates transaction format encoding.
/// </summary>
/// <remarks>
/// The encoding is as per <see href="https://zips.z.cash/zip-0225">ZIP-225</see> and
/// section 7 of the <see href="https://zips.z.cash/protocol/protocol.pdf">Zcash protocol</see>.
/// </remarks>
public record RawTransaction
{
	private const uint HeaderVersionMask = 0x7fffffff;

	private const uint HeaderOverwinteredMask = 0x80000000;

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
	/// <returns>The decoded transaction.</returns>
	/// <exception cref="NotSupportedException">Thrown if the transaction version exceeds the supported version range.</exception>
	public static RawTransaction Decode(ReadOnlySpan<byte> bytes)
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
		ReadOnlyMemory<SaplingSpendDescription> spendsSapling = default;
		ReadOnlyMemory<SaplingOutputDescription> outputsSapling = default;
		if (version == 4)
		{
			valueBalanceSapling = reader.ReadInt64LE();
		}

		if (version >= 4)
		{
			spendsSapling = ReadSaplingSpendDescription(ref reader, reader.ReadUInt64Compact(), version);
			outputsSapling = ReadSaplingOutputDescription(ref reader, reader.ReadUInt64Compact(), version);
		}

		if (version == 5 && spendsSapling.Length + outputsSapling.Length > 0)
		{
			valueBalanceSapling = reader.ReadInt64LE();
		}

		ReadOnlySpan<byte> anchorSapling = default;
		ReadOnlySpan<byte> spendProofsSapling = default;
		ReadOnlySpan<byte> spendAuthSigsSapling = default;
		ReadOnlySpan<byte> outputProofsSapling = default;
		ReadOnlyMemory<JSDescriptionBCTV14> joinSplitsBCTV14 = default;
		ReadOnlyMemory<JSDescriptionGroth16> joinSplitsGroth16 = default;
		ReadOnlySpan<byte> joinSplitPubKey = default;
		ReadOnlySpan<byte> joinSplitSig = default;
		if (version == 5)
		{
			if (spendsSapling.Length > 0)
			{
				anchorSapling = reader.Read(32);
			}

			spendProofsSapling = reader.Read(192 * spendsSapling.Length);
			spendAuthSigsSapling = reader.Read(64 * spendsSapling.Length);
			outputProofsSapling = reader.Read(192 * outputsSapling.Length);
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

		ReadOnlySpan<byte> bindingSigSapling = default;
		if (version >= 4 && spendsSapling.Length + outputsSapling.Length > 0)
		{
			bindingSigSapling = reader.Read(64);
		}

		ReadOnlyMemory<OrchardAction> actionsOrchard = default;
		OrchardFlags flagsOrchard = default;
		long valueBalanceOrchard = 0;
		ReadOnlySpan<byte> anchorOrchard = default;
		ReadOnlySpan<byte> proofsOrchard = default;
		ReadOnlySpan<byte> spendAuthSigsOrchard = default;
		ReadOnlySpan<byte> bindingSigOrchard = default;
		if (version == 5)
		{
			actionsOrchard = ReadOrchardAction(ref reader, reader.ReadUInt64Compact(), version);
			if (actionsOrchard.Length > 0)
			{
				flagsOrchard = (OrchardFlags)reader.ReadByte();
				valueBalanceOrchard = reader.ReadInt64LE();
				anchorOrchard = reader.Read(32);
				int sizeProofsOrchard = reader.ReadInt32Compact();
				proofsOrchard = reader.Read(sizeProofsOrchard);
				spendAuthSigsOrchard = reader.Read(64 * actionsOrchard.Length);
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

	private static ReadOnlyMemory<SaplingSpendDescription> ReadSaplingSpendDescription(ref DecodingReader reader, ulong count, uint version)
	{
		if (count == 0)
		{
			return default;
		}

		SaplingSpendDescription[] array = new SaplingSpendDescription[count];
		for (ulong i = 0; i < count; i++)
		{
			array[i] = SaplingSpendDescription.Decode(ref reader, version);
		}

		return array;
	}

	private static ReadOnlyMemory<SaplingOutputDescription> ReadSaplingOutputDescription(ref DecodingReader reader, ulong count, uint version)
	{
		if (count == 0)
		{
			return default;
		}

		SaplingOutputDescription[] array = new SaplingOutputDescription[count];
		for (ulong i = 0; i < count; i++)
		{
			array[i] = SaplingOutputDescription.Decode(ref reader, version);
		}

		return array;
	}

	private static ReadOnlyMemory<OrchardAction> ReadOrchardAction(ref DecodingReader reader, ulong count, uint version)
	{
		if (count == 0)
		{
			return default;
		}

		OrchardAction[] array = new OrchardAction[count];
		for (ulong i = 0; i < count; i++)
		{
			array[i] = OrchardAction.Decode(ref reader, version);
		}

		return array;
	}

	/// <summary>
	/// The Transparent-related fields in a transaction.
	/// </summary>
	public record struct TransparentFields
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
	public record struct SproutFields
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
	public record struct SaplingFields
	{
		/// <summary>
		/// Gets the net value of Sapling spends minus outputs.
		/// </summary>
		public required long ValueBalance { get; init; }

		/// <summary>
		/// Gets a sequence of spend descriptions.
		/// </summary>
		public required ReadOnlyMemory<SaplingSpendDescription> Spends { get; init; }

		/// <summary>
		/// Gets a sequence of output descriptions.
		/// </summary>
		public required ReadOnlyMemory<SaplingOutputDescription> Outputs { get; init; }

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
	public record struct OrchardFields
	{
		/// <summary>
		/// Gets a sequence of Orchard Action descriptions.
		/// </summary>
		public required ReadOnlyMemory<OrchardAction> Actions { get; init; }

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
	public record struct JSDescriptionBCTV14
	{
		private readonly ulong vpubOld;
		private readonly ulong vpubNew;
		private readonly Bytes32 anchor;
		private readonly Bytes64 nullifiers;
		private readonly Bytes64 commitments;
		private readonly Bytes32 ephemeralKey;
		private readonly Bytes32 randomSeed;
		private readonly Bytes64 vmacs;
		private readonly Bytes296 zkproof;
		private readonly Bytes1202 encCiphertexts;

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
		public JSDescriptionBCTV14(
			ulong vpub_old,
			ulong vpub_new,
			ReadOnlySpan<byte> anchor,
			ReadOnlySpan<byte> nullifiers,
			ReadOnlySpan<byte> commitments,
			ReadOnlySpan<byte> ephemeralKey,
			ReadOnlySpan<byte> randomSeed,
			ReadOnlySpan<byte> vmacs,
			ReadOnlySpan<byte> zkproof,
			ReadOnlySpan<byte> encCiphertexts)
		{
			this.vpubOld = vpub_old;
			this.vpubNew = vpub_new;
			this.anchor = new(anchor);
			this.nullifiers = new(nullifiers);
			this.commitments = new(commitments);
			this.ephemeralKey = new(ephemeralKey);
			this.randomSeed = new(randomSeed);
			this.vmacs = new(vmacs);
			this.zkproof = new(zkproof);
			this.encCiphertexts = new(encCiphertexts);
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
		[UnscopedRef]
		public ReadOnlySpan<byte> Anchor => this.anchor.Value;

		/// <summary>
		/// Gets a sequence of nullifiers of the input notes.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Nullifiers => this.nullifiers.Value;

		/// <summary>
		/// Gets a sequence of note commitments for the output notes.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Commitments => this.commitments.Value;

		/// <summary>
		/// Gets a Curve25519 public key.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> EphemeralKey => this.ephemeralKey.Value;

		/// <summary>
		/// Gets a 256-bit seed that must be chosen independently at random for each JoinSplit description.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> RandomSeed => this.randomSeed.Value;

		/// <summary>
		/// Gets a sequence of message authentication tags h_sig 1..N old binding hSig to each ask of the JoinSplit description.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Vmacs => this.vmacs.Value;

		/// <summary>
		/// Gets an encoding of the zk-SNARK proof.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Zkproof => this.zkproof.Value;

		/// <summary>
		/// Gets a sequence of ciphertext components for the encrypted output notes.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> EncCiphertexts => this.encCiphertexts.Value;

		/// <summary>
		/// Decodes a <see cref="JSDescriptionBCTV14"/>.
		/// </summary>
		/// <param name="reader">The reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being decoded.</param>
		/// <returns>The initialized value.</returns>
		/// <exception cref="NotSupportedException">Throw if the transaction version doesn't support this value.</exception>
		public static JSDescriptionBCTV14 Decode(ref DecodingReader reader, uint transactionVersion)
		{
			if (transactionVersion is not (2 or 3))
			{
				throw new NotSupportedException(Strings.FormatUnsupportedTransactionVersion(transactionVersion));
			}

			ulong vpub_old = reader.ReadUInt64LE();
			ulong vpub_new = reader.ReadUInt64LE();
			ReadOnlySpan<byte> anchor = reader.Read(32);
			ReadOnlySpan<byte> nullifiers = reader.Read(64);
			ReadOnlySpan<byte> commitments = reader.Read(64);
			ReadOnlySpan<byte> ephemeralKey = reader.Read(32);
			ReadOnlySpan<byte> randomSeed = reader.Read(32);
			ReadOnlySpan<byte> vmacs = reader.Read(64);
			ReadOnlySpan<byte> zkproof = reader.Read(296);
			ReadOnlySpan<byte> encCiphertexts = reader.Read(1202);

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
	public record struct JSDescriptionGroth16
	{
		private readonly ulong vpubOld;
		private readonly ulong vpubNew;
		private readonly Bytes32 anchor;
		private readonly Bytes32 nullifiers;
		private readonly Bytes32 commitments;
		private readonly Bytes32 ephemeralKey;
		private readonly Bytes32 randomSeed;
		private readonly Bytes32 vmacs;
		private readonly Bytes192 zkproof;
		private readonly Bytes1202 encCiphertexts;

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
			ReadOnlySpan<byte> anchor,
			ReadOnlySpan<byte> nullifiers,
			ReadOnlySpan<byte> commitments,
			ReadOnlySpan<byte> ephemeralKey,
			ReadOnlySpan<byte> randomSeed,
			ReadOnlySpan<byte> vmacs,
			ReadOnlySpan<byte> zkproof,
			ReadOnlySpan<byte> encCiphertexts)
		{
			this.vpubOld = vpub_old;
			this.vpubNew = vpub_new;
			this.anchor = new(anchor);
			this.nullifiers = new(nullifiers);
			this.commitments = new(commitments);
			this.ephemeralKey = new(ephemeralKey);
			this.randomSeed = new(randomSeed);
			this.vmacs = new(vmacs);
			this.zkproof = new(zkproof);
			this.encCiphertexts = new(encCiphertexts);
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
		[UnscopedRef]
		public ReadOnlySpan<byte> Anchor => this.anchor.Value;

		/// <summary>
		/// Gets a sequence of nullifiers of the input notes.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Nullifiers => this.nullifiers.Value;

		/// <summary>
		/// Gets a sequence of note commitments for the output notes.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Commitments => this.commitments.Value;

		/// <summary>
		/// Gets a Curve25519 public key.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> EphemeralKey => this.ephemeralKey.Value;

		/// <summary>
		/// Gets a 256-bit seed that must be chosen independently at random for each JoinSplit description.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> RandomSeed => this.randomSeed.Value;

		/// <summary>
		/// Gets a sequence of message authentication tags h_sig 1..N old binding hSig to each ask of the JoinSplit description.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Vmacs => this.vmacs.Value;

		/// <summary>
		/// Gets an encoding of the zk-SNARK proof.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Zkproof => this.zkproof.Value;

		/// <summary>
		/// Gets a sequence of ciphertext components for the encrypted output notes.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> EncCiphertexts => this.encCiphertexts.Value;

		/// <summary>
		/// Decodes a <see cref="JSDescriptionGroth16"/>.
		/// </summary>
		/// <param name="reader">The reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being decoded.</param>
		/// <returns>The initialized value.</returns>
		/// <exception cref="NotSupportedException">Throw if the transaction version doesn't support this value.</exception>
		public static JSDescriptionGroth16 Decode(ref DecodingReader reader, uint transactionVersion)
		{
			if (transactionVersion != 4)
			{
				throw new NotSupportedException(Strings.FormatUnsupportedTransactionVersion(transactionVersion));
			}

			ulong vpub_old = reader.ReadUInt64LE();
			ulong vpub_new = reader.ReadUInt64LE();
			ReadOnlySpan<byte> anchor = reader.Read(32);
			ReadOnlySpan<byte> nullifiers = reader.Read(64);
			ReadOnlySpan<byte> commitments = reader.Read(64);
			ReadOnlySpan<byte> ephemeralKey = reader.Read(32);
			ReadOnlySpan<byte> randomSeed = reader.Read(32);
			ReadOnlySpan<byte> vmacs = reader.Read(64);
			ReadOnlySpan<byte> zkproof = reader.Read(192);
			ReadOnlySpan<byte> encCiphertexts = reader.Read(length: 1202);

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
	public record struct SaplingSpendDescription
	{
		private readonly Bytes32 cv;
		private readonly Bytes32 anchor;
		private readonly Bytes32 nullifier;
		private readonly Bytes32 rk;
		private readonly Bytes192 zkproof;
		private readonly Bytes64 spendAuthSig;

		/// <summary>
		/// Initializes a new instance of the <see cref="SaplingSpendDescription"/> class
		/// for a v5 transaction.
		/// </summary>
		/// <inheritdoc cref="SaplingSpendDescription(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
		public SaplingSpendDescription(ReadOnlySpan<byte> cv, ReadOnlySpan<byte> nullifier, ReadOnlySpan<byte> rk)
		{
			this.TransactionVersion = 5;
			this.cv = new(cv);
			this.nullifier = new(nullifier);
			this.rk = new(rk);
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
		public SaplingSpendDescription(ReadOnlySpan<byte> cv, ReadOnlySpan<byte> anchor, ReadOnlySpan<byte> nullifier, ReadOnlySpan<byte> rk, ReadOnlySpan<byte> zkproof, ReadOnlySpan<byte> spendAuthSig)
		{
			this.TransactionVersion = 4;
			this.cv = new(cv);
			this.anchor = new(anchor);
			this.nullifier = new(nullifier);
			this.rk = new(rk);
			this.zkproof = new(zkproof);
			this.spendAuthSig = new(spendAuthSig);
		}

		/// <summary>
		/// Gets the transaction version this value is configured for.
		/// </summary>
		public uint TransactionVersion { get; }

		/// <summary>
		/// Gets a value commitment to the value of the input note.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> ValueCommitment => this.cv.Value;

		/// <summary>
		/// Gets the nullifier of the input note.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Nullifier => this.nullifier.Value;

		/// <summary>
		/// Gets the randomized validating key for the element of <c>spendAuthSigsSapling</c> corresponding to this Spend.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> ValidatingKey => this.rk.Value;

		/// <summary>
		/// Gets the root of the Sapling note commitment tree at some block height in the past.
		/// </summary>
		/// <remarks>
		/// Only applicable when <see cref="TransactionVersion"/> is 4.
		/// </remarks>
		[UnscopedRef]
		public ReadOnlySpan<byte> Anchor => this.TransactionVersion == 4 ? this.anchor.Value : default;

		/// <summary>
		/// Gets an encoding of the zk-SNARK proof.
		/// </summary>
		/// <remarks>
		/// Only applicable when <see cref="TransactionVersion"/> is 4.
		/// </remarks>
		[UnscopedRef]
		public ReadOnlySpan<byte> ZkProof => this.TransactionVersion == 4 ? this.zkproof.Value : default;

		/// <summary>
		/// Gets a signature authorizing this spend.
		/// </summary>
		/// <remarks>
		/// Only applicable when <see cref="TransactionVersion"/> is 4.
		/// </remarks>
		[UnscopedRef]
		public ReadOnlySpan<byte> SpendAuthSig => this.TransactionVersion == 4 ? this.spendAuthSig.Value : default;

		/// <summary>
		/// Decodes a <see cref="SaplingSpendDescription"/>.
		/// </summary>
		/// <param name="reader">The decoding reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being read from.</param>
		/// <returns>The initialized <see cref="SaplingSpendDescription"/>.</returns>
		/// <exception cref="NotSupportedException">Thrown if the <paramref name="transactionVersion"/> is not 4 or 5.</exception>
		public static SaplingSpendDescription Decode(ref DecodingReader reader, uint transactionVersion)
		{
			if (transactionVersion is > 5 or < 4)
			{
				throw new NotSupportedException(Strings.FormatUnsupportedTransactionVersion(transactionVersion));
			}

			ReadOnlySpan<byte> cv = reader.Read(32);
			ReadOnlySpan<byte> anchor = transactionVersion == 4 ? reader.Read(32) : default;
			ReadOnlySpan<byte> nullifier = reader.Read(32);
			ReadOnlySpan<byte> rk = reader.Read(32);
			ReadOnlySpan<byte> zkproof = transactionVersion == 4 ? reader.Read(192) : default;
			ReadOnlySpan<byte> spendAuthSig = transactionVersion == 4 ? reader.Read(64) : default;

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
	public record struct SaplingOutputDescription
	{
		private readonly Bytes32 cv;
		private readonly Bytes32 cmu;
		private readonly Bytes32 ephemeralKey;
		private readonly Bytes580 encCiphertext;
		private readonly Bytes80 outCiphertext;
		private readonly Bytes192 zkproof;

		/// <summary>
		/// Initializes a new instance of the <see cref="SaplingOutputDescription"/> class
		/// for a v5 transaction.
		/// </summary>
		/// <inheritdoc cref="SaplingOutputDescription(ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte}, ReadOnlySpan{byte})"/>
		public SaplingOutputDescription(ReadOnlySpan<byte> cv, ReadOnlySpan<byte> cmu, ReadOnlySpan<byte> ephemeralKey, ReadOnlySpan<byte> encCiphertext, ReadOnlySpan<byte> outCiphertext)
		{
			this.Version = 5;
			this.cv = new(cv);
			this.cmu = new(cmu);
			this.ephemeralKey = new(ephemeralKey);
			this.encCiphertext = new(encCiphertext);
			this.outCiphertext = new(outCiphertext);
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
		public SaplingOutputDescription(ReadOnlySpan<byte> cv, ReadOnlySpan<byte> cmu, ReadOnlySpan<byte> ephemeralKey, ReadOnlySpan<byte> encCiphertext, ReadOnlySpan<byte> outCiphertext, ReadOnlySpan<byte> zkproof)
		{
			this.Version = 5;
			this.cv = new(cv);
			this.cmu = new(cmu);
			this.ephemeralKey = new(ephemeralKey);
			this.encCiphertext = new(encCiphertext);
			this.outCiphertext = new(outCiphertext);
			this.zkproof = new(zkproof);
		}

		/// <summary>
		/// Gets the transaction version this value is configured for.
		/// </summary>
		public uint Version { get; }

		/// <summary>
		/// Gets a value commitment to the net value of the output note.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> ValueCommitment => this.cv.Value;

		/// <summary>
		/// Gets the u-coordinate of the note commitment for the output note.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> NoteCommitmentUCoord => this.cmu.Value;

		/// <summary>
		/// Gets an encoding of an ephemeral Jubjub public key.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> EphemeralKey => this.ephemeralKey.Value;

		/// <summary>
		/// Gets the encrypted contents of the note plaintext.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> EncryptedCiphertext => this.encCiphertext.Value;

		/// <summary>
		/// Gets the encrypted contents of the byte string created by concatenation of the transmission key with the ephemeral secret key.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> OutCiphertext => this.outCiphertext.Value;

		/// <summary>
		/// Gets an encoding of the zk-SNARK proof.
		/// </summary>
		/// <remarks>
		/// This property only applies when <see cref="Version"/> is 4.
		/// </remarks>
		[UnscopedRef]
		public ReadOnlySpan<byte> ZkProof => this.Version == 4 ? this.zkproof.Value : default;

		/// <summary>
		/// Decodes a <see cref="SaplingOutputDescription"/>.
		/// </summary>
		/// <param name="reader">The decoding reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being read from.</param>
		/// <returns>The initialized <see cref="SaplingOutputDescription"/>.</returns>
		/// <exception cref="NotSupportedException">Thrown if the <paramref name="transactionVersion"/> is not 4 or 5.</exception>
		public static SaplingOutputDescription Decode(ref DecodingReader reader, uint transactionVersion)
		{
			if (transactionVersion is > 5 or < 4)
			{
				throw new NotSupportedException(Strings.FormatUnsupportedTransactionVersion(transactionVersion));
			}

			ReadOnlySpan<byte> cv = reader.Read(32);
			ReadOnlySpan<byte> cmu = reader.Read(32);
			ReadOnlySpan<byte> ephemeralKey = reader.Read(32);
			ReadOnlySpan<byte> encCiphertext = reader.Read(580);
			ReadOnlySpan<byte> outCiphertext = reader.Read(80);
			ReadOnlySpan<byte> zkproof = transactionVersion == 4 ? reader.Read(192) : default;

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
	public record struct OrchardAction
	{
		private Bytes32 cv;
		private Bytes32 nullifier;
		private Bytes32 rk;
		private Bytes32 cmx;
		private Bytes32 ephemeralKey;
		private Bytes580 encCiphertext;
		private Bytes80 outCiphertext;

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
		public OrchardAction(ReadOnlySpan<byte> cv, ReadOnlySpan<byte> nullifier, ReadOnlySpan<byte> rk, ReadOnlySpan<byte> cmx, ReadOnlySpan<byte> ephemeralKey, ReadOnlySpan<byte> encCiphertext, ReadOnlySpan<byte> outCiphertext)
		{
			this.cv = new(cv);
			this.nullifier = new(nullifier);
			this.rk = new(rk);
			this.cmx = new(cmx);
			this.ephemeralKey = new(ephemeralKey);
			this.encCiphertext = new(encCiphertext);
			this.outCiphertext = new(outCiphertext);
		}

		/// <summary>
		/// Gets a value commitment to the net value of the input note minus the output note.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> ValueCommitment => this.cv.Value;

		/// <summary>
		/// Gets the nullifier of the input note.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Nullifier => this.nullifier.Value;

		/// <summary>
		/// Gets a randomized validating key for spendAuthSig.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Rk => this.rk.Value;

		/// <summary>
		/// Gets the x-coordinate of the note commitment for the output note.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> Cmx => this.cmx.Value;

		/// <summary>
		/// Gets an encoding of an ephemeral Pallas public key.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> EphemeralKey => this.ephemeralKey.Value;

		/// <summary>
		/// Gets a ciphertext component for the encrypted output note.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> EncryptedCiphertext => this.encCiphertext.Value;

		/// <summary>
		/// Gets a ciphertext component that allows the holder of the outgoing cipher key (which can be derived from a full viewing key) to recover the recipient diversified transmission key pk_d and the ephemeral private key esk, hence the entire note plaintext.
		/// </summary>
		[UnscopedRef]
		public ReadOnlySpan<byte> OutCiphertext => this.outCiphertext.Value;

		/// <summary>
		/// Decodes an <see cref="OrchardAction"/>.
		/// </summary>
		/// <param name="reader">The reader to use.</param>
		/// <param name="transactionVersion">The version of the transaction being decoded.</param>
		/// <returns>The initialized <see cref="OrchardAction"/>.</returns>
		public static OrchardAction Decode(ref DecodingReader reader, uint transactionVersion)
		{
			ReadOnlySpan<byte> cv = reader.Read(32);
			ReadOnlySpan<byte> nullifier = reader.Read(32);
			ReadOnlySpan<byte> rk = reader.Read(32);
			ReadOnlySpan<byte> cmx = reader.Read(32);
			ReadOnlySpan<byte> ephemeralKey = reader.Read(32);
			ReadOnlySpan<byte> encCiphertext = reader.Read(580);
			ReadOnlySpan<byte> outCiphertext = reader.Read(80);

			return new(cv, nullifier, rk, cmx, ephemeralKey, encCiphertext, outCiphertext);
		}
	}
}
