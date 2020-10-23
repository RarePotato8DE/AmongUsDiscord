using System;
using System.Drawing;
using System.Text.Json;
using System.Threading.Tasks;
using AmongUsDiscord;
using DSharpPlus.Entities;
using SocketIOClient;
using System.Linq;
using System.Collections.Generic;

namespace AmongUsCapture
{
    public class ClientSocket
    {
        public event EventHandler<ConnectedEventArgs> OnConnected;
        public event EventHandler OnDisconnected;

        private SocketIO socket;
        private string ConnectCode;

        public void Init()
        {
            // Initialize a socket.io connection.
            socket = new SocketIO();

            // Handle tokens from protocol links.
            IPCadapter.getInstance().OnToken += OnTokenHandler;

            // Register handlers for game-state change events.
            GameMemReader.getInstance().GameStateChanged += GameStateChangedHandler;
            GameMemReader.getInstance().PlayerChanged += PlayerChangedHandler;
            GameMemReader.getInstance().JoinedLobby += JoinedLobbyHandler;
            GameMemReader.getInstance().ChatMessageAdded += ChatMessageAddedHandler;

            // Handle socket connection events.
            socket.OnConnected += (sender, e) =>
            {
                // Report the connection
                //Settings.form.setConnectionStatus(true);

                // Alert any listeners that the connection has occurred.
                OnConnected?.Invoke(this, new ConnectedEventArgs() { Uri = socket.ServerUri.ToString() });

                // On each (re)connection, send the connect code and then force-update everything.
                socket.EmitAsync("connectCode", ConnectCode).ContinueWith((_) =>
                {
                    GameMemReader.getInstance().ForceUpdatePlayers();
                    GameMemReader.getInstance().ForceTransmitState();
                    GameMemReader.getInstance().ForceTransmitLobby();
                });
            };

            // Handle socket disconnection events.
            socket.OnDisconnected += (sender, e) =>
            {
                //Settings.form.setConnectionStatus(false);
                //Settings.conInterface.WriteTextFormatted($"[§bClientSocket§f] Lost connection!");

                // Alert any listeners that the disconnection has occured.
                OnDisconnected?.Invoke(this, EventArgs.Empty);
            };

            if (Program.debug_mode)
                Console.WriteLine("Initialized socket");
        }

        public void OnTokenHandler(object sender, StartToken token)
        {
            if (socket.Connected)
                // Disconnect from the existing host...
                socket.DisconnectAsync().ContinueWith((t) =>
                {
                    // ...then connect to the new one.
                    Connect(token.Host, token.ConnectCode);
                });
            else
                // Connect using the host and connect code specified by the token.
                Connect(token.Host, token.ConnectCode);
        }

        private void OnConnectionFailure(AggregateException e = null)
        {
            var message = e != null ? e.Message : "A generic connection error occured.";
        }

        private void Connect(string url, string connectCode)
        {
            try
            {
                ConnectCode = connectCode;
                socket.ServerUri = new Uri(url);
                if (socket.Connected) socket.DisconnectAsync().Wait();
                socket.ConnectAsync().ContinueWith(t =>
                {
                    if (!t.IsCompletedSuccessfully)
                    {
                        OnConnectionFailure(t.Exception);
                        return;
                    }
                });
            }
            catch (ArgumentNullException)
            {
                Console.WriteLine("Invalid bot host, not connecting");
            }
            catch (UriFormatException)
            {
                Console.WriteLine("Invalid bot host, not connecting");
            }
        }

        private void ChatMessageAddedHandler(object sender, ChatMessageEventArgs e)
        {
            if (Program.debug_mode)
                Console.WriteLine($"ChatMessageAdded: {e.Sender} ({e.Color.ToString()}): {e.Message}");
            return;
            if (!socket.Connected) return;
        }

