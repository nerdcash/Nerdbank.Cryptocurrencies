// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
}
