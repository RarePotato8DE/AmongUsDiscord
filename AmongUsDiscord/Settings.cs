using System;
using System.IO;
using System.Windows;
using Config.Net;

namespace AmongUsCapture
{
    public static class Settings
    {
        public static string StorageLocation = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "\\AmongUsDiscord");
        //Global persistent settings that are saved to a json file. Limited Types
        public static IPersistentSettings PersistentSettings = new ConfigurationBuilder<IPersistentSettings>().UseJsonFile(Path.Join(StorageLocation, "Settings.json")).Build();
        public static IGameOffsets GameOffsets = new ConfigurationBuilder<IGameOffsets>().UseJsonFile(Path.Join(StorageLocation, "GameOffsets.json")).Build();
    }

    public interface IPersistentSettings
    {
        //Types allowed: bool, double, int, long, string, TimeSpan, DateTime, Uri, Guid
        //DateTime is always converted to UTC
        [Option(Alias = "Host", DefaultValue = "http://localhost:8123")]
        string host { get; set; }
        
        [Option(Alias = "DebugConsole", DefaultValue = false)]
        bool debugConsole { get; set; }
    }

    public interface IGameOffsets
    {
        //Types allowed: bool, double, int, long, string, TimeSpan, DateTime, Uri, Guid
        //DateTime is always converted to UTC
        
        [Option(Alias = "GameHash", DefaultValue = "FF1DAE62454312FCE09A39061999C26FD26440FDA5F36C1E6424290A34D05B08")]
        string GameHash { get; }
        
        [Option(Alias = "Offsets.Client", DefaultValue = 21216924)]
        int AmongUsClientOffset { get; set; }
        
        [Option(Alias = "Offsets.GameData", DefaultValue = 21217080)]
        int GameDataOffset { get; set; }
        
        [Option(Alias = "Offsets.MeetingHud", DefaultValue = 21216180)]
        int MeetingHudOffset { get; set; }
        
        [Option(Alias = "Offsets.GameStartManager", DefaultValue = 20552656)]
        int GameStartManagerOffset { get; set; }
        
        [Option(Alias = "Offsets.HudManager", DefaultValue = 17173184)]
        int HudManagerOffset { get; set; }
        
        [Option(Alias = "Offsets.ServerManager", DefaultValue = 20511952)]
        int ServerManagerOffset { get; set; }
    }
}
