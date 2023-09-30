// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.CommandLine;

namespace Nerdbank.Zcash.Cli;

internal class RequestPaymentCommand
{
	internal required IConsole Console { get; init; }

	internal required ZcashAddress[] Payees { get; init; }

	internal required decimal[]? Amounts { get; init; }

	internal required Memo[]? Memos { get; init; }

	internal required string[]? Labels { get; init; }

	internal required string[]? Messages { get; init; }

	internal static Command BuildCommand()
	{
		Argument<ZcashAddress[]> payeesArgument = new("payee", parse: Utilities.AddressParserAllowMultiple, description: Strings.RequestPaymentPayeeArgumentDescription) { Arity = ArgumentArity.OneOrMore };
		Option<decimal[]> amountsOption = new("--amount", Strings.RequestPaymentAmountOptionDescription) { Arity = ArgumentArity.OneOrMore };
		Option<Memo[]> memosOption = new("--memo", parseArgument: Utilities.MemoParserAllowMultiple, description: Strings.RequestPaymentMemoOptionDescription) { Arity = ArgumentArity.OneOrMore };
		Option<string[]> labelsOption = new("--label", Strings.RequestPaymentLabelOptionDescription) { Arity = ArgumentArity.OneOrMore };
		Option<string[]> messagesOption = new("--message", Strings.RequestPaymentMessageOptionDescription) { Arity = ArgumentArity.OneOrMore };

		Command command = new("RequestPayment", Strings.RequestPaymentCommandDescription)
		{
			payeesArgument,
			amountsOption,
			memosOption,
			labelsOption,
			messagesOption,
		};

		command.AddValidator(v =>
		{
			int payeeCount = v.FindResultFor(payeesArgument)?.Tokens.Count ?? 0;
			int amountsCount = v.FindResultFor(amountsOption)?.Tokens.Count ?? 0;
			int memosCount = v.FindResultFor(memosOption)?.Tokens.Count ?? 0;
			int labelsCount = v.FindResultFor(labelsOption)?.Tokens.Count ?? 0;
			int messagesCount = v.FindResultFor(messagesOption)?.Tokens.Count ?? 0;

			if (amountsCount > 0 && payeeCount != amountsCount)
			{
				v.ErrorMessage = Strings.FormatRequestPaymentArgumentCountMismatch(amountsOption.Name, payeeCount, amountsCount);
				return;
			}

			if (memosCount > 0 && payeeCount != memosCount)
			{
				v.ErrorMessage = Strings.FormatRequestPaymentArgumentCountMismatch(memosOption.Name, payeeCount, memosCount);
				return;
			}

			if (labelsCount > 0 && payeeCount != labelsCount)
			{
				v.ErrorMessage = Strings.FormatRequestPaymentArgumentCountMismatch(labelsOption.Name, payeeCount, labelsCount);
				return;
			}

			if (messagesCount > 0 && payeeCount != messagesCount)
			{
				v.ErrorMessage = Strings.FormatRequestPaymentArgumentCountMismatch(messagesOption.Name, payeeCount, messagesCount);
				return;
			}
		});

		command.SetHandler(ctxt =>
		{
			ctxt.ExitCode = new RequestPaymentCommand
			{
				Console = ctxt.Console,
				Payees = ctxt.ParseResult.GetValueForArgument(payeesArgument),
				Amounts = ctxt.ParseResult.GetValueForOption(amountsOption),
				Memos = ctxt.ParseResult.GetValueForOption(memosOption),
				Labels = ctxt.ParseResult.GetValueForOption(labelsOption),
				Messages = ctxt.ParseResult.GetValueForOption(messagesOption),
			}.Execute();
		});

		return command;
	}

	internal int Execute()
	{
		ImmutableArray<Zip321PaymentRequestUris.PaymentRequestDetails>.Builder details =
			ImmutableArray.CreateBuilder<Zip321PaymentRequestUris.PaymentRequestDetails>(this.Payees.Length);
		for (int i = 0; i < this.Payees.Length; i++)
		{
			decimal? amount = this.Amounts?.Length > 0 ? this.Amounts[i] : null;
			Memo memo = this.Memos?.Length > 0 ? this.Memos[i] : Memo.NoMemo;
			string? label = this.Labels?.Length > 0 ? this.Labels[i] : null;
			string? message = this.Messages?.Length > 0 ? this.Messages[i] : null;

			details.Add(new(this.Payees[i])
			{
				Amount = amount,
				Memo = memo,
				Label = label,
				Message = message,
			});
		}

		Zip321PaymentRequestUris.PaymentRequest request = new(details.MoveToImmutable());
		this.Console.WriteLine(request.ToString());

		return 0;
	}
}
