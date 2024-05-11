// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Avalonia.Media.Imaging;

namespace Nerdbank.Zcash;

public static class Resources
{
	public static Bitmap AppLogo { get; } = LoadBitmap("Resources/applogo.png");

	private static Bitmap LoadBitmap(string resourceName)
	{
		using Stream stream = LoadResourceStream(resourceName);
		return new Bitmap(stream);
	}

	private static Stream LoadResourceStream(string resourceName)
		=> typeof(Resources).Assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Resource '{resourceName}' not found.");
}
