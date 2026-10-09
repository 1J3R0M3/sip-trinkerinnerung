using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Trinkwasser.Core.Drinks;

namespace Trinkwasser.App.Controls;

/// <summary>Smiley für den Trinkstand. Wechselt die Stimmung, hüpft er kurz.</summary>
public sealed partial class MoodFace : UserControl
{
    public static readonly DependencyProperty MoodProperty = DependencyProperty.Register(
        nameof(Mood), typeof(Mood), typeof(MoodFace), new PropertyMetadata(Mood.Okay, OnMoodChanged));

    public MoodFace()
    {
        InitializeComponent();
        Loaded += (_, _) => VisualStateManager.GoToState(this, Mood.ToString(), false);
    }

    public Mood Mood
    {
        get => (Mood)GetValue(MoodProperty);
        set => SetValue(MoodProperty, value);
    }

    private static void OnMoodChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var face = (MoodFace)d;
        VisualStateManager.GoToState(face, e.NewValue.ToString(), true);
        // Nur bei echten Wechseln im laufenden Betrieb hüpfen, nicht beim ersten Anzeigen –
        // und nicht, wenn in Windows die Animationseffekte ausgeschaltet sind.
        if (face.IsLoaded && Settings.AnimationsEnabled) face.PopStoryboard.Begin();
    }

    private static readonly Windows.UI.ViewManagement.UISettings Settings = new();
}
