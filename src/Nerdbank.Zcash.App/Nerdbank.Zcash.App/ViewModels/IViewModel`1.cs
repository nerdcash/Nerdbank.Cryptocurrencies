// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.ViewModels;

/// <summary>
/// An interface implemented by a view model that wraps a model.
/// </summary>
/// <typeparam name="TModel">The type of the model.</typeparam>
internal interface IViewModel<TModel>
{
	/// <summary>
	/// Gets the underlying model.
	/// </summary>
	TModel Model { get; }
}
