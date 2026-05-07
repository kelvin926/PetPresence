using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WpfInputElement = System.Windows.IInputElement;
using WpfPoint = System.Windows.Point;
using WpfMouse = System.Windows.Input.Mouse;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using WpfMouseButtonState = System.Windows.Input.MouseButtonState;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace PetPresence.Desktop.Overlay;

public partial class OverlayWindow : Window
{
    private FriendPetViewModel? _draggedPet;
    private WpfPoint _dragStartMouse;
    private WpfPoint _dragStartPet;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OverlayWindowInterop.ApplyOverlayStyles(this, clickThrough: true);
        DataContextChanged += (_, _) => SubscribeToViewModel();
    }


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

    private void Pet_MouseLeftButtonDown(object sender, WpfMouseButtonEventArgs e)
    {
        if (ViewModel?.LayoutEditMode != true || sender is not FrameworkElement { DataContext: FriendPetViewModel pet })
        {
            return;
        }

        _draggedPet = pet;
        _dragStartMouse = e.GetPosition(this);
        _dragStartPet = new WpfPoint(pet.X, pet.Y);
        WpfMouse.Capture((WpfInputElement)sender);
        e.Handled = true;
    }

    private void Pet_MouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_draggedPet is null || e.LeftButton != WpfMouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(this);
        _draggedPet.X = Math.Max(0, _dragStartPet.X + current.X - _dragStartMouse.X);
        _draggedPet.Y = Math.Max(0, _dragStartPet.Y + current.Y - _dragStartMouse.Y);
        e.Handled = true;
    }

    private void Pet_MouseLeftButtonUp(object sender, WpfMouseButtonEventArgs e)
    {
        if (_draggedPet is null)
        {
            return;
        }

        _draggedPet = null;
        WpfMouse.Capture(null);
        e.Handled = true;
    }
}
