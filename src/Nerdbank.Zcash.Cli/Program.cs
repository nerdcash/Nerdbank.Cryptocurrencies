// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Reflection;
using Nerdbank.Zcash.Cli;

Command rootCommand = new(Assembly.GetExecutingAssembly().GetName().Name!, Strings.RootCommandDescription)
{
	AccountsCommand.BuildCommand(),
	NewAccountCommand.BuildCommand(),
	ImportAccountCommand.BuildCommand(),
	UACommand.BuildCommand(),
	TexCommand.BuildCommand(),
	SyncCommand.BuildCommand(),
	BalanceCommand.BuildCommand(),
	HistoryCommand.BuildCommand(),
	ShieldCommand.BuildCommand(),
	SendCommand.BuildCommand(),
	RequestPaymentCommand.BuildCommand(),
	DeriveCommand.BuildCommand(),
	PriceCommand.BuildCommand(),
};

await new CommandLineBuilder(rootCommand)
	.UseDefaults()
	.Build()
	.InvokeAsync(args);
