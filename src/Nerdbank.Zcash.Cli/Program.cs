// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using Nerdbank.Zcash.Cli;

RootCommand rootCommand = new(Strings.RootCommandDescription)
{
	AccountsCommand.BuildCommand(),
	NewAccountCommand.BuildCommand(),
	ImportAccountCommand.BuildCommand(),
	UACommand.BuildCommand(),
	TexCommand.BuildCommand(),
	SyncCommand.BuildCommand(),
	BalanceCommand.BuildCommand(),
	HistoryCommand.BuildCommand(),
	PaymentsCommand.BuildCommand(),
	ShieldCommand.BuildCommand(),
	SendCommand.BuildCommand(),
	RequestPaymentCommand.BuildCommand(),
	DeriveCommand.BuildCommand(),
	PriceCommand.BuildCommand(),
};

return await rootCommand.Parse(args).InvokeAsync();
