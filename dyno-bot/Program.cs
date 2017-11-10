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

            string token = "Mzc0MzEyMzEwMzM0MDI5ODM1.DOaXxA.qBCh3baVTlIR7_QgPB7VYPStRmY";
        
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

        private Task Connected()
        {
            channelMod = new ChannelMod(_client.CurrentUser);
            return Task.CompletedTask;
        }
        private Task Init()
        {
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

            connectedGuilds.Remove(connectedGuilds.Length - 2, 2);

            return Task.CompletedTask;
        }

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

        private async Task UserUpdatedAsync(SocketUser before, SocketUser after)
        {
            var user = after as SocketGuildUser;
            if (authGrant(user.VoiceChannel) && user.VoiceChannel != null)
            {
                await channelMod.RenewChannels(user.VoiceChannel);
            }
        }

        private async Task UserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            await Task.WhenAll(checkAndUpdateAsync(after.VoiceChannel), checkAndUpdateAsync(before.VoiceChannel));
        }

        private Task Log(LogMessage msg)
        {
            Console.WriteLine(msg.ToString());
            return Task.CompletedTask;
        }

        private async Task checkAndUpdateAsync(SocketVoiceChannel voiceChannel)
        {
            if (voiceChannel != null && authGrant(voiceChannel))
            {
                await channelMod.RenewChannels(voiceChannel);
            }
        }

        private bool authGrant(SocketVoiceChannel channel)
        {
            return channel.PermissionOverwrites.ToList().Exists(x => x.TargetId == _client.CurrentUser.Id);
        }

        private void heartbeat(object sender, System.Timers.ElapsedEventArgs e)
        {
            Console.WriteLine("Connected to the following guild(s): " + connectedGuilds.ToString());
        }
    }
}
