// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

namespace Nerdbank.Zcash;

/// <summary>
/// Facilitates passing Zcash spending keys to the zcash_client_backend crate.
/// </summary>
internal class UnifiedSpendingKey : IEnumerable<ISpendingKey>, ISpendingKey
{
	private const Era CurrentEra = Era.Orchard;

	private readonly IReadOnlyCollection<IUnifiedEncodingElement> spendingKeys;

	private UnifiedSpendingKey(ZcashNetwork network, IReadOnlyCollection<IUnifiedEncodingElement> spendingKeys)
	{
		this.Network = network;
		this.spendingKeys = spendingKeys;
	}

	private enum Era : uint
	{
		/// <summary>
		/// The Orchard era begins at Orchard activation, and will end if a new pool that requires a
		/// change to unified spending keys is introduced.
		/// </summary>
		Orchard = 6,
	}

	private enum BranchId : uint
	{
		Nu5 = 0xc2d6_d0b4,
	}

	/// <inheritdoc/>
	public ZcashNetwork Network { get; }

	/// <summary>
	/// Gets the unified full viewing key.
	/// </summary>
	public UnifiedViewingKey.Full FullViewingKey => throw new NotImplementedException();

	/// <inheritdoc/>
	IFullViewingKey ISpendingKey.FullViewingKey => this.FullViewingKey;

	/// <summary>
	/// Gets the unified incoming viewing key.
	/// </summary>
	public UnifiedViewingKey.Incoming IncomingViewingKeys => this.FullViewingKey.IncomingViewingKey;

	/// <inheritdoc/>
	IIncomingViewingKey IFullViewingKey.IncomingViewingKey => this.IncomingViewingKeys;

	////internal UnifiedViewingKey UnifiedFullViewingKey { get; }

	////internal UnifiedViewingKey UnifiedIncomingViewingKey { get; }

	/// <inheritdoc cref="Create(IReadOnlyCollection{ISpendingKey})"/>
	public static UnifiedSpendingKey Create(params ISpendingKey[] spendingKeys) => Create((IReadOnlyCollection<ISpendingKey>)spendingKeys);

	/// <summary>
	/// Constructs a unified spending key from a set of spending keys.
	/// </summary>
	/// <param name="spendingKeys">
	/// <para>
	/// The spending keys to include in the unified spending key.
	/// This must not be empty.
	/// This must not include more than one spending key for a given pool.
	/// </para>
	/// <para>
	/// Supported key types are:
	/// <list type="bullet">
	/// <item><see cref="Orchard.SpendingKey"/></item>
	/// <item><see cref="Zip32HDWallet.Sapling.ExtendedSpendingKey"/></item>
	/// <item><see cref="Zip32HDWallet.Transparent.ExtendedSpendingKey"/></item>
	/// </list>
	/// </para>
	/// </param>
	/// <returns>The unified viewing key.</returns>
	public static UnifiedSpendingKey Create(IReadOnlyCollection<ISpendingKey> spendingKeys)
	{
		Requires.NotNull(spendingKeys);
		Requires.Argument(spendingKeys.Count > 0, nameof(spendingKeys), "Cannot create a unified spending key with no viewing keys.");

		IUnifiedEncodingElement[] elements = new IUnifiedEncodingElement[spendingKeys.Count];
		int index = 0;
		foreach (ISpendingKey key in spendingKeys)
		{
			if (key is IUnifiedEncodingElement unifiedElement)
			{
				elements[index++] = unifiedElement;
			}
			else
			{
				throw new NotSupportedException($"Key {key.GetType()} is not supported in a unified spending key.");
			}
		}

		return new(spendingKeys.First().Network, elements);
	}

	/// <inheritdoc/>
	public IEnumerator<ISpendingKey> GetEnumerator() => this.spendingKeys.Cast<ISpendingKey>().GetEnumerator();

	/// <inheritdoc/>
	IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

	/// <summary>
	/// Deserializes a unified spending key from a given buffer.
	/// </summary>
	/// <param name="buffer">The buffer that contains the unified spending key.</param>
	/// <param name="network">The network the key should operate on.</param>
	/// <returns>The deserialized unified spending key.</returns>
	/// <remarks>
	/// This format is undocumented, but <see href="https://github.com/zcash/librustzcash/blob/b580c42bdc9cb0d0fb06a3975783be1bb34395e8/zcash_client_backend/src/keys.rs#L250-L338">implemented in zcash_client_backend</see>.
	/// </remarks>
	internal static UnifiedSpendingKey FromBytes(ReadOnlySpan<byte> buffer, ZcashNetwork network)
	{
		BranchId branchId = (BranchId)BitUtilities.ReadUInt32LE(buffer);
		Era era = EraFrom(branchId);
		buffer = buffer.Slice(4);

		if (era != CurrentEra)
		{
			throw new InvalidKeyException($"Era was expected to be {CurrentEra} but was {era}.");
		}

		List<IUnifiedEncodingElement> elements = new();
		while (!buffer.IsEmpty)
		{
			byte typeCode = buffer[0];
			buffer = buffer.Slice(1);
			int lengthOfLength = CompactSize.Decode(buffer, out ulong lengthOfElement);
			buffer = buffer.Slice(lengthOfLength);
			ReadOnlySpan<byte> elementContent = buffer[..(int)lengthOfElement];
			buffer = buffer.Slice((int)lengthOfElement);

			IUnifiedEncodingElement? element = typeCode switch
			{
				UnifiedTypeCodes.Orchard => Orchard.SpendingKey.DecodeUnifiedKeyContribution(elementContent, network),
				UnifiedTypeCodes.Sapling => Zip32HDWallet.Sapling.ExtendedSpendingKey.DecodeUnifiedViewingKeyContribution(elementContent, network),
				UnifiedTypeCodes.TransparentP2PKH => Zip32HDWallet.Transparent.ExtendedSpendingKey.DecodeUnifiedKeyContribution(elementContent, network),
				_ => null,
			};

			if (element is not null)
			{
				elements.Add(element);
			}
		}

		if (elements.Count == 0)
		{
			throw new InvalidKeyException("Unified spending key contained 0 recognized keys.");
		}

		return new(network, elements);
	}

	/// <summary>
	/// Writes the binary encoding of the unified spending key.
	/// </summary>
	/// <param name="buffer">The buffer to write to.</param>
	/// <returns>The number of bytes written to the buffer.</returns>
	/// <remarks>
	/// This format is undocumented, but <see href="https://github.com/zcash/librustzcash/blob/b580c42bdc9cb0d0fb06a3975783be1bb34395e8/zcash_client_backend/src/keys.rs#L211-L243">implemented in zcash_client_backend</see>.
	/// </remarks>
	internal int ToBytes(Span<byte> buffer)
	{
		int written = 0;
		written += BitUtilities.WriteLE((uint)BranchFrom(CurrentEra), buffer[written..]);

		foreach (IUnifiedEncodingElement key in this.spendingKeys)
		{
			buffer[written++] = key.UnifiedTypeCode;
			written += CompactSize.Encode((ulong)key.UnifiedDataLength, buffer[written..]);
			written += key.WriteUnifiedData(buffer[written..]);
		}

		return written;
	}

	private static Era EraFrom(BranchId branchId)
	{
		return branchId switch
		{
			BranchId.Nu5 => Era.Orchard,
			_ => throw new NotSupportedException(),
		};
	}

	private static BranchId BranchFrom(Era era)
	{
		return era switch
		{
			Era.Orchard => BranchId.Nu5,
			_ => throw new NotSupportedException(),
		};
	}
}
