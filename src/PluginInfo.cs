using System.Reflection;

// The DLL's file properties (Windows Explorer, Details tab): who made it, under which licence, and the font's licence.
[assembly: AssemblyTitle("GorillaHurouf")]
[assembly: AssemblyDescription(GorillaHurouf.PluginInfo.Description)]
[assembly: AssemblyCopyright("Copyright (c) 2026 Musaed. MIT licence. Includes Noto Sans Arabic, SIL Open Font License 1.1. The licence texts are inside this DLL and are written to BepInEx\\cache\\GorillaHurouf on first use.")]
[assembly: AssemblyFileVersion("0.1.0.0")]

namespace GorillaHurouf
{
    public static class PluginInfo
    {
        // Other mods depend on this GUID (BepInDependency): never change it after the first release.
        public const string GUID = "dev.musaed.gorillahurouf";
        public const string NAME = "GorillaHurouf";
        public const string Description = "Arabic text for Gorilla Tag: shaping, font, notifications, translations and auto-fix";
        public const string Version = "0.1.0";
    }
}
