using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AmongUsCapture;
using DSharpPlus;
using DSharpPlus.Entities;
using Microsoft.Win32;
using Newtonsoft.Json;
using SharedMemory;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace AmongUsDiscord
{
    class Program
    {
        private static readonly EventWaitHandle waitHandle = new AutoResetEvent(false);
        public static readonly ClientSocket socket = new ClientSocket();
        public static DiscordClient discord;
        public static DiscordGuild guild;
        public static DiscordChannel codeChannel;
        public static DiscordChannel mainChannel;
        public static DiscordChannel deadChannel;
        public static DiscordRole impostorRole;
        public static List<DiscordMember> deadMembersToMove = new List<DiscordMember>();
        public static Configuration config;
        public static bool debug_mode = false;
        public static bool cheat_mode = false;

        static dynamic GetDynamicConfig()
        {
            dynamic main = new JObject();
            main.bot_token = "";
            main.debug_mode = false;
            main.cheat_mode = false;

            dynamic ids = new JObject();
            ids.guild_id = (ulong)0;
            ids.main_channel_id = (ulong)0;
            ids.dead_channel_id = (ulong)0;
            ids.code_channel_id = (ulong)0;
            ids.impostor_role_id = (ulong)0;
            main.ids = ids;

            dynamic settings = new JObject();
            settings.move_when_dead = false;
            settings.mute_while_tasks = false;
            settings.post_lobbycode = false;
            settings.assign_impostor_role = false;
            main.settings = settings;
            return main;
        }

        static void Main(string[] args)
        {
            Console.Title = "AmongUsDiscord - Developed by RarePotato8DE";
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(Console.Title + "\n");
            Console.ResetColor();

            Console.WriteLine("---------------------------------------------------------------------------\n");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("This discord bot is based on the amonguscapture open-source project");
            Console.WriteLine("You can find it here https://github.com/denverquane/amonguscapture\n");
            Console.ResetColor();

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("I just made minor changes to the amonguscapture project and added the discord functionality");
            Console.WriteLine("Most work was done by denverquane, developer of amonguscapture and by the dsharpplus team\n");
            Console.ResetColor();
            //Console.WriteLine("-------------------------Press any key to continue-------------------------\n");
            Console.WriteLine("---------------------------------------------------------------------------\n");

            if (!File.Exists("config.json"))
            {
                File.WriteAllText(@"config.json", GetDynamicConfig().ToString());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Config file was created in the applications directory");
                Console.WriteLine("Configurate the file and restart the program");
                Console.ResetColor();
                Console.WriteLine("Press any key to continue");
                Console.ReadKey();
                Environment.Exit(0);
            }

            config = JsonConvert.DeserializeObject<Configuration>(File.ReadAllText("config.json"));
            debug_mode = config.debug_mode;
            cheat_mode = config.cheat_mode;
            //Create the Form Console interface. 
            var socketTask = Task.Factory.StartNew(() => socket.Init()); // run socket in background. Important to wait for init to have actually finished before continuing
            Task.Factory.StartNew(() => GameMemReader.getInstance().RunLoop()); // run loop in background
            socketTask.Wait();
            IPCadapter.getInstance().RegisterMinion();
            MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            try
            {
                discord = new DiscordClient(new DiscordConfiguration
                {
                    Token = config.bot_token,
                    TokenType = TokenType.Bot,
                    MinimumLogLevel = Microsoft.Extensions.Logging.LogLevel.None
                });

                discord.Ready += Discord_Ready;
                discord.GuildDownloadCompleted += Discord_GuildDownloadCompleted;

                await discord.ConnectAsync();
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("The discord bot could not be started");
                Console.WriteLine("Please double check the given token in the config file");
                Console.WriteLine("You won't be able to use the discord functionality");
                Console.ResetColor();
            }
            await Task.Delay(-1);
        }

        public static async Task Discord_GuildDownloadCompleted(DiscordClient sender, DSharpPlus.EventArgs.GuildDownloadCompletedEventArgs e)
        {
            if (debug_mode)
                Console.WriteLine("Discord_GuildDownloadCompleted");
            guild = await discord.GetGuildAsync(config.ids.guild_id);
            codeChannel = guild.GetChannel(config.ids.code_channel_id);
            mainChannel = guild.GetChannel(config.ids.main_channel_id);
            deadChannel = guild.GetChannel(config.ids.dead_channel_id);
            impostorRole = guild.GetRole(config.ids.impostor_role_id);
        }

        public static async Task Discord_Ready(DiscordClient sender, DSharpPlus.EventArgs.ReadyEventArgs e)
        {
            if (debug_mode)
                Console.WriteLine("Discord_Ready");
        }

        public static string GetExecutablePath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        public class Ids
        {
            public ulong guild_id { get; set; }
            public ulong main_channel_id { get; set; }
            public ulong dead_channel_id { get; set; }
            public ulong code_channel_id { get; set; }
            public ulong impostor_role_id { get; set; }
        }

        public class Settings
        {
            public bool move_when_dead { get; set; }
            public bool mute_while_tasks { get; set; }
            public bool post_lobbycode { get; set; }
            public bool assign_impostor_role { get; set; }
        }

        public class Configuration
        {
            public string bot_token { get; set; }
            public bool debug_mode { get; set; }
            public bool cheat_mode { get; set; }
            public Ids ids { get; set; }
            public Settings settings { get; set; }
        }
    }
}