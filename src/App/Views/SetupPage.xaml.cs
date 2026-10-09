using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Trinkwasser.App.Services;
using Trinkwasser.App.ViewModels;

namespace Trinkwasser.App.Views;

public sealed partial class SetupPage : Page
{
    public SetupPage()
    {
        ViewModel = new SetupViewModel(App.Current.Loc, App.Current.Settings, App.Current.Autostart);
        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelChanged;
        ViewModel.FocusRequested += (_, field) => FocusField(field);
        Loaded += async (_, _) =>
        {
            FocusCurrentStep();
            await ViewModel.Autostart.RefreshAsync();
        };
        Unloaded += (_, _) => ViewModel.Dispose();
    }

    public SetupViewModel ViewModel { get; }

    public Visibility StepVisibility(int step, int visibleStep) =>
        step == visibleStep ? Visibility.Visible : Visibility.Collapsed;

    public bool IsStep(int step, int expected) => step == expected;

    public Visibility BackVisibility(bool isFirstStep) =>
        isFirstStep ? Visibility.Collapsed : Visibility.Visible;

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(SetupViewModel.Step):
                // Neuer Schritt: Fokus aufs erste Feld und Überschrift vorlesen lassen.
                DispatcherQueue.TryEnqueue(FocusCurrentStep);
                Announcer.Announce(this, $"{ViewModel.StepText}: {ViewModel.StepHeading}");
                break;
            case nameof(SetupViewModel.ErrorMessage):
                Announcer.Announce(this, ViewModel.ErrorMessage);
                break;
        }
    }

    private void FocusCurrentStep()
    {
        Control? target = ViewModel.Step switch
        {
            0 => LanguageChoice,
            1 => DailyGoalBox,
            _ => IntervalBox,
        };
        target?.Focus(FocusState.Programmatic);
    }

    private void FocusField(string field)
    {
        Control target = field switch
        {
            nameof(SetupViewModel.WeeklyGoal) => WeeklyGoalBox,
            nameof(SetupViewModel.Glass) => GlassBox,
            _ => DailyGoalBox,
        };
        target.Focus(FocusState.Programmatic);
    }
}
