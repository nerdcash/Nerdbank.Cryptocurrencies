// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// Metadata that can be associated with a unified address or viewing key.
/// </summary>
public record UnifiedEncodingMetadata
{
	/// <summary>
	/// Gets an instance of <see cref="UnifiedEncodingMetadata"/> with default values.
	/// </summary>
	public static readonly UnifiedEncodingMetadata Default = new();

	private DateTimeOffset? expirationDate;

	/// <summary>
	/// Gets the date after which an address this value is applied to expires.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When applied to a viewing key, using this key after its expiration is allowed,
	/// but this value must flow to any other keys or addresses derived from this key.
	/// </para>
	/// <para>
	/// When applied to an address, transactions that send to this address
	/// must be set to expire on a block height expected to be mined no later than
	/// 24 hours within the time specified by this property.
	/// </para>
	/// <para>
	/// When this and <see cref="ExpirationHeight"/> are both set, both must be honored.
	/// </para>
	/// </remarks>
	/// <devremarks>
	/// This value is associated with <see cref="UnifiedTypeCodes.ExpirationByUnixTimeTypeCode"/>.
	/// </devremarks>
	public DateTimeOffset? ExpirationDate
	{
		get => this.expirationDate;
		init => this.expirationDate = value is null ? null : DateTimeOffset.FromUnixTimeSeconds(value.Value.ToUnixTimeSeconds());
	}

	/// <summary>
	/// Gets the block height at which this key expires.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When applied to a viewing key, using this key after its expiration is allowed,
	/// but this value must flow to any other keys or addresses derived from this key.
	/// </para>
	/// <para>
	/// When applied to an address, transactions that send to this address must be set
	/// to expire on a block height no greater than the height specified by this property.
	/// To avoid information leakage on the blockchain, the transaction should <em>not</em>
	/// reduce its customary expiration block to accommodate this value, lest it disclose
	/// the expiration block of the recipient, allowing linking.
	/// Instead, if the customary expiration block would be greater than this value,
	/// the transaction should simply not be transmitted to the blockchain.
	/// </para>
	/// <para>
	/// When this and <see cref="ExpirationDate"/> are both set, both must be honored.
	/// </para>
	/// </remarks>
	/// <devremarks>
	/// This value is associated with <see cref="UnifiedTypeCodes.ExpirationByBlockHeightTypeCode"/>.
	/// </devremarks>
	public uint? ExpirationHeight { get; init; }

	/// <summary>
	/// Gets a value indicating whether this metadata has any must-understand metadata.
	/// </summary>
	internal bool HasMustUnderstandMetadata => this.ExpirationDate.HasValue || this.ExpirationHeight.HasValue;

	/// <summary>
	/// Gets the unified elements that represent this metadata.
	/// </summary>
	/// <returns>A sequence of elements.</returns>
	internal IEnumerable<IUnifiedEncodingElement> GetElements()
	{
		if (this.ExpirationDate.HasValue)
		{
			byte[] data = new byte[8];
			BitUtilities.WriteLE(checked((ulong)this.ExpirationDate.Value.ToUnixTimeSeconds()), data);
			yield return new Element
			{
				UnifiedTypeCode = UnifiedTypeCodes.ExpirationByUnixTimeTypeCode,
				UnifiedData = data,
				UnifiedDataLength = data.Length,
			};
		}

		if (this.ExpirationHeight.HasValue)
		{
			byte[] data = new byte[4];
			BitUtilities.WriteLE(this.ExpirationHeight.Value, data);
			yield return new Element
			{
				UnifiedTypeCode = UnifiedTypeCodes.ExpirationByBlockHeightTypeCode,
				UnifiedData = data,
				UnifiedDataLength = data.Length,
			};
		}
	}

	/// <summary>
	/// Decodes a value for <see cref="ExpirationDate"/> from its unified encoding.
	/// </summary>
	/// <param name="span">The 8 byte buffer to decode.</param>
	/// <returns>The value to use for <see cref="ExpirationDate"/>.</returns>
	internal static DateTimeOffset DecodeExpirationDate(ReadOnlySpan<byte> span) => DateTimeOffset.FromUnixTimeSeconds(checked((long)BitUtilities.ReadUInt64LE(span)));

	/// <summary>
	/// Decodes a value for <see cref="ExpirationHeight"/> from its unified encoding.
	/// </summary>
	/// <param name="span">The 4 byte buffer to decode.</param>
	/// <returns>The value to use for <see cref="ExpirationHeight"/>.</returns>
	internal static uint DecodeExpirationHeight(ReadOnlySpan<byte> span) => BitUtilities.ReadUInt32LE(span);

	private class Element : IUnifiedEncodingElement
	{
		public required byte UnifiedTypeCode { get; init; }

		public required int UnifiedDataLength { get; init; }

		internal required ReadOnlyMemory<byte> UnifiedData { get; init; }

		public int WriteUnifiedData(Span<byte> destination)
		{
			this.UnifiedData.Span.CopyTo(destination);
			return this.UnifiedData.Length;
		}
	}
}
