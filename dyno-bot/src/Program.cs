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
using Dynobot.Repositories;

namespace Dynobot
{
    public class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(typeof(Program));
        private DiscordSocketClient _client;
        private ChannelMod channelMod;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            // Initialize client and guild dictionary
            _client = new DiscordSocketClient();
            
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
            _client.JoinedGuild += JoinedGuild; ;
            _client.LeftGuild += LeftGuild;
            _client.Connected += Connected;
            _client.Ready += Init;

            await _client.LoginAsync(TokenType.Bot, token.Value);
            await _client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
            log.Fatal("Program has termianted.");
        }

        // Initialize channelMod
        private Task Connected()
        {
            channelMod = new ChannelMod(_client.CurrentUser, new GameRepository(), log);
            return Task.CompletedTask;
        }

        // TODO: Supporting multiple guilds could make this costly, move to DB? Firebase?
        // Run start-up renewals
        private async Task Init()
        {
            // For each of Dyno's guilds build the connected guild dictionary
            log.Info("Connected to " + _client.Guilds.Count + " guild(s). Running guild updates");
            foreach(SocketGuild guild in _client.Guilds)
            {
                log.Debug("Updating guild: " + guild.Id + " - \"" + guild.Name + "\"");
                await channelMod.UpdateGuild(guild);
            }
            log.Info("All guilds updated.");
        }

        // Logging task.
        private Task Log(LogMessage msg)
        {
            log.Info(msg.ToString());
            return Task.CompletedTask;
        }

        private Task JoinedGuild(SocketGuild guild)
        {
            log.Info("Joined guild: \"" + guild.Name + ":" + guild.Id + "\". Connected to: " + _client.Guilds.Count + " guild(s).");
            return Task.CompletedTask;
        }

        private Task LeftGuild(SocketGuild guild) 
        {
            log.Info("Left a guild \"" + guild.Name + ":" + guild.Id + "\". Connected to: " + _client.Guilds.Count + " guild(s).");
            return Task.CompletedTask;
        }

        // Run renewal when a user's status changes, i.e., game is launched or termianted
        private async Task UserUpdatedAsync(SocketUser before, SocketUser after)
        {
            log.Debug("User (" + after.Username + ") updated. Before (" + 
                before.Activity + "), after: (" + after.Activity + ")");
            var user = after as SocketGuildUser;
            if (user.VoiceChannel != null && AuthGrant(user.VoiceChannel))
            {
                log.Debug("Attempting to update user:" + user.Username + ". Current Dynamic Channel: " + user.VoiceChannel.Name);
                await channelMod.UpdateUserDynamicChannel(user);
                // If multiple people in channel, this will change the channel name to that of the user who last played a game
            }
        }

        // Renew on joining new voice channel
        private async Task UserVoiceStateUpdatedAsync(SocketUser user, SocketVoiceState before, SocketVoiceState after)
        {
            // Run renewals for both previous and new user's channels, #threadzForDayz?
            if(before.VoiceChannel != null && after.VoiceChannel != null)
            {
                log.Debug("User (" + user.Username + ") voice state changed: (" + 
                before.VoiceChannel + ":" + before.VoiceChannel.Id + ")-->(" + 
                after.VoiceChannel + ":" + after.VoiceChannel.Id + ")");
            }
            else if(before.VoiceChannel != null)
            {
                log.Debug("User (" + user.Username + ") voice state changed: (" + 
                before.VoiceChannel + ":" + before.VoiceChannel.Id + ")-->(" + 
                after.VoiceChannel + ")");
            }
            else //(after.VoiceChannel != null)
            {
                log.Debug("User (" + user.Username + ") voice state changed: (" + 
                before.VoiceChannel + ")-->(" + 
                after.VoiceChannel + ":" + after.VoiceChannel.Id + ")");
            }
            await Task.WhenAll(CheckAndUpdateAsync(after.VoiceChannel, true), CheckAndUpdateAsync(before.VoiceChannel, false));
        }

        //
        private async Task CheckAndUpdateAsync(SocketVoiceChannel voiceChannel, bool userJoined)
        {
            if (voiceChannel != null && AuthGrant(voiceChannel))
            {
                if  (userJoined) 
                {
                    await channelMod.UpdateNewDynamicChannel(voiceChannel);
                }
                else 
                {
                    await channelMod.UpdateOldDynamicChannel(voiceChannel);
                }
            }
        }

        // TODO: Use a method decorator instead for this?
        private bool AuthGrant(SocketVoiceChannel channel)
        {
            // TODO: What about when someone fucks with permissions later, i.e., removes one? Fail this, I guess?
            return channel.PermissionOverwrites.ToList().Exists(x => x.TargetId == _client.CurrentUser.Id);
        }
    }
}
