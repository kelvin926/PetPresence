using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PetPresence.Desktop.Overlay;

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private bool _layoutEditMode;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FriendPetViewModel> Friends { get; } = [];

    public bool LayoutEditMode
    {
        get => _layoutEditMode;
        set
        {
            if (_layoutEditMode == value)
            {
                return;
            }

            _layoutEditMode = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LayoutEditMode)));
        }
    }
}
