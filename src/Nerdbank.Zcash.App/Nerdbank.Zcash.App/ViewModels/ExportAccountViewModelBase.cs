// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public abstract class ExportAccountViewModelBase : ViewModelBase
{
	protected ExportAccountViewModelBase(IViewModelServices viewModelServices, ulong? birthdayHeight)
	{
		this.BirthdayHeight = birthdayHeight;
	}

	public string BirthdayHeightCaption => "Birthday height";

	public ulong? BirthdayHeight { get; }
}
