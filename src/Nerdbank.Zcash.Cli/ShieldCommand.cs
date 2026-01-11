// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class ShieldCommand : SyncFirstCommandBase
{
	internal bool ShieldAllAddresses { get; init; }

	internal string? Memo { get; init; }

	internal ulong Threshold { get; init; }

	internal static Command BuildCommand()
	{
		Option<bool> shieldAllOption = new("--all", Strings.ShieldAllOptionDescription);
		Option<string> memoOption = new("--memo", Strings.SendMemoOptionDescription);
		Option<ulong> thresholdOption = new("--threshold", Strings.ShieldThresholdOptionDescription);

		Command command = new("shield", Strings.ShieldCommandDescription)
		{
			WalletPathArgument,
			TestNetOption,
			LightServerUriOption,
			NoSyncOption,
			shieldAllOption,
			memoOption,
			thresholdOption,
			SpendingKeySeedOption,
			SpendingKeyAccountIndexOption,
		};

		command.SetAction(async parseResult =>
		{
			return await new ShieldCommand
			{
				WalletPath = parseResult.GetValue(WalletPathArgument),
				NoSync = parseResult.GetValue(NoSyncOption),
				TestNet = parseResult.GetValue(TestNetOption),
				LightWalletServerUrl = parseResult.GetValue(LightServerUriOption),
				ShieldAllAddresses = parseResult.GetValue(shieldAllOption),
				Memo = parseResult.GetValue(memoOption),
				Threshold = parseResult.GetValue(thresholdOption),
				SpendingKeySeed = parseResult.GetValue(SpendingKeySeedOption),
				SpendingKeyAccountIndex = parseResult.GetValue(SpendingKeyAccountIndexOption),
			}.ExecuteAsync(CancellationToken.None);
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
			Console.WriteLine(Strings.NoUnshieldedFunds);
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
			Console.WriteLine($"Shielding {entry.Balance} ZEC from {entry.Address}...");
			TxId txid = await client.ShieldAsync(this.SelectedAccount!, entry.Address, this.Threshold, Zcash.Memo.FromMessage(this.Memo), cancellationToken);
			Console.WriteLine($"Transmitted transaction ID: {txid}");
		}
	}
}
