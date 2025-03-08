// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// Key type codes for use in unified addresses and unified keys.
/// </summary>
internal static class UnifiedTypeCodes
{
	/// <summary>
	/// The type code for P2PKH transparent keys and addresses.
	/// </summary>
	internal const byte TransparentP2PKH = 0x00;

	/// <summary>
	/// The type code for P2SH transparent keys and addresses.
	/// </summary>
	internal const byte TransparentP2SH = 0x01;

	/// <summary>
	/// The type code for sapling keys and addresses.
	/// </summary>
	internal const byte Sapling = 0x02;

	/// <summary>
	/// The type code for orchard keys and addresses.
	/// </summary>
	internal const byte Orchard = 0x03;

	/// <summary>
	/// Introduces an unsigned 32-bit integer in little-endian order specifying
	/// the Address Expiry Height, a block height of the Zcash chain associated
	/// with the UA/UVK. A Unified Address containing this type code MUST be
	/// considered expired when the height of the Zcash chain is greater than this value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This value is associated with <see cref="UnifiedEncodingMetadata.ExpirationHeight"/>.
	/// </para>
	/// </remarks>
	internal const byte ExpirationByBlockHeightTypeCode = 0xe0;

	/// <summary>
	/// Introduces an unsigned 64-bit integer in
	/// little-endian order specifying a Unix Epoch Time, hereafter referred to
	/// as the Address Expiry Time. A Unified Address containing this type code
	/// MUST be considered expired when the current time is after the Address Expiry Time.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This value is associated with <see cref="UnifiedEncodingMetadata.ExpirationDate"/>.
	/// </para>
	/// </remarks>
	internal const byte ExpirationByUnixTimeTypeCode = 0xe1;

	/// <summary>
	/// The lower bound of metadata type codes that MUST be understood by a decoder or else the UA must be rejected.
	/// ZIP-316 revision 0 UAs must reject any type codes in this range.
	/// </summary>
	internal const byte MustUnderstandTypeCodeStart = 0xe0;

	/// <summary>
	/// The upper bound of metadata type codes that MUST be understood by a decoder or else the UA must be rejected.
	/// ZIP-316 revision 0 UAs must reject any type codes in this range.
	/// </summary>
	internal const byte MustUnderstandTypeCodeEnd = 0xfd;

	/// <summary>
	/// The range of type codes that are reserved for metadata.
	/// </summary>
	internal static readonly Range MetadataTypeCodeRange = 0xc0..0xfd;
}
