using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PetPresence.Desktop.Overlay;

public sealed class OverlayViewModel : INotifyPropertyChanged
{
    private bool _layoutEditMode;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<FriendPetViewModel> Friends { get; } = [];


    public FriendPetViewModel GetOrAddFriend(string userId, string displayName)
    {
        var existing = Friends.FirstOrDefault(friend => string.Equals(friend.UserId, userId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            return existing;
        }

        var index = Friends.Count;
        var friendPet = new FriendPetViewModel
        {
            UserId = userId,
            DisplayName = displayName,
            StatusText = "오프라인...",
            AnimationKey = "offline",
            X = 120 + index * 144,
            Y = 120
        };
        Friends.Add(friendPet);
        return friendPet;
    }

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
