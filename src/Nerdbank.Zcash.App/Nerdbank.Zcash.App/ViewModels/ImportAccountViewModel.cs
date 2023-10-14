// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class ImportAccountViewModel : ViewModelBase
{
	private string key = string.Empty;

	public ImportAccountViewModel()
	{
		this.ImportCommand = ReactiveCommand.Create(() => { });
	}

	public string Title => "Import Account";

	public string Explanation => "You can import a key for a specific pool (e.g. transparent, sapling, orchard) or you can import a unified key that can be used for all pools. Multi-pool keys are view-only keys because no multi-pool spending keys exist.";

	public string Key
	{
		get => this.key;
		set => this.RaiseAndSetIfChanged(ref this.key, value);
	}

	public string KeyCaption => "Enter the private key, full or incoming viewing key for the account you wish to import.";

	public string ImportCommandCaption => "Import";

	public ReactiveCommand<Unit, Unit> ImportCommand { get; }
}
