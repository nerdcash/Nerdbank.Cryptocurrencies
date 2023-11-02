// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reactive.Linq;

namespace ViewModels;

public class CreateNewWalletViewModelTests : ViewModelTestBase
{
	private readonly CreateNewWalletViewModel viewModel;

	public CreateNewWalletViewModelTests()
	{
		this.viewModel = new(this.MainViewModel);
	}

	[Fact]
	public void PasswordContainsWhitespace()
	{
		Assert.False(this.viewModel.PasswordContainsWhitespace);
		this.viewModel.Password = "MyPassword";
		Assert.False(this.viewModel.PasswordContainsWhitespace);

		this.viewModel.Password = "MyPassword ";
		Assert.True(this.viewModel.PasswordContainsWhitespace);

		this.viewModel.Password = "MyPassword\t";
		Assert.True(this.viewModel.PasswordContainsWhitespace);

		this.viewModel.Password = "My Password";
		Assert.True(this.viewModel.PasswordContainsWhitespace);

		this.viewModel.Password = " ";
		Assert.True(this.viewModel.PasswordContainsWhitespace);
	}

	[Fact]
	public async Task RemoveWhitespaceCommandAsync()
	{
		await this.viewModel.RemoveWhitespaceCommand.Execute().FirstAsync();
		Assert.Equal(string.Empty, this.viewModel.Password);
		Assert.False(await this.viewModel.RemoveWhitespaceCommand.CanExecute.FirstAsync());

		this.viewModel.Password = "My Password";
		await this.viewModel.RemoveWhitespaceCommand.Execute().FirstAsync();
		Assert.Equal("MyPassword", this.viewModel.Password);
		await this.viewModel.RemoveWhitespaceCommand.Execute().FirstAsync();
		Assert.Equal("MyPassword", this.viewModel.Password);

		this.viewModel.Password = " My Password \t X ";
		await this.viewModel.RemoveWhitespaceCommand.Execute().FirstAsync();
		Assert.Equal("MyPasswordX", this.viewModel.Password);
	}
}
