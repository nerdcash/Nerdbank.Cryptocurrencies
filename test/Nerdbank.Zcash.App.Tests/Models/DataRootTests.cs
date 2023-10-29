// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Models;

public class DataRootTests : ModelTestBase<DataRoot>
{
	public DataRootTests(ITestOutputHelper logger)
		: base(logger)
	{
	}

	public DataRoot DataRoot { get; set; } = new();

	public override DataRoot Model => this.DataRoot;
}
