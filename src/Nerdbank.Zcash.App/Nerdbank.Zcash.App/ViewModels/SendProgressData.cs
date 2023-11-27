// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class SendProgressData : ProgressData
{
	[Obsolete("For design-time use only.", error: true)]
	public SendProgressData()
		: this(new DesignTimeViewModelServices())
	{
		this.IsInProgress = true;
		this.Current = 1;
		this.To = 2;
	}

	public SendProgressData(IViewModelServices viewModelServices)
	{
	}

	public override string Caption => "Send in progress";

	internal void Apply(LightWalletClient.SendProgress? progress)
	{
		this.IsInProgress = progress?.Total > 0;

		this.To = progress?.Total ?? 0;
		this.Current = progress?.Progress ?? 0;
	}
}
