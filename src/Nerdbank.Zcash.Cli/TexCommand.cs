// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.IO;

namespace Nerdbank.Zcash.Cli;

internal class TexCommand
{
	internal required IConsole Console { get; init; }

	internal required string TransparentAddress { get; init; }

	internal static Command BuildCommand()
	{
		Argument<string> transparentAddressArgument = new("transparentAddress", Strings.TexTransparentAddressArgumentDescription) { Arity = ArgumentArity.ExactlyOne };
		Command command = new Command("tex", Strings.TexCommandDescription)
		{
			transparentAddressArgument,
		};

		command.SetHandler(ctxt =>
		{
			ctxt.ExitCode = new TexCommand
			{
				Console = ctxt.Console,
				TransparentAddress = ctxt.ParseResult.GetValueForArgument(transparentAddressArgument),
			}.Execute(ctxt.GetCancellationToken());
		});
		return command;
	}

	private int Execute(CancellationToken cancellationToken)
	{
		if (!ZcashAddress.TryDecode(this.TransparentAddress, out _, out string? errorMessage, out ZcashAddress? result))
		{
			this.Console.Error.WriteLine(errorMessage);
			return 1;
		}

		if (result is not TransparentP2PKHAddress tAddr)
		{
			this.Console.Error.WriteLine(Strings.TransparentP2PKHAddressRequired);
			return 2;
		}

		TexAddress tex = new(tAddr);
		this.Console.WriteLine(tex);
		return 0;
	}
}
