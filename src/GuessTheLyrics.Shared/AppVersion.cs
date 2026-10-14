using System.Reflection;

namespace GuessTheLyrics.Shared;

public static class AppVersion
{
    /// <summary>Reads the informational version of an assembly without the "+commit" suffix.</summary>
    public static string Of(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                      ?? assembly.GetName().Version?.ToString()
                      ?? "0.0.0";

        var plus = version.IndexOf('+');
        return plus >= 0 ? version[..plus] : version;
    }
}
