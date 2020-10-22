using AmongUsCapture;
using DSharpPlus.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace AmongUsDiscord
{
    public class Work
    {
        public static GameState oldState;
        public static GameState newState;
        public static List<GameState> states = new List<GameState>();

        public static void ChangeStates(GameState state)
        {
            states.Add(state);
            if (states.Count == 1)
            {
                oldState = states[0];
                newState = states[0];
            }
            else
            {
                oldState = states[states.Count - 2];
                newState = states[states.Count - 1];
            }
        }

        public static async Task MuteState(GameState state)
        {
            if (Program.mainChannel == null) return;

            if (state == GameState.TASKS)
            {
                //await Task.Delay(4000);
                foreach (var user in Program.mainChannel.Users)
                {
                    try
                    {
                        await user.SetMuteAsync(true);
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            else
            {
                foreach (var user in Program.mainChannel.Users)
                {
                    try
                    {
                        if (Program.config.settings.mute_dead_always)
                        {
                            if (!Program.deadMembersToMove.Contains(user))
                            {
                                await user.SetMuteAsync(false);
                                await Task.Delay(100);
                            }
                        }
                        else
                        {
                            await user.SetMuteAsync(false);
                            await Task.Delay(100);
                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
        }

        public static async Task AddDeadsToList(PlayerChangedEventArgs args)
        {
            //if (Program.deadChannel == null) return;
            if (Program.mainChannel == null) return;
            foreach (var usr in Program.mainChannel.Users)
                if (usr != null)
                    if (usr.DisplayName != null)
                        if (usr.DisplayName.ToLower() == args.Name.ToLower())
                            if (args.Action == PlayerAction.Died || args.Action == PlayerAction.Exiled)
                                Program.deadMembersToMove.Add(usr);
        }

        public static async Task MoveIfDead(GameState state)
        {
            if (state == GameState.LOBBY || state == GameState.MENU || state == GameState.UNKNOWN)
                Program.deadMembersToMove.Clear();

            if (!Program.config.settings.move_when_dead) return;
            if (Program.mainChannel == null) return;
            if (state == GameState.DISCUSSION)
            {
                if (Program.deadChannel == null) return;
                foreach (var usrMove in Program.deadMembersToMove)
                {
                    await Program.deadChannel.PlaceMemberAsync(usrMove);
                    await Task.Delay(100);
                    await usrMove.SetMuteAsync(false);
                    await Task.Delay(100);
                }
            }

            if (state == GameState.LOBBY || state == GameState.MENU || state == GameState.UNKNOWN)
            {
                if (Program.deadChannel == null) return;
                if (Program.mainChannel == null) return;
                foreach (var user in Program.deadChannel.Users)
                {
                    try
                    {
                        await Program.mainChannel.PlaceMemberAsync(user);
                        await Task.Delay(100);
                    }
                    catch (Exception ex)
                    {

                    }
                }
                Program.deadMembersToMove.Clear();
            }
        }

        public static string lastLobbyCode = "";
        public static async Task PublishLobbyCode(LobbyEventArgs args)
        {
            if (Program.codeChannel == null) return;
            var emb = new DiscordEmbedBuilder
            {
                Color = DiscordColor.Red,
                Title = "Among Us Lobby",
                Description = $"Region: {args.Region.ToString()}\nCode: {args.LobbyCode}"
            };
            if (lastLobbyCode != args.LobbyCode)
            {
                lastLobbyCode = args.LobbyCode;
                await Program.codeChannel.SendMessageAsync(embed: emb.Build());
            }
        }

        public static async Task NewGameHandler()
        {
            await Task.Run(async () =>
            {
                List<PlayerInfo> impos = new List<PlayerInfo>();
                int extraLoops = 2;
                while (impos.Count() == 0 || extraLoops != 0)
                {
                    var impostors = GetPlayerInfos().Where(inf => inf.GetIsImpostor()).ToList();

                    if (impos.Count() > 0)
                        extraLoops--;

                    foreach (var imposter in impostors)
                    {
                        if (impos.Contains(imposter)) continue;
                        impos.Add(imposter);
                        if (Program.cheat_mode)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"{imposter.GetPlayerName()} ({imposter.GetPlayerColor().ToString()}) is an impostor");
                            Console.ForegroundColor = ConsoleColor.Gray;
                        }
                        if (Program.config.settings.assign_impostor_role)
                        {
                            var discordImposterUser = Program.mainChannel.Users.Where(u => u.DisplayName.ToLower() == imposter.GetPlayerName().ToLower()).First();
                            if (discordImposterUser != null)
                                hasImposterRole.Add(discordImposterUser);
                        }
                    }
                    await Task.Delay(750);
                }
            });
        }

        public static List<DiscordMember> hasImposterRole = new List<DiscordMember>();
        public static async Task GameEndedHandler()
        {
            if (Program.impostorRole == null) return;
            if (Program.config.settings.assign_impostor_role == false) return;
            foreach (var usr in hasImposterRole)
            {
                try
                {
                    await usr.RevokeRoleAsync(Program.impostorRole);
                    await Task.Delay(250);
                }
                catch (DSharpPlus.Exceptions.UnauthorizedException e)
                {

                }
            }
            hasImposterRole.Clear();
        }

        public static List<PlayerInfo> GetPlayerInfos()
        {
            List<PlayerInfo> infos = new List<PlayerInfo>();
            var allPlayersPtr =
        ProcessMemory.Read<IntPtr>(GameMemReader.GameAssemblyPtr, GameMemReader._gameOffsets.GameDataOffset, 0x5C, 0, 0x24);
            var allPlayers = ProcessMemory.Read<IntPtr>(allPlayersPtr, 0x08);
            var playerCount = ProcessMemory.Read<int>(allPlayersPtr, 0x0C);

            var playerAddrPtr = allPlayers + 0x10;
            playerAddrPtr = allPlayers + 0x10;

            for (var i = 0; i < playerCount; i++)
            {
                var pi = ProcessMemory.Read<PlayerInfo>(playerAddrPtr, 0, 0);
                playerAddrPtr += 4;
                if (pi.PlayerName == 0) continue;
                var playerName = pi.GetPlayerName();
                if (playerName.Length == 0) continue;
                infos.Add(pi);
            }
            return infos;
        }
    }
}
