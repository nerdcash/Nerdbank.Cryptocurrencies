// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

public partial class Zip32HDWallet
{
	public partial class Orchard
	{
		public class IncomingViewingKey
		{
			internal IncomingViewingKey(FullViewingKey fullViewingKey)
			{

			}

			internal DiversifierKey Dk { get; }

			internal KeyAgreementPrivateKey Ivk { get; }
		}
	}
}
