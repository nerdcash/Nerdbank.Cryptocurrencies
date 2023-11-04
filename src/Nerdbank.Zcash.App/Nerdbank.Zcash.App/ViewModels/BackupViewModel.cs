// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

/// <summary>
/// Displays the seed phrase, birthday height, # of accounts used, and backup to file button.
/// </summary>
public class BackupViewModel : ViewModelBaseWithAccountSelector, IHasTitle
{
	private readonly ObservableAsPropertyHelper<ViewModelBase?> exportAccountViewModel;
	private bool revealData;

	[Obsolete("Design-time only", error: true)]
	public BackupViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public BackupViewModel(IViewModelServices viewModelServices)
		: base(viewModelServices)
	{
		this.RevealCommand = ReactiveCommand.Create(() => { this.IsRevealed = !this.IsRevealed; });

		this.BackupFileViewModel = new BackupFileViewModel(viewModelServices);
		this.exportAccountViewModel = this.WhenAnyValue<BackupViewModel, ViewModelBase?, Account?>(
			vm => vm.SelectedAccount,
			a =>
				a is null ? null :
				a.MemberOf is null ? new ExportLoneAccountViewModel(viewModelServices, a.ZcashAccount) :
				new ExportHDWalletViewModel(viewModelServices, a.MemberOf))
			.ToProperty(this, nameof(this.ExportAccountViewModel));
	}

	public string Title => "Backup";

	public BackupFileViewModel BackupFileViewModel { get; }

	public ViewModelBase? ExportAccountViewModel => this.exportAccountViewModel.Value;

	public ReactiveCommand<Unit, Unit> RevealCommand { get; }

	public string RevealCommandCaption => "Reveal seed phrase";

	public List<string> RevealCautions => new()
	{
		"🕵🏼 Revealing your seed phrase on a screen may compromise your security. If you are in a public place, you should not reveal your seed phrase.",
		"📷 If you take a screenshot of your seed phrase, your backup may be viewed by other apps or uploaded to the cloud. You can make a safe backup with physical paper and pen and storing it in a secure location.",
	};

	public bool IsRevealed
	{
		get => this.revealData;
		set => this.RaiseAndSetIfChanged(ref this.revealData, value);
	}
}
