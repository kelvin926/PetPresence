using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PetPresence.Desktop.Overlay;

public partial class OverlayWindow : Window
{
    private FriendPetViewModel? _draggedPet;
    private Point _dragStartMouse;
    private Point _dragStartPet;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OverlayWindowInterop.ApplyOverlayStyles(this, clickThrough: true);
        DataContextChanged += (_, _) => SubscribeToViewModel();
    }

    protected override bool ShowWithoutActivation => true;

    private OverlayViewModel? ViewModel => DataContext as OverlayViewModel;

    private void SubscribeToViewModel()
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        vm.PropertyChanged += ViewModelOnPropertyChanged;
        OverlayWindowInterop.ApplyOverlayStyles(this, clickThrough: !vm.LayoutEditMode);
    }

    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OverlayViewModel.LayoutEditMode) && ViewModel is { } vm)
        {
            OverlayWindowInterop.ApplyOverlayStyles(this, clickThrough: !vm.LayoutEditMode);
        }
    }

    private void Pet_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.LayoutEditMode != true || sender is not FrameworkElement { DataContext: FriendPetViewModel pet })
        {
            return;
        }

        _draggedPet = pet;
        _dragStartMouse = e.GetPosition(this);
        _dragStartPet = new Point(pet.X, pet.Y);
        Mouse.Capture((IInputElement)sender);
        e.Handled = true;
    }

    private void Pet_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggedPet is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        _draggedPet.X = Math.Max(0, _dragStartPet.X + current.X - _dragStartMouse.X);
        _draggedPet.Y = Math.Max(0, _dragStartPet.Y + current.Y - _dragStartMouse.Y);
        e.Handled = true;
    }

    private void Pet_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggedPet is null)
        {
            return;
        }

        _draggedPet = null;
        Mouse.Capture(null);
        e.Handled = true;
    }
}
