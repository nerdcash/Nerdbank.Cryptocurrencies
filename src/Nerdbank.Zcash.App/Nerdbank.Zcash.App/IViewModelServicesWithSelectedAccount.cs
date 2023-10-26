﻿// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App;

public interface IViewModelServicesWithSelectedAccount : IViewModelServices
{
	/// <summary>
	/// Gets or sets the active account.
	/// </summary>
	new Account SelectedAccount { get; set; }
}
