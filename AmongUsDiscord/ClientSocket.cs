using System;
using System.Drawing;
using System.Text.Json;
using AmongUsDiscord;
using SocketIOClient;

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

            if (Work.newState == GameState.TASKS && Work.oldState == GameState.LOBBY)
                Work.NewGameHandler().ConfigureAwait(false).GetAwaiter().GetResult();
            if ((Work.newState == GameState.LOBBY || Work.newState == GameState.MENU) && (Work.oldState == GameState.DISCUSSION || Work.oldState == GameState.TASKS))
                Work.GameEndedHandler().ConfigureAwait(false).GetAwaiter().GetResult();

            if (Program.config.settings.mute_while_tasks)
                Work.MuteState(e.NewState).ConfigureAwait(false).GetAwaiter().GetResult();
            if (Program.config.settings.move_when_dead)
                Work.MoveIfDead(e.NewState).ConfigureAwait(false).GetAwaiter().GetResult();
            return;
            if (!socket.Connected) return;
            socket.EmitAsync("state",
                JsonSerializer
                    .Serialize(e.NewState)); // could possibly use continueWith() w/ callback if result is needed
        }

        private void PlayerChangedHandler(object sender, PlayerChangedEventArgs e)
        {
            if (Program.cheat_mode)
                Console.WriteLine($"PlayerChanged: {e.Name} ({e.Color.ToString()}) {e.Action.ToString()}");

            if (Program.config.settings.move_when_dead)
                Work.MoveIfDead(e).ConfigureAwait(false).GetAwaiter().GetResult();
            return;
            if (!socket.Connected) return;
            socket.EmitAsync("player",
                JsonSerializer.Serialize(e)); //Makes code wait for socket to emit before closing thread.
        }

        private void JoinedLobbyHandler(object sender, LobbyEventArgs e)
        {
            if (Program.debug_mode)
                Console.WriteLine($"JoinedLobby: {e.LobbyCode}");

            if (Program.config.settings.post_lobbycode)
                Work.PublishLobbyCode(e).ConfigureAwait(false).GetAwaiter().GetResult();
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