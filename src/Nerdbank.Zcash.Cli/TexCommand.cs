// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class TexCommand
{
	internal required string TransparentAddress { get; init; }

	internal static Command BuildCommand()
	{
		Argument<string> transparentAddressArgument = new("transparentAddress")
		{
			Description = Strings.TexTransparentAddressArgumentDescription,
			Arity = ArgumentArity.ExactlyOne,
		};
		Command command = new Command("tex", Strings.TexCommandDescription)
		{
			transparentAddressArgument,
		};

		command.SetAction((parseResult, cancellationToken) =>
		{
			return Task.FromResult(new TexCommand
			{
				TransparentAddress = parseResult.GetValue(transparentAddressArgument),
			}.Execute(cancellationToken));
		});
		return command;
	}

	private int Execute(CancellationToken cancellationToken)
	{
		if (!ZcashAddress.TryDecode(this.TransparentAddress, out _, out string? errorMessage, out ZcashAddress? result))
		{
			Console.Error.WriteLine(errorMessage);
			return 1;
		}

		if (result is not TransparentP2PKHAddress tAddr)
		{
			Console.Error.WriteLine(Strings.TransparentP2PKHAddressRequired);
			return 2;
		}

		TexAddress tex = new(tAddr);
		Console.WriteLine(tex);
		return 0;
	}
}
