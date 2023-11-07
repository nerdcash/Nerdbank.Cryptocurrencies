// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App;

public static class AppUtilities
{
	internal const ulong SaplingActivationHeight = 419_200;

	public static AccountViewModel? FirstOrDefault(this IEnumerable<AccountViewModel> accountViewModels, Account? accountModel)
	{
		return accountModel is null ? null : accountViewModels.FirstOrDefault(a => a.Account == accountModel);
	}
}
