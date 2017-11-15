using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Reflection;
using Newtonsoft.Json;
using Discord;
using Discord.WebSocket;
using Dynobot.Services;
using Dynobot.Data;

namespace Dynobot
{
    public class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Program));
        private DiscordSocketClient _client;
        private StringBuilder connectedGuilds;
        private ChannelMod channelMod;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            _client = new DiscordSocketClient();
            connectedGuilds = new StringBuilder();
            
            // Get token
            Token token = JsonConvert.DeserializeObject<Token>(File.ReadAllText(@"token.json"));

            // Initialize Logger
            XmlDocument log4netConfig = new XmlDocument();
            log4netConfig.Load(File.OpenRead("log4net.config"));

            var repo = log4net.LogManager.CreateRepository(
                Assembly.GetEntryAssembly(), typeof(log4net.Repository.Hierarchy.Hierarchy));

            log4net.Config.XmlConfigurator.Configure(repo, log4netConfig["log4net"]);
        
            _client.Log += Log;
            _client.UserVoiceStateUpdated += UserVoiceStateUpdatedAsync;
            _client.GuildMemberUpdated += UserUpdatedAsync;
            _client.JoinedGuild += UpdateGuildList; ;
            _client.LeftGuild += UpdateGuildList;
            _client.Connected += Connected;
            _client.Ready += Init;

            await _client.LoginAsync(TokenType.Bot, token.Value);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        // Initialize channelMod
        private Task Connected()
        {
            channelMod = new ChannelMod(_client.CurrentUser, log);
            return Task.CompletedTask;
        }

        // TODO: Supporting multiple guilds could make this costly, move to DB? Firebase?
        // Run start-up renewals
        private Task Init()
        {
            // For each of Dyno's guilds and for all channels within, renew them channels.
            // TODO: Do I really need to do this for all channels in this fashion, may be a more efficient way.
            log.Info("Connected to " + _client.Guilds.Count + " guild(s).");
            foreach(SocketGuild guild in _client.Guilds)
            {
                connectedGuilds.Append(guild.Name + ", ");

                foreach (SocketVoiceChannel channel in guild.VoiceChannels)
                {
                    if (authGrant(channel))
                    {
                        Task.Run(() => channelMod.RenewAllChannels(channel)).Wait();
                    }
                }
            }

            // Error when not connected to any guilds, derp.
            connectedGuilds.Remove(connectedGuilds.Length - 2, 2); // Stupid way to remove comma & space...

            return Task.CompletedTask;
        }

        // Update the list of connected guilds, used in heartbeat/logging -- may not be really needed anymore...
        private Task UpdateGuildList(SocketGuild unused)
        {
            log.Info("Guild status updated, connected to " + _client.Guilds.Count + " guild(s).");
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
            log.Debug("Hit UserUpdateAsync user: " + after.Username);
            var user = after as SocketGuildUser;
            if (user.VoiceChannel != null && authGrant(user.VoiceChannel))
            {
                await channelMod.UpdateExistingChannel(user.VoiceChannel);
            }
        }

        // Renew on joining new voice channel
        private async Task UserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            // Run renewals for both previous and new user's channels, #threadzForDayz?
            await Task.WhenAll(checkAndUpdateAsync(after.VoiceChannel, true), checkAndUpdateAsync(before.VoiceChannel, false));
        }

        // Logging task.
        private Task Log(LogMessage msg)
        {
            log.Info(msg.ToString());
            return Task.CompletedTask;
        }

        //
        private async Task checkAndUpdateAsync(SocketVoiceChannel voiceChannel, bool userJoined)
        {
            if (voiceChannel != null && authGrant(voiceChannel))
            {
                if  (userJoined) 
                {
                    await channelMod.UpdateNewChannel(voiceChannel);
                }
                else 
                {
                    await channelMod.UpdateOldChannel(voiceChannel);
                }
            }
        }

        // TODO: Use a method decorator instead for this?
        private bool authGrant(SocketVoiceChannel channel)
        {
            // TODO: What about when someone fucks with permissions later, i.e., removes one? Fail this, I guess?
            return channel.PermissionOverwrites.ToList().Exists(x => x.TargetId == _client.CurrentUser.Id);
        }
    }
}
