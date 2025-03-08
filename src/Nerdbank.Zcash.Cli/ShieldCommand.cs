// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class ShieldCommand : SyncFirstCommandBase
{
	internal bool ShieldAllAddresses { get; init; }

	internal static Command BuildCommand()
	{
		Option<bool> shieldAllOption = new("--all", Strings.ShieldAllOptionDescription);

		Command command = new("shield", Strings.ShieldCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
			LightServerUriOption,
			NoSyncOption,
			shieldAllOption,
			SpendingKeySeedOption,
			SpendingKeyAccountIndexOption,
		};

		command.SetHandler(async ctxt =>
		{
			ctxt.ExitCode = await new ShieldCommand
			{
				Console = ctxt.Console,
				WalletPath = ctxt.ParseResult.GetValueForArgument(WalletPathArgument),
				NoSync = ctxt.ParseResult.GetValueForOption(NoSyncOption),
				TestNet = ctxt.ParseResult.GetValueForOption(TestNetOption),
				LightWalletServerUrl = ctxt.ParseResult.GetValueForOption(LightServerUriOption),
				ShieldAllAddresses = ctxt.ParseResult.GetValueForOption(shieldAllOption),
				SpendingKeySeed = ctxt.ParseResult.GetValueForOption(SpendingKeySeedOption),
				SpendingKeyAccountIndex = ctxt.ParseResult.GetValueForOption(SpendingKeyAccountIndexOption),
			}.ExecuteAsync(ctxt.GetCancellationToken());
		});

		return command;
	}

	internal override async Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken)
	{
		int exitCode = await base.ExecuteAsync(client, cancellationToken);
		if (exitCode != 0)
		{
			return exitCode;
		}

		IReadOnlyList<(TransparentAddress Address, decimal Balance)> unshieldedBalances = client.GetUnshieldedBalances(this.SelectedAccount!);
		if (unshieldedBalances.Count == 0)
		{
			this.Console.WriteLine(Strings.NoUnshieldedFunds);
			return 0;
		}

		if (this.ShieldAllAddresses)
		{
			foreach ((TransparentAddress Address, decimal Balance) entry in unshieldedBalances)
			{
				await ShieldAddressAsync(entry);
			}

			return 0;
		}
		else
		{
			// Pick an address at random.
			await ShieldAddressAsync(unshieldedBalances[Random.Shared.Next(unshieldedBalances.Count)]);

			// Indicate by exit code if additional funds remain unshielded.
			return unshieldedBalances.Count > 1 ? 1 : 0;
		}

		async Task ShieldAddressAsync((TransparentAddress Address, decimal Balance) entry)
		{
			this.Console.WriteLine($"Shielding {entry.Balance} ZEC from {entry.Address}...");
			TxId txid = await client.ShieldAsync(this.SelectedAccount!, entry.Address, cancellationToken);
			this.Console.WriteLine($"Transmitted transaction ID: {txid}");
		}
	}
}
