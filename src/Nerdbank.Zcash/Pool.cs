// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// The various pools of Zcash funds.
/// </summary>
public enum Pool
{
	/// <summary>
	/// The transparent pool, which contains unshielded funds. Analogous to Bitcoin.
	/// </summary>
	Transparent,

	/// <summary>
	/// The first shielded pool. Deprecated. Trusted setup.
	/// </summary>
	Sprout,

	/// <summary>
	/// The second shielded pool. Trusted setup.
	/// </summary>
	Sapling,

	/// <summary>
	/// The third shielded pool. Uses a trustless setup.
	/// </summary>
	Orchard,
}
