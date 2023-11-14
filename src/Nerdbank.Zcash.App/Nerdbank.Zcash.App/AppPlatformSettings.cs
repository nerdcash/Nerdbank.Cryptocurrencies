// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App;

/// <summary>
/// Contains settings that are specific to the platform on which the application is running.
/// </summary>
public class AppPlatformSettings
{
	/// <summary>
	/// Gets the path to the directory where wallet data should be stored.
	/// </summary>
	public required string? ConfidentialDataPath { get; init; }

	/// <summary>
	/// Gets the path to the directory where non-confidential application data should be stored.
	/// </summary>
	public required string? NonConfidentialDataPath { get; init; }

	/// <summary>
	/// Gets a value indicating whether the path to the directory where wallet data should be stored is encrypted.
	/// </summary>
	public bool ConfidentialDataPathIsEncrypted { get; init; }
}
