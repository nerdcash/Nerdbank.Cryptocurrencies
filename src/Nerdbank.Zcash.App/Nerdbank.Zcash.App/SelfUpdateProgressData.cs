// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App;

public class SelfUpdateProgressData : ProgressData
{
	private string caption = "Downloading update...";

	public override string Caption => this.caption;

	public void NotifyDownloadingUpdate(string newVersion)
	{
		this.Clear();
		this.SetCaption(Strings.FormatDownloadingUpdate(newVersion));
		this.IsInProgress = true;

		// Velopack counts to 100%.
		this.To = 100;
	}

	private void SetCaption(string caption)
	{
		this.caption = caption;
		this.RaisePropertyChanged(nameof(this.Caption));
	}
}
