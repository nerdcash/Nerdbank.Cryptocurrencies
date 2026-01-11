// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using static System.CommandLine.ArgumentValidation;
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
		this.WalletPath = copyFrom.WalletPath;
		this.TestNet = copyFrom.TestNet;
		this.LightWalletServerUrl = copyFrom.LightWalletServerUrl;
	}

	internal static Option<bool> TestNetOption { get; } = new("--testnet", Strings.TestNetOptionDescription);

	internal static Option<Uri> LightServerUriOption { get; } = new("--lightserverUrl", Strings.LightServerUrlOptionDescription);

	internal required string WalletPath { get; init; }

	internal bool TestNet { get; set; }

	internal Uri? LightWalletServerUrl { get; set; }

	internal ZcashAddress? SelectedAccountAddress { get; set; }

	internal ZcashAccount? SelectedAccount { get; private set; }

	internal string? SpendingKeySeed { get; init; }

	internal uint SpendingKeyAccountIndex { get; init; }

	protected static Argument<string> WalletPathArgument { get; } = new Argument<string>("wallet path")
	{
		Description = Strings.WalletPathArgumentDescription,
	}.AcceptLegalFilePathsOnly();

	protected static Option<ZcashAddress> OptionalSelectedAccountOption { get; } = new("--account")
	{
		Description = Strings.SelectedAccountArgumentDescription,
		CustomParser = arg => ZcashAddress.Decode(arg.Tokens[0].Value),
	};

	protected static Option<ZcashAddress> RequiredSelectedAccountOption { get; } = new("--account")
	{
		Description = Strings.SelectedAccountArgumentDescription,
		CustomParser = arg => ZcashAddress.Decode(arg.Tokens[0].Value),
		Required = true,
	};

	protected static Option<string> SpendingKeySeedOption { get; } = new("--spending-key-seed")
	{
		Description = Strings.SpendingKeySeedOptionDescription,
		Required = true,
	};

	protected static Option<uint> SpendingKeyAccountIndexOption { get; } = new("--spending-key-account-index", getDefaultValue: () => 0, description: Strings.SpendingKeyAccountIndexOptionDescription);

	internal async Task<int> ExecuteAsync(CancellationToken cancellationToken)
	{
		using LightWalletClient client = Utilities.ConstructLightClient(
			this.LightWalletServerUrl,
			this.TestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet,
			this.WalletPath);

		if (this.SelectedAccountAddress is not null)
		{
			this.SelectedAccount = client.GetAccounts().First(a => a.DefaultAddress.Equals(this.SelectedAccountAddress));
		}

		if (this.SpendingKeySeed is not null)
		{
			Zip32HDWallet zip32 = new(Bip39Mnemonic.Parse(this.SpendingKeySeed), this.TestNet ? ZcashNetwork.TestNet : ZcashNetwork.MainNet);
			ZcashAccount spendingAccount = new ZcashAccount(zip32, this.SpendingKeyAccountIndex);
			await client.AddAccountAsync(spendingAccount, cancellationToken);

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
