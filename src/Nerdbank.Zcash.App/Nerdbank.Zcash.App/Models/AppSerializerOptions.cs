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

	/// <summary>
	/// Gets a cache of accounts that have been serialized or deserialized.
	/// </summary>
	/// <remarks>
	/// This allows for the same account to be referenced by multiple contacts without serializing the account multiple times.
	/// </remarks>
	internal Dictionary<Guid, Account> Accounts { get; } = new();
}
