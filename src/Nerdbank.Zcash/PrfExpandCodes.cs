// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

// Document enum elements
#pragma warning disable SA1602

/// <summary>
/// The PRFexpand codes that are mixed into the data to produce unique keys.
/// </summary>
internal enum PrfExpandCodes : byte
{
	SaplingAsk = 0x0,
	SaplingNsk = 0x1,
	SaplingOvk = 0x2,
	Esk = 0x4,
	Rcm = 0x5,
	OrchardAsk = 0x6,
	OrchardNk = 0x7,
	OrchardRivk = 0x8,
	Psi = 0x9,
	SaplingDk = 0x10,
	SaplingExtSK = 0x11,
	SaplingExtFVK = 0x12,
	SaplingAskDerive = 0x13,
	SaplingNskDerive = 0x14,
	SaplingOvkDerive = 0x15,
	SaplingDkDerive = 0x16,
	OrchardZip32Child = 0x81,
	OrchardDkOvk = 0x82,
	OrchardRivkInternal = 0x83,
}
