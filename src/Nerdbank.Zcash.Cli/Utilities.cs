// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace Nerdbank.Zcash.Cli;

internal static class Utilities
{
	internal static Uri GetDefaultLightWalletUrl(bool testNet) => LightWalletServers.GetDefaultServer(
		testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet);

	internal static LightWalletClient ConstructLightClient(Uri? userSpecifiedServerUrl, ZcashNetwork network, string walletPath)
	{
		Uri serverUrl = userSpecifiedServerUrl ?? GetDefaultLightWalletUrl(testNet: network is not ZcashNetwork.MainNet);
		return new(serverUrl, network, walletPath);
	}

	internal static ZcashAddress AddressParser(ArgumentResult result)
	{
		if (ZcashAddress.TryDecode(result.Tokens[0].Value, out _, out string? errorMessage, out ZcashAddress? addr))
		{
			return addr;
		}

		result.ErrorMessage = errorMessage;
		return null!;
	}

	internal static ZcashAddress[] AddressParserAllowMultiple(ArgumentResult result)
	{
		ZcashAddress[] addresses = new ZcashAddress[result.Tokens.Count];
		for (int i = 0; i < result.Tokens.Count; i++)
		{
			if (ZcashAddress.TryDecode(result.Tokens[i].Value, out _, out string? errorMessage, out ZcashAddress? addr))
			{
				addresses[i] = addr;
			}
			else
			{
				result.ErrorMessage = errorMessage;
				return Array.Empty<ZcashAddress>();
			}
		}

		return addresses;
	}

	internal static Memo[] MemoParserAllowMultiple(ArgumentResult result)
	{
		Memo[] memos = new Memo[result.Tokens.Count];
		for (int i = 0; i < result.Tokens.Count; i++)
		{
			memos[i] = Memo.FromMessage(result.Tokens[i].Value);
		}

		return memos;
	}

	internal static void PrintAccountInfo(ZcashAccount account)
	{
		Console.WriteLine($"Network:         {account.Network}");
		Console.WriteLine(string.Empty);
		Console.WriteLine($"Unified address:      {account.DefaultAddress}");
		Console.WriteLine($"Orchard receiver:     {account.IncomingViewing.Orchard?.DefaultAddress}");
		Console.WriteLine($"Sapling receiver:     {account.IncomingViewing.Sapling?.DefaultAddress}");
		Console.WriteLine($"Transparent receiver: {account.IncomingViewing.Transparent?.DefaultAddress}");

		Console.WriteLine(string.Empty);
		Console.WriteLine($"Unified full viewing key:     {account.FullViewing?.UnifiedKey}");
		Console.WriteLine($"Unified incoming viewing key: {account.IncomingViewing?.UnifiedKey}");
	}
}
