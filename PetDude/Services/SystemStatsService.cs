using System.Net.NetworkInformation;
using System.Windows.Input;
using PetDude.Models;

namespace PetDude.Services;

public sealed class SystemStatsService
{
    public PetMood? GetReaction()
    {
        if (Keyboard.IsKeyToggled(Key.CapsLock))
        {
            return PetMood.CapsLock;
        }

        if (!NetworkInterface.GetIsNetworkAvailable())
        {
            return PetMood.NoInternet;
        }

        return null;
    }
}
