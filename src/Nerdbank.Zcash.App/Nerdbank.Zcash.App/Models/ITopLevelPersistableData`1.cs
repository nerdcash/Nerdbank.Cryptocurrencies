// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Nerdbank.Zcash.App.Models;

public interface ITopLevelPersistableData<T> : IPersistableData
{
	static abstract T Load(Stream stream);

	Task SaveAsync(Stream stream, CancellationToken cancellationToken);
}
