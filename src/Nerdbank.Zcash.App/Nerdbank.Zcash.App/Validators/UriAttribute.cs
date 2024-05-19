// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;

namespace Nerdbank.Zcash.App.Validators;

/// <summary>
/// A validation attribute to be applied on view model <see cref="string"/> properties that should be a valid <see cref="Uri"/>.
/// </summary>
/// <remarks>
/// An empty value is considered valid, so this attribute may be used in conjunction with <see cref="RequiredAttribute"/>.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Method | AttributeTargets.Parameter, AllowMultiple = true)]
internal class UriAttribute : ValidationAttribute
{
	public override bool IsValid(object? value)
	{
		return value is string { Length: > 0 } s && Uri.TryCreate(s, UriKind.Absolute, out Uri? _);
	}
}
