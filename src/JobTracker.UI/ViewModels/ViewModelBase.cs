using CommunityToolkit.Mvvm.ComponentModel;

namespace JobTracker.UI.ViewModels;

/// <summary>The base of every view model; the <see cref="ViewLocator"/> finds a view for any type derived from it.</summary>
public abstract class ViewModelBase : ObservableObject;
