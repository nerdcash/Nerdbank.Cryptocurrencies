// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	partial class Sapling
	{
		internal readonly struct ViewingKey
		{
			internal ViewingKey(SubgroupPoint ak, NullifierDerivingKey nk)
			{
				this.Ak = ak;
				this.Nk = nk;
			}

			internal SubgroupPoint Ak { get; }

			internal NullifierDerivingKey Nk { get; }
		}
	}
}
