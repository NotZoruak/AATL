using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace MFAAvalonia.Helper;

public class ViewsHelper
{
    private readonly Dictionary<Type, Type> _vmToViewMap = [];
    private readonly Dictionary<Type, Type> _viewToVMMap = [];

    public ViewsHelper AddView<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TView,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TViewModel>(ServiceCollection services)
        where TView : ContentControl
        where TViewModel : ObservableObject
    {
        var viewType = typeof(TView);
        var viewModelType = typeof(TViewModel);

        _vmToViewMap.Add(viewModelType, viewType);

        services.AddSingleton<TViewModel>();
        services.AddSingleton<TView>();

        return this;
    }

    public ViewsHelper AddOnlyView<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TView,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TViewModel>(ServiceCollection services)
        where TView : ContentControl
        where TViewModel : ObservableObject
    {
        var viewType = typeof(TView);
        var viewModelType = typeof(TViewModel);
        
        services.AddSingleton<TView>();
        _viewToVMMap.Add(viewType, viewModelType);
        return this;
    }
    
    public ViewsHelper AddOnlyViewModel<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TView,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        TViewModel>(ServiceCollection services)
        where TView : ContentControl
        where TViewModel : ObservableObject
    {
        var viewType = typeof(TView);
        var viewModelType = typeof(TViewModel);
        
        services.AddSingleton<TViewModel>();
        _viewToVMMap.Add(viewType, viewModelType);
        return this;
    }
    
    public bool TryCreateView(IServiceProvider provider, Type viewModelType, [NotNullWhen(true)] out Control? view)
    {
        var viewModel = provider.GetRequiredService(viewModelType);
        if (_vmToViewMap.TryGetValue(viewModelType, out var viewType))
        {
            if (provider.GetRequiredService(viewType) is Control _view)
            {
                view = _view;
                view.DataContext = viewModel;
                return true;
            }
        }
        return TryCreateView(viewModel, out view);
    }

    public bool TryCreateView(object? viewModel, [NotNullWhen(true)] out Control? view)
    {
        view = null;

        if (viewModel == null)
        {
            return false;
        }

        var viewModelType = viewModel.GetType();

        if (_vmToViewMap.TryGetValue(viewModelType, out var viewType))
        {
            view = Activator.CreateInstance(viewType) as Control;

            if (view != null)
            {
                view.DataContext = viewModel;
            }
        }

        return view != null;
    }

    public Control CreateView<TViewModel>(IServiceProvider provider) where TViewModel : ObservableObject
    {
        var viewModelType = typeof(TViewModel);

        if (TryCreateView(provider, viewModelType, out var view))
        {
            return view;
        }

        throw new InvalidOperationException();
    }
}
