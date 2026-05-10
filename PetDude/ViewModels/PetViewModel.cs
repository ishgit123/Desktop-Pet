using System.ComponentModel;
using System.Runtime.CompilerServices;
using PetDude.Models;

namespace PetDude.ViewModels;

public sealed class PetViewModel : INotifyPropertyChanged
{
    private double _eyeOffsetX;
    private double _eyeOffsetY;
    private bool _isBlinking;
    private PetMood _mood = PetMood.Idle;
    private PetCharacter _character = PetCharacter.Cat;
    private string _statusText = string.Empty;
    private double _petOffsetX = 58;
    private double _petOffsetY = 48;
    private double _bodyBobY;
    private double _bodyTilt;
    private double _leftFootOffsetX;
    private double _leftFootOffsetY;
    private double _rightFootOffsetX;
    private double _rightFootOffsetY;
    private double _tailWag;
    private double _faceScaleX = 1;
    private bool _isWalking;
    private bool _isUnlocked;

    public event PropertyChangedEventHandler? PropertyChanged;

    public double EyeOffsetX
    {
        get => _eyeOffsetX;
        set => SetField(ref _eyeOffsetX, value);
    }

    public double EyeOffsetY
    {
        get => _eyeOffsetY;
        set => SetField(ref _eyeOffsetY, value);
    }

    public bool IsBlinking
    {
        get => _isBlinking;
        set => SetField(ref _isBlinking, value);
    }

    public PetMood Mood
    {
        get => _mood;
        set
        {
            if (SetField(ref _mood, value))
            {
                OnPropertyChanged(nameof(IsSleeping));
                OnPropertyChanged(nameof(IsPoked));
                OnPropertyChanged(nameof(IsAlert));
                OnPropertyChanged(nameof(IsBored));
                OnPropertyChanged(nameof(IsStatusVisible));
            }
        }
    }

    public PetCharacter Character
    {
        get => _character;
        set
        {
            if (SetField(ref _character, value))
            {
                OnPropertyChanged(nameof(IsCat));
                OnPropertyChanged(nameof(IsDog));
                OnPropertyChanged(nameof(IsRobot));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (SetField(ref _statusText, value))
            {
                OnPropertyChanged(nameof(IsStatusVisible));
            }
        }
    }

    public double PetOffsetX
    {
        get => _petOffsetX;
        set => SetField(ref _petOffsetX, value);
    }

    public double PetOffsetY
    {
        get => _petOffsetY;
        set => SetField(ref _petOffsetY, value);
    }

    public double BodyBobY
    {
        get => _bodyBobY;
        set => SetField(ref _bodyBobY, value);
    }

    public double BodyTilt
    {
        get => _bodyTilt;
        set => SetField(ref _bodyTilt, value);
    }

    public double LeftFootOffsetX
    {
        get => _leftFootOffsetX;
        set => SetField(ref _leftFootOffsetX, value);
    }

    public double LeftFootOffsetY
    {
        get => _leftFootOffsetY;
        set => SetField(ref _leftFootOffsetY, value);
    }

    public double RightFootOffsetX
    {
        get => _rightFootOffsetX;
        set => SetField(ref _rightFootOffsetX, value);
    }

    public double RightFootOffsetY
    {
        get => _rightFootOffsetY;
        set => SetField(ref _rightFootOffsetY, value);
    }

    public double TailWag
    {
        get => _tailWag;
        set => SetField(ref _tailWag, value);
    }

    public double FaceScaleX
    {
        get => _faceScaleX;
        set => SetField(ref _faceScaleX, value);
    }

    public bool IsWalking
    {
        get => _isWalking;
        set => SetField(ref _isWalking, value);
    }

    public bool IsUnlocked
    {
        get => _isUnlocked;
        set => SetField(ref _isUnlocked, value);
    }

    public bool IsSleeping => Mood == PetMood.Sleep;
    public bool IsPoked => Mood == PetMood.Poked;
    public bool IsAlert => Mood is PetMood.Alert or PetMood.CapsLock or PetMood.NoInternet;
    public bool IsBored => Mood == PetMood.Bored;
    public bool IsStatusVisible => !string.IsNullOrWhiteSpace(StatusText);
    public bool IsCat => Character == PetCharacter.Cat;
    public bool IsDog => Character == PetCharacter.Dog;
    public bool IsRobot => Character == PetCharacter.Robot;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
