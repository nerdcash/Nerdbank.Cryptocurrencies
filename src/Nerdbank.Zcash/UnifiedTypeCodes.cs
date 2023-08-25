// Copyright (c) Andrew Arnott. All rights reserved.
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
}