        private void GameStateChangedHandler(object sender, GameStateChangedEventArgs e)
        {
            if (Program.debug_mode)
                Console.WriteLine($"GameStateChanged: {e.NewState}");
            Work.ChangeStates(e.NewState);

            if ((Work.newState == GameState.LOBBY || Work.newState == GameState.MENU) && (Work.oldState == GameState.DISCUSSION || Work.oldState == GameState.TASKS))
                GameEndedHandler(sender, e);

            if (Work.newState == GameState.DISCUSSION)
                GameDiscussionHandler(sender, e);

            if (Work.newState == GameState.TASKS && (Work.oldState == GameState.DISCUSSION || Work.oldState == GameState.LOBBY))
            {
                if (Work.oldState == GameState.LOBBY)
                {
                    NewGameStartedHandler(sender, e);
                }
                else
                {
                    GameContinuesHandler(sender, e);
                }
            }
            //if (Work.newState == GameState.TASKS && Work.oldState == GameState.LOBBY)
            //    Work.NewGameHandler().ConfigureAwait(false).GetAwaiter().GetResult();
            //if ((Work.newState == GameState.LOBBY || Work.newState == GameState.MENU) && (Work.oldState == GameState.DISCUSSION || Work.oldState == GameState.TASKS))
            //    Work.GameEndedHandler().ConfigureAwait(false).GetAwaiter().GetResult();

            //if (Program.config.settings.mute_while_tasks)
            //    Work.MuteState(e.NewState).ConfigureAwait(false).GetAwaiter().GetResult();
            //Work.MoveIfDead(e.NewState).ConfigureAwait(false).GetAwaiter().GetResult();
            return;
            if (!socket.Connected) return;
            socket.EmitAsync("state",
                JsonSerializer
                    .Serialize(e.NewState)); // could possibly use continueWith() w/ callback if result is needed
        }

        private async void GameDiscussionHandler(object sender, GameStateChangedEventArgs e)
        {
            //discussion
            if (Program.config.settings.move_when_dead)
            {
                if (Program.deadChannel != null && Program.mainChannel != null)
                {
                    foreach (var dead in Work.deadPlayers)
                    {
                        var deadDiscordUsersAll = Program.mainChannel.Users.Where(u => u.DisplayName.ToLower() == dead.Name.ToLower());
                        if (deadDiscordUsersAll != null && deadDiscordUsersAll.Count() > 0)
                        {
                            var deadDiscordUser = deadDiscordUsersAll.First();
                            if (deadDiscordUser != null)
                            {
                                try
                                {
                                    await Program.deadChannel.PlaceMemberAsync(deadDiscordUser);
                                    await Task.Delay(150);
                                    await deadDiscordUser.SetMuteAsync(false);
                                    await Task.Delay(100);
                                }
                                catch (DSharpPlus.Exceptions.UnauthorizedException ee)
                                {

                                }
                            }
                        }
                    }
                }
            }

            if (Program.config.settings.mute_while_tasks)
            {
                if (Program.mainChannel != null)
                {
                    List<DiscordMember> deadDiscordUsers = new List<DiscordMember>();
                    foreach (var dead in Work.deadPlayers)
                    {
                        var deadDiscordUserAll = Program.mainChannel.Users.Where(u => u.DisplayName.ToLower() == dead.Name.ToLower());
                        if (deadDiscordUserAll != null)
                            if (deadDiscordUserAll.Count() > 0)
                            {
                                var deadDiscordUser = deadDiscordUserAll.First();
                                if (deadDiscordUser != null)
                                {
                                    deadDiscordUsers.Add(deadDiscordUser);
                                    Work.deadDiscordPlayers.Add(deadDiscordUser);
                                }
                            }
                    }

                    foreach (var user in Program.mainChannel.Users)
                    {
                        if (Program.config.settings.mute_dead_always)
                        {
                            if (!deadDiscordUsers.Contains(user))
                            {
                                await user.SetMuteAsync(false);
                                await Task.Delay(250);
                            }
                        }
                        else
                        {
                            await user.SetMuteAsync(false);
                            await Task.Delay(250);
                        }
                    }
                }
            }
        }

        private async void GameContinuesHandler(object sender, GameStateChangedEventArgs e)
        {
            //was discussion, now tasks
            if (Program.config.settings.mute_while_tasks)
            {
                if (Program.mainChannel != null)
                {
                    foreach (var user in Program.mainChannel.Users)
                    {
                        await user.SetMuteAsync(true);
                        await Task.Delay(250);
                    }
                }
            }
        }

