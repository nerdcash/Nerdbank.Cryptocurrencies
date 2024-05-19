// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;

namespace Nerdbank.Zcash.App.ViewModels;

public class BackupFileViewModel : ViewModelBase
{
	private readonly IViewModelServices viewModelServices;
	private string backupFilePassword = string.Empty;
	private bool enableHidingPassword = true;

	[Obsolete("For design-time use only.", error: true)]
	public BackupFileViewModel()
		: this(new DesignTimeViewModelServices())
	{
	}

	public BackupFileViewModel(IViewModelServices viewModelServices)
	{
		this.viewModelServices = viewModelServices;

		this.GenerateSecurePasswordCommand = ReactiveCommand.Create(this.GenerateSecurePassword);
		this.BackupCommand = ReactiveCommand.Create(() => { });

		this.LinkProperty(nameof(this.EnableHidingPassword), nameof(this.ConcealPasswordChar));
	}

	public string BackupCommandCaption => BackupFileStrings.BackupCommandCaption;

	public ReactiveCommand<Unit, Unit> BackupCommand { get; }

	public string BackupToFileExplanation => BackupFileStrings.BackupToFileExplanation;

	public string BackupFilePasswordCaption => BackupFileStrings.BackupFilePasswordCaption;

	public string GenerateSecurePasswordCaption => BackupFileStrings.GenerateSecurePasswordCaption;

	public ReactiveCommand<Unit, Unit> GenerateSecurePasswordCommand { get; }

	public string BackupFilePasswordExplanation => BackupFileStrings.BackupFilePasswordExplanation;

	public string BackupFilePassword
	{
		get => this.backupFilePassword;
		set
		{
			if (this.backupFilePassword != value)
			{
				this.EnableHidingPassword = true;
				this.RaiseAndSetIfChanged(ref this.backupFilePassword, value);
			}
		}
	}

	public string BackupFilePasswordWatermark => BackupFileStrings.BackupFilePasswordWatermark;

	public bool EnableHidingPassword
	{
		get => this.enableHidingPassword;
		set => this.RaiseAndSetIfChanged(ref this.enableHidingPassword, value);
	}

	public string ConcealPasswordChar => this.EnableHidingPassword ? "●" : string.Empty;

	public void GenerateSecurePassword()
	{
		this.BackupFilePassword = GeneratePassword(Zip32HDWallet.MinimumEntropyLengthInBits);
		this.EnableHidingPassword = false;
	}

	private static string GeneratePassword(int entropyLengthInBits)
	{
		// The character sequence intentionally excludes commonly misread characters.
		const string validChars = "abcdefghijkmnopqrstuvwxyzABCDEFGHJKLMNPQRTUVWXYZ234679!@#$%^&*()_+-=[];':\",.<>?~";
		StringBuilder password = new(entropyLengthInBits / 8);

		while (password.Length < entropyLengthInBits / 8)
		{
			password.Append(validChars[RandomNumberGenerator.GetInt32(validChars.Length)]);
		}

		return password.ToString();
	}
}
