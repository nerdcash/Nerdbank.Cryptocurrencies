// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App.Validators;

/// <summary>
/// A validation attribute to be applied on view model <see cref="string"/> properties that should be a Zcash address.
/// </summary>
/// <remarks>
/// An empty value is considered valid, so this attribute may be used in conjunction with <see cref="RequiredAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true)]
internal class ZcashAddressAttribute : ValidationAttribute
{
	protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
	{
		if (value is string { Length: > 0 } s)
		{
			if (ZcashAddress.TryDecode(s, out DecodeError? _, out string? _, out ZcashAddress? address))
			{
				if (validationContext.ObjectInstance is ViewModelBaseWithAccountSelector viewModel)
				{
					// We should make sure the Zcash address's network aligns with that of the selected account.
					// TODO: Verify that the user changing the selected account (and leaving the address alone) removes the validation error in the UI.
					if (viewModel.SelectedAccount is not null && address.Network != viewModel.SelectedAccount.Network)
					{
						this.ErrorMessage = Strings.FormatAddressNetworkMismatch(
							AddressTicker: address.Network.AsSecurity().TickerSymbol,
							AddressNetwork: address.Network,
							AccountTicker: viewModel.SelectedAccount.Network.AsSecurity().TickerSymbol,
							AccountNetwork: viewModel.SelectedAccount.Network);
						return new ValidationResult(this.ErrorMessage);
					}
				}

				this.ErrorMessage = null;
				return null;
			}
			else
			{
				this.ErrorMessage = Strings.InvalidAddress;
			}
		}
		else
		{
			this.ErrorMessage = null;
		}

		return new ValidationResult(this.ErrorMessage);
	}
}
