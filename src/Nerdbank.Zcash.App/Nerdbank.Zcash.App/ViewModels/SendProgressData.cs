// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

public class SendProgressData : ProgressData
{
	public SendProgressData()
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
