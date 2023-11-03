// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;
using Nerdbank.Cryptocurrencies;

namespace Nerdbank.Zcash.App;

/// <summary>
/// A validation attribute to be applied on view model <see cref="string"/> properties that should be a Zcash address.
/// </summary>
/// <remarks>
/// An empty value is considered valid, so this attribute may be used in conjunction with <see cref="RequiredAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true)]
internal class ZcashAddressAttribute : ValidationAttribute
{
	public override bool IsValid(object? value)
	{
		if (value is string { Length: > 0 } s)
		{
			if (ZcashAddress.TryDecode(s, out DecodeError? _, out string? _, out _))
			{
				this.ErrorMessage = null;
				return true;
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

		return false;
	}
}
