// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using MessagePack;

namespace Nerdbank.Zcash.App.Models;

internal class AppSerializerOptions : MessagePackSerializerOptions
{
	internal AppSerializerOptions(MessagePackSerializerOptions copyFrom)
		: base(copyFrom)
	{
	}

	internal HDWallet? HDWalletOwner { get; set; }
}
