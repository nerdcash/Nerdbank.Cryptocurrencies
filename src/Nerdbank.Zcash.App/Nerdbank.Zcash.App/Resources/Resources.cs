// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Media.Imaging;

namespace Nerdbank.Zcash;

public static class Resources
{
	public static Bitmap ZcashLogo { get; } = LoadBitmap("Resources/zec.png");

	private static Bitmap LoadBitmap(string resourceName)
	{
		using Stream? stream = typeof(Resources).Assembly.GetManifestResourceStream(resourceName);
		if (stream is null)
		{
			throw new InvalidOperationException($"Resource '{resourceName}' not found.");
		}

		return new Bitmap(stream);
	}
}
