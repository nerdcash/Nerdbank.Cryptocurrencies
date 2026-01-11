// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal abstract class SyncFirstCommandBase : WalletUserCommandBase
{
	internal SyncFirstCommandBase()
	{
	}

	internal static Option<bool> NoSyncOption { get; } = new("--no-sync", Strings.NoSyncOption);

	internal required bool NoSync { get; init; }

	internal override async Task<int> ExecuteAsync(LightWalletClient client, CancellationToken cancellationToken)
	{
		if (!this.NoSync)
		{
			SyncCommand syncCommand = new(this);
			await syncCommand.ExecuteAsync(client, cancellationToken);
			Console.WriteLine(string.Empty);
		}

		return 0;
	}
}
