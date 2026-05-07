namespace PetPresence.Desktop.Activity;

public interface IForegroundWindowReader
{
    ForegroundAppSnapshot? Read();
}
