// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine.Parsing;

namespace Nerdbank.Zcash.Cli;

internal static class Utilities
{
	internal static Uri GetDefaultLightWalletUrl(bool testNet) =>
		testNet ? new Uri("https://zcash.mysideoftheweb.com:19067/") : new Uri("https://zcash.mysideoftheweb.com:9067/");

	internal static LightWalletClient ConstructLightClient(Uri? userSpecifiedServerUrl, string walletPath, bool testNet, bool watchMemPool)
	{
		Uri serverUrl = userSpecifiedServerUrl ?? GetDefaultLightWalletUrl(testNet);
		return new(
				serverUrl,
				testNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet,
				Path.GetDirectoryName(walletPath)!,
				Path.GetFileName(walletPath),
				$"{Path.GetFileNameWithoutExtension(walletPath)}.log",
				watchMemPool);
	}

	internal static LightWalletClient ConstructLightClient(Uri? userSpecifiedServerUrl, ZcashAccount account, string walletPath, bool watchMemPool)
	{
		Uri serverUrl = userSpecifiedServerUrl ?? GetDefaultLightWalletUrl(account.Network != ZcashNetwork.MainNet);
		return new(
			serverUrl,
			account,
			Path.GetDirectoryName(walletPath)!,
			Path.GetFileName(walletPath),
			$"{Path.GetFileNameWithoutExtension(walletPath)}.log",
			watchMemPool);
	}

	internal static ZcashAddress AddressParser(ArgumentResult result)
	{
		if (ZcashAddress.TryParse(result.Tokens[0].Value, out ZcashAddress? addr, out _, out string? errorMessage))
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
			if (ZcashAddress.TryParse(result.Tokens[i].Value, out ZcashAddress? addr, out _, out string? errorMessage))
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
}