        private async void NewGameStartedHandler(object sender, GameStateChangedEventArgs e)
        {
            //was lobby, now tasks
            if (Program.config.settings.mute_while_tasks)
            {
                if (Program.mainChannel != null)
                {
                    foreach (var user in Program.mainChannel.Users)
                    {
                        await user.SetMuteAsync(true);
                        await Task.Delay(250);
                    }
                }
            }

            await Task.Run(async () =>
            {
                await Task.Delay(4000);
                var impostors = Work.GetPlayerInfos().Where(i => i.GetIsImpostor());
                while (impostors.Count() == 0)
                {
                    impostors = Work.GetPlayerInfos().Where(i => i.GetIsImpostor());
                    await Task.Delay(500);
                }

                if (Program.cheat_mode)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    foreach (var impostor in impostors)
                        Console.WriteLine(impostor.GetPlayerName() + " (" + impostor.GetPlayerColor().ToString() + ") is an impostor");
                    Console.ResetColor();
                }

                if (Program.mainChannel != null)
                {
                    if (Program.config.settings.assign_impostor_role)
                    {
                        if (Program.impostorRole != null)
                        {
                            foreach (var impostor in impostors)
                            {
                                var discordImposterUsers = Program.mainChannel.Users.Where(u => u.DisplayName.ToLower() == impostor.GetPlayerName().ToLower());
                                if (discordImposterUsers != null && discordImposterUsers.Count() > 0)
                                {
                                    var currentDiscordImposterUser = discordImposterUsers.First();
                                    if (currentDiscordImposterUser != null)
                                    {
                                        Work.discordImpostors.Add(currentDiscordImposterUser);
                                        try
                                        {
                                            await currentDiscordImposterUser.GrantRoleAsync(Program.impostorRole);
                                            await Task.Delay(250);
                                        }
                                        catch (DSharpPlus.Exceptions.UnauthorizedException e)
                                        {

                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            });
        }

        private async void GameEndedHandler(object sender, GameStateChangedEventArgs e)
        {
            //was discussion or tasks, now menu or lobby
            Work.deadPlayers.Clear();
            if (Program.config.settings.mute_while_tasks)
            {
                if (Program.mainChannel != null)
                {
                    foreach (var user in Program.mainChannel.Users)
                    {
                        await user.SetMuteAsync(false);
                        await Task.Delay(250);
                    }
                }
            }

            if (Program.config.settings.move_when_dead)
            {
                if (Program.mainChannel != null && Program.deadChannel != null)
                {
                    foreach (var user in Program.deadChannel.Users)
                    {
                        try
                        {
                            await Program.mainChannel.PlaceMemberAsync(user);
                            await Task.Delay(150);
                        }
                        catch (Exception ex)
                        {

                        }
                    }
                }
            }

            if (Program.config.settings.assign_impostor_role && Program.impostorRole != null)
            {
                foreach (var user in Work.discordImpostors)
                {
                    try
                    {
                        await user.RevokeRoleAsync(Program.impostorRole);
                        await Task.Delay(250);
                    }
                    catch (DSharpPlus.Exceptions.UnauthorizedException eee)
                    {

                    }
                }
            }

            Work.discordImpostors.Clear();
            Work.deadDiscordPlayers.Clear();
        }

        private void PlayerChangedHandler(object sender, PlayerChangedEventArgs e)
        {
            if (Program.cheat_mode)
                Console.WriteLine($"PlayerChanged: {e.Name} ({e.Color.ToString()}) {e.Action.ToString()}");

            if (e.Action == PlayerAction.Died)
                PlayerDiedHandler(sender, e);
            //Work.AddDeadsToList(e).ConfigureAwait(false).GetAwaiter().GetResult();
            return;
            if (!socket.Connected) return;
            socket.EmitAsync("player",
                JsonSerializer.Serialize(e)); //Makes code wait for socket to emit before closing thread.
        }

        private async void PlayerDiedHandler(object sender, PlayerChangedEventArgs e)
        {
            Work.deadPlayers.Add(e);
        }

        private async void JoinedLobbyHandler(object sender, LobbyEventArgs e)
        {
            if (Program.debug_mode)
                Console.WriteLine($"JoinedLobby: {e.LobbyCode}");

            if (Program.config.settings.post_lobbycode)
            {
                if (Program.codeChannel != null)
                {
                    var emb = new DiscordEmbedBuilder
                    {
                        Color = DiscordColor.Red,
                        Title = "Among Us Lobby",
                        Description = $"Region: {e.Region.ToString()}\nCode: {e.LobbyCode}"
                    };
                    if (Work.lastLobbyCode != e.LobbyCode)
                    {
                        Work.lastLobbyCode = e.LobbyCode;
                        await Program.codeChannel.SendMessageAsync(embed: emb.Build());
                    }
                }
            }

            //if (Program.config.settings.post_lobbycode)
            //    Work.PublishLobbyCode(e).ConfigureAwait(false).GetAwaiter().GetResult();
            return;
            if (!socket.Connected) return;
            socket.EmitAsync("lobby", JsonSerializer.Serialize(e));
        }



        public class ConnectedEventArgs : EventArgs
        {
            public string Uri { get; set; }
        }
    }
}