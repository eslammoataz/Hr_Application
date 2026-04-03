namespace HrSystemApp.Application.Settings;

public class FirebaseSettings
{
    public const string SectionName = "Firebase";

    public string CredentialPath { get; set; } = string.Empty;
    public string AppName { get; set; } = string.Empty;
    public string MessagingScope { get; set; } = string.Empty;
}
