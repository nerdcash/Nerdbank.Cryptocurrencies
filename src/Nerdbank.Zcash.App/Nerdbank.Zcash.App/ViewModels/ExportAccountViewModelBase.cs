// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public abstract class ExportAccountViewModelBase : ViewModelBase
{
	private readonly ObservableAsPropertyHelper<ulong?> optimizedBirthdayHeight;
	private readonly ObservableAsPropertyHelper<ulong?> rebirthHeight;

	protected ExportAccountViewModelBase(IViewModelServices viewModelServices, Account account)
	{
		this.Account = account;
		this.BirthdayHeight = account.ZcashAccount.BirthdayHeight;

		this.optimizedBirthdayHeight = account.WhenAnyValue(a => a.OptimizedBirthdayHeight)
			.ToProperty(this, nameof(this.OptimizedBirthdayHeight));
		this.rebirthHeight = account.WhenAnyValue(a => a.RebirthHeight)
			.ToProperty(this, nameof(this.RebirthHeight));
	}

	public Account Account { get; }

	public string BirthdayHeightCaption => ExportAccountStrings.BirthdayHeightCaption;

	public ulong? BirthdayHeight { get; }

	public string RebirthHeightCaption => ExportAccountStrings.RebirthHeightCaption;

	public ulong? RebirthHeight => this.rebirthHeight.Value;

	public string OptimizedBirthdayHeightCaption => ExportAccountStrings.OptimizedBirthdayHeightCaption;

	public ulong? OptimizedBirthdayHeight => this.optimizedBirthdayHeight.Value;
}
