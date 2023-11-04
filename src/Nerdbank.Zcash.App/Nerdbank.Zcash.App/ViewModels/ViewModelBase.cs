// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Nerdbank.Zcash.App.ViewModels;

public class ViewModelBase : ReactiveObject
{
	protected void LinkProperty(string basePropertyName, string dependentPropertyName)
	{
		this.PropertyChanged += (sender, e) =>
		{
			if (e.PropertyName == basePropertyName)
			{
				this.RaisePropertyChanged(dependentPropertyName);
			}
		};

		this.PropertyChanging += (sender, e) =>
		{
			if (e.PropertyName == basePropertyName)
			{
				this.RaisePropertyChanging(dependentPropertyName);
			}
		};
	}

	private protected static void WrapModels<TModelCollection, TModel, TViewModel>(TModelCollection models, ObservableCollection<TViewModel> viewModels, Func<TModel, TViewModel> wrapper)
		where TModelCollection : IEnumerable<TModel>, INotifyCollectionChanged
		where TModel : class
		where TViewModel : class, IViewModel<TModel>
	{
		models.CollectionChanged += (s, e) =>
		{
			if (e.NewItems is not null)
			{
				foreach (TModel model in e.NewItems)
				{
					viewModels.Add(wrapper(model));
				}
			}

			if (e.OldItems is not null)
			{
				foreach (TModel model in e.OldItems)
				{
					if (viewModels.FirstOrDefault(vm => EqualityComparer<TModel>.Default.Equals(vm.Model, model)) is TViewModel viewModel)
					{
						viewModels.Remove(viewModel);
					}
				}
			}
		};

		foreach (TModel model in models)
		{
			viewModels.Add(wrapper(model));
		}
	}
}
