using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PetPresence.Desktop.Overlay;

public sealed class FriendPetViewModel : INotifyPropertyChanged
{
    private string _statusText = "상태 확인 중...";
    private string _animationKey = "idle";
    private double _x;
    private double _y;

    public event PropertyChangedEventHandler? PropertyChanged;

    public required string UserId { get; init; }
    public required string DisplayName { get; init; }

    public string StatusText
    {
        get => _statusText;
        set => SetField(ref _statusText, value);
    }

    public string AnimationKey
    {
        get => _animationKey;
        set => SetField(ref _animationKey, value);
    }

    public double X
    {
        get => _x;
        set => SetField(ref _x, value);
    }

    public double Y
    {
        get => _y;
        set => SetField(ref _y, value);
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
