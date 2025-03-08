// Copyright (c) IronPigeon, LLC. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash;

/// <summary>
/// An object that invokes a given delegate when disposed.
/// </summary>
internal class DisposableAction : IDisposable
{
	private Action? onDispose;

	/// <summary>
	/// Initializes a new instance of the <see cref="DisposableAction"/> class.
	/// </summary>
	/// <param name="onDispose">The delegate to invoke upon disposal.</param>
	internal DisposableAction(Action onDispose)
	{
		this.onDispose = onDispose;
	}

	/// <inheritdoc/>
	public void Dispose()
	{
		this.onDispose?.Invoke();
		this.onDispose = null;
	}
}
