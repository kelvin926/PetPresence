namespace PetPresence.Desktop.Privacy;

public sealed class BrowserExtensionPolicy
{
    public bool EnabledByDefault => false;
    public bool MaySendRawUrlToServer => false;
    public bool MaySendRawPageTitleToServer => false;

    public string Explain() =>
        "Browser extension support is optional. If installed later, it must classify locally and send only category/status data.";
}
