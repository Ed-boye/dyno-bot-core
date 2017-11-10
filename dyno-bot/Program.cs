using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Dynobot.Services;

namespace Dynobot
{
    public class Program
    {
        private DiscordSocketClient _client;
        private System.Timers.Timer _timer;
        private StringBuilder connectedGuilds;
        private ChannelMod channelMod;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _timer = new System.Timers.Timer(30000);
            _client = new DiscordSocketClient();
            connectedGuilds = new StringBuilder();

            string token = ""; // TODO: get from file or some shit
        
            _client.Log += Log;
            _client.UserVoiceStateUpdated += UserVoiceStateUpdatedAsync;
            _client.GuildMemberUpdated += UserUpdatedAsync;
            _client.JoinedGuild += UpdateGuildList; ;
            _client.LeftGuild += UpdateGuildList;
            _client.Connected += Connected;
            _client.Ready += Init;
            _timer.Elapsed += heartbeat;

            _timer.Enabled = true;

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        // Initialize channelMod
        private Task Connected()
        {
            channelMod = new ChannelMod(_client.CurrentUser);
            return Task.CompletedTask;
        }

        // TODO: Supporting multiple guilds could make this costly, move to DB? Firebase?
        // Run start-up renewals
        private Task Init()
        {
            // For each of Dyno's guilds and for all channels within, renew them channels.
            // TODO: Do I really need to do this for all channels in this fashion, may be a more efficient way.
            Console.WriteLine("Connected to " + _client.Guilds.Count + " guild(s).");
            foreach(SocketGuild guild in _client.Guilds)
            {
                connectedGuilds.Append(guild.Name + ", ");

                foreach (SocketVoiceChannel channel in guild.VoiceChannels)
                {
                    if (authGrant(channel))
                    {
                        Task.Run(() => channelMod.RenewChannels(channel)).Wait();
                    }
                }
            }

            connectedGuilds.Remove(connectedGuilds.Length - 2, 2); // Stupid way to remove comma & space...

            return Task.CompletedTask;
        }

        // Update the list of connected guilds, used in heartbeat/logging -- may not be really needed anymore...
        private Task UpdateGuildList(SocketGuild unused)
        {
            Console.WriteLine("Guild status updated, connected to " + _client.Guilds.Count + " guild(s).");
            connectedGuilds.Clear();
            foreach (SocketGuild guild in _client.Guilds)
            {
                connectedGuilds.Append(guild.Name + ", ");
            }
            connectedGuilds.Remove(connectedGuilds.Length - 2, 2);
            return Task.CompletedTask;
        }

        // Run renewal when a user's status changes, i.e., game is launched or termianted
        private async Task UserUpdatedAsync(SocketUser before, SocketUser after)
        {
            var user = after as SocketGuildUser;
            if (user.VoiceChannel != null && authGrant(user.VoiceChannel))
            {
                await channelMod.RenewChannels(user.VoiceChannel);
            }
        }

        // Renew on joining new voice channel
        private async Task UserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            // Run renewals for both previous and new user's channels, #threadzForDayz?
            await Task.WhenAll(checkAndUpdateAsync(after.VoiceChannel), checkAndUpdateAsync(before.VoiceChannel));
        }

        // Logging task.
        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        //
        private async Task checkAndUpdateAsync(SocketVoiceChannel voiceChannel)
        {
            if (voiceChannel != null && authGrant(voiceChannel))
            {
                // Run channel renewals based on this channel's state
                await channelMod.RenewChannels(voiceChannel);
            }
        }

        // TODO: Use a method decorator instead for this?
        private bool authGrant(SocketVoiceChannel channel)
        {
            // TODO: What about when someone fucks with permissions later, i.e., removes one? Fail this, I guess?
            return channel.PermissionOverwrites.ToList().Exists(x => x.TargetId == _client.CurrentUser.Id);
        }

        // TODO: Remove, deploying to Pi will remove need for Azure and stupid auto termianting if no CPU is used...
        // Would have liked to just use Log method.
        private void heartbeat(object sender, System.Timers.ElapsedEventArgs e)
        {
            Console.WriteLine("Connected to the following guild(s): " + connectedGuilds.ToString());
        }
    }
}
