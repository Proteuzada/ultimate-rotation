namespace UltimateRotation;

internal static class UiText
{
    public const string SetupTitle = "Guia de Rotação Ultimate###UltimateRotationSetup";
    public const string OverlayTitle = "Guia###UltimateRotationOverlay";
    public const string Working = "Consultando o FFLogs…";
    public const string CredentialsHint = "Crie um cliente em fflogs.com/api/clients. O segredo fica só na configuração local do Dalamud.";
    public const string GuideOnly = "Guia apenas. O plugin não aperta nenhuma habilidade.";
    public const string CommandHelp = "Abre o guia de rotação. /guia overlay mostra ou esconde o overlay.";

    public static string Kind(StepKind kind) => kind switch
    {
        StepKind.Gcd => "GCD",
        StepKind.OffGcd => "oGCD",
        _ => "ação",
    };
}
