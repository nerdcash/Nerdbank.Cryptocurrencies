// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using Nerdbank.Bitcoin;

namespace Nerdbank.Zcash.Cli;

internal abstract class WalletUserCommandBase
{
	internal WalletUserCommandBase()
	{
	}

	[SetsRequiredMembers]
	internal WalletUserCommandBase(WalletUserCommandBase copyFrom)
	{
		this.Console = copyFrom.Console;
		this.WalletPath = copyFrom.WalletPath;
		this.TestNet = copyFrom.TestNet;
		this.LightWalletServerUrl = copyFrom.LightWalletServerUrl;
	}

	internal required IConsole Console { get; init; }

	internal required string WalletPath { get; init; }

	internal bool TestNet { get; set; }

	internal Uri? LightWalletServerUrl { get; set; }

	internal ZcashAddress? SelectedAccountAddress { get; set; }

	internal ZcashAccount? SelectedAccount { get; private set; }

	internal string? SpendingKeySeed { get; init; }

	internal uint SpendingKeyAccountIndex { get; init; }

	protected static Argument<string> WalletPathArgument { get; } = new Argument<string>("wallet path", Strings.WalletPathArgumentDescription).LegalFilePathsOnly();

	protected static Option<bool> TestNetOption { get; } = new("--testnet", Strings.TestNetOptionDescription);

	protected static Option<Uri> LightServerUriOption { get; } = new("--lightserverUrl", Strings.LightServerUrlOptionDescription);

	protected static Option<ZcashAddress> OptionalSelectedAccountOption { get; } = new("--account", parseArgument: arg => ZcashAddress.Decode(arg.Tokens[0].Value), description: Strings.SelectedAccountArgumentDescription);

	protected static Option<ZcashAddress> RequiredSelectedAccountOption { get; } = new("--account", parseArgument: arg => ZcashAddress.Decode(arg.Tokens[0].Value), description: Strings.SelectedAccountArgumentDescription) { IsRequired = true };

	protected static Option<string> SpendingKeySeedOption { get; } = new("--spending-key-seed", Strings.SpendingKeySeedOptionDescription) { IsRequired = true };

	protected static Option<uint> SpendingKeyAccountIndexOption { get; } = new("--spending-key-account-index", () => 0, Strings.SpendingKeyAccountIndexOptionDescription);

	internal async Task<int> ExecuteAsync(CancellationToken cancellationToken)
	{
		using LightWalletClient client = Utilities.ConstructLightClient(
			this.LightWalletServerUrl,
			this.TestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet,
			this.WalletPath);

		client.UpdateFrequency = TimeSpan.FromSeconds(3);

		if (this.SelectedAccountAddress is not null)
		{
			this.SelectedAccount = client.GetAccounts().First(a => a.DefaultAddress.Equals(this.SelectedAccountAddress));
		}

		if (this.SpendingKeySeed is not null)
		{
			Zip32HDWallet zip32 = new(Bip39Mnemonic.Parse(this.SpendingKeySeed), this.TestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet);
			ZcashAccount spendingAccount = new ZcashAccount(zip32, this.SpendingKeyAccountIndex);
			client.AddAccount(spendingAccount);

			// Upgrade the SelectedAccount property too.
			if (this.SelectedAccount is not null && !ZcashAccount.Equality.ByIncomingViewingKey.Equals(this.SelectedAccount, spendingAccount))
			{
				throw new Exception("Specified account does not match the spending key.");
			}

			this.SelectedAccount = spendingAccount;
		}

		return await this.ExecuteAsync(client, cancellationToken);
	}

	internal abstract Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken);
}
