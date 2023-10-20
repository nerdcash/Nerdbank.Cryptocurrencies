// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.CommandLine;
using System.CommandLine.IO;

namespace Nerdbank.Zcash.Cli;

internal class UACommand
{
	internal static Command BuildCommand()
	{
		Command command = new("ua", Strings.UACommandDescription)
		{
			ParseCommand.BuildCommand(),
			ConstructCommand.BuildCommand(),
		};

		return command;
	}

	private class ParseCommand
	{
		internal required IConsole Console { get; init; }

		internal required string UnifiedAddress { get; init; }

		internal static Command BuildCommand()
		{
			Argument<string> uaArgument = new("unified address", Strings.UAParseArgumentDescription);

			Command parseCommand = new("parse", Strings.UAParseCommandDescription)
		{
			uaArgument,
		};
			parseCommand.SetHandler(ctxt =>
			{
				ctxt.ExitCode = new ParseCommand
				{
					Console = ctxt.Console,
					UnifiedAddress = ctxt.ParseResult.GetValueForArgument(uaArgument),
				}.Execute(ctxt.GetCancellationToken());
			});

			return parseCommand;
		}

		internal int Execute(CancellationToken cancellationToken)
		{
			if (!ZcashAddress.TryDecode(this.UnifiedAddress, out _, out string? errorMessage, out ZcashAddress? parsed))
			{
				this.Console.Error.WriteLine(errorMessage);
				return 1;
			}

			if (parsed is not UnifiedAddress ua)
			{
				this.Console.Error.WriteLine(Strings.NotAUnifiedAddress);
				return 2;
			}

			this.Console.WriteLine($"Network: {ua.Network}");

			foreach (ZcashAddress receiver in ua.Receivers)
			{
				this.Console.WriteLine(receiver);
			}

			return 0;
		}
	}

	private class ConstructCommand
	{
		internal required IConsole Console { get; init; }

		internal required string[] Receivers { get; init; }

		internal static Command BuildCommand()
		{
			Argument<string[]> receiversArgument = new("receivers", Strings.UAConstructReceiversArgumentDescription)
			{
				Arity = ArgumentArity.OneOrMore,
			};

			Command command = new("construct", Strings.UAConstructCommandDescription)
			{
				receiversArgument,
			};

			command.SetHandler(ctxt =>
			{
				ctxt.ExitCode = new ConstructCommand
				{
					Console = ctxt.Console,
					Receivers = ctxt.ParseResult.GetValueForArgument(receiversArgument),
				}.Execute(ctxt.GetCancellationToken());
			});

			return command;
		}

		internal int Execute(CancellationToken cancellationToken)
		{
			ZcashAddress[] receiverAddresses = new ZcashAddress[this.Receivers.Length];
			for (int i = 0; i < this.Receivers.Length; i++)
			{
				if (!ZcashAddress.TryDecode(this.Receivers[i], out _, out string? errorMessage, out ZcashAddress? addr))
				{
					this.Console.Error.WriteLine(Strings.FormatInvalidAddress(this.Receivers[i], errorMessage));
					return 1;
				}

				receiverAddresses[i] = addr;
			}

			try
			{
				UnifiedAddress ua = UnifiedAddress.Create(receiverAddresses);
				this.Console.WriteLine(ua);
			}
			catch (ArgumentException ex)
			{
				this.Console.Error.WriteLine(ex.Message);
				return 2;
			}

			return 0;
		}
	}
}
