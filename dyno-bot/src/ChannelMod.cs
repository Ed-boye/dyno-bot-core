using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Dynobot;
using log4net;

namespace Dynobot.Services 
{
    public class ChannelMod
    {
        private const string DEFAULT_DYNAMIC_CHANNEL_NAME = "Join to change name";
        private IUser dyno;
        private log4net.ILog log;
        public ChannelMod (IUser dyno, ILog log) 
        {
           this.dyno = dyno;
           this.log = log;
        }

        // Garbage method.
        // TODO: Sometimes the last channel that didn't get deleted that is dynamic won't get renamed
        // This method is stupid and you wrote it like a dunce.
        public async Task UpdateGuild(SocketGuild guild) 
        {
            var dynamicChannels = GetDynamicChannels(guild);
            if (dynamicChannels.Count == 0)
            {
                log.Debug("UPDATE GUILD");
                var newChannel = await guild.CreateVoiceChannelAsync(DEFAULT_DYNAMIC_CHANNEL_NAME);
                await newChannel.AddPermissionOverwriteAsync(dyno, new Discord.OverwritePermissions());
                log.Debug("Created New Channel: " + newChannel.Id + " - \"" + newChannel.Name + "\"");
                // Create one
            }
            else if (dynamicChannels.Count == 1 && !dynamicChannels.First().Name.Equals(DEFAULT_DYNAMIC_CHANNEL_NAME))
            {
                var channel = dynamicChannels.First();
                if(channel.Users.Count >= 1) 
                {
                    log.Debug("UPDATE GUILD");
                    await UpdateSingleUserChannel(channel);
                }
                else if (channel.Users.Count == 0) 
                {
                    log.Debug("UPDATE GUILD");
                    await channel.ModifyAsync(x => x.Name = DEFAULT_DYNAMIC_CHANNEL_NAME);
                    log.Debug("Reverted Single Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                }
                if(dynamicChannels.Where(x => x.Users.Count == 0).ToList().Count == 0) // Dynamic channels not at n + 1, create one.
                {
                    log.Debug("UPDATE GUILD");
                    var newChannel = await guild.CreateVoiceChannelAsync(DEFAULT_DYNAMIC_CHANNEL_NAME);
                    log.Debug("Created New Channel: " + newChannel.Id + " - \"" + newChannel.Name + "\"");
                    await newChannel.AddPermissionOverwriteAsync(dyno, new Discord.OverwritePermissions());
                }
            }
            else if (dynamicChannels.Count > 1) 
            {
                var emptyChannels = dynamicChannels.Where(x => x.Users.Count == 0).ToList();
                if(emptyChannels.Count == 0) // Dynamic channel not at n + 1, create one.
                {
                    log.Debug("UPDATE GUILD");
                    var newChannel = await guild.CreateVoiceChannelAsync(DEFAULT_DYNAMIC_CHANNEL_NAME);
                    log.Debug("Created New Channel: " + newChannel.Id + " - \"" + newChannel.Name + "\"");
                    await newChannel.AddPermissionOverwriteAsync(dyno, new Discord.OverwritePermissions());
                }
                else if(emptyChannels.Count == dynamicChannels.Count) // Delete all but one dynamic channel
                {
                    if (!emptyChannels.Last().Name.Equals(DEFAULT_DYNAMIC_CHANNEL_NAME))
                    {
                        log.Debug("UPDATE GUILD");
                        await emptyChannels.Last().ModifyAsync(x => x.Name = DEFAULT_DYNAMIC_CHANNEL_NAME);
                        log.Debug("Reverted Single Channel: " + emptyChannels.Last().Id + " - \"" + emptyChannels.Last().Name + "\"");
                        // TODO: Don't be lazy, wrap your modify etc to also log, probably
                    }
                    foreach(SocketVoiceChannel channel in dynamicChannels.Take(dynamicChannels.Count - 1))
                    {
                        log.Debug("UPDATE GUILD");
                        await channel.DeleteAsync();
                        log.Debug("Deleted Empty Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                    }
                }
                else // Remove channels so n + 1 remain where n is the number dynamic channels with users in them.
                {
                    if (!dynamicChannels.Last().Name.Equals(DEFAULT_DYNAMIC_CHANNEL_NAME))
                    {
                        log.Debug("UPDATE GUILD");
                        await dynamicChannels.Last().ModifyAsync(x => x.Name = DEFAULT_DYNAMIC_CHANNEL_NAME);
                        log.Debug("Reverted Single Channel: " + dynamicChannels.Last().Id + " - \"" + dynamicChannels.Last().Name + "\"");
                        // TODO: Don't be lazy, wrap your modify etc to also log, probably
                    }
                    foreach(SocketVoiceChannel channel in dynamicChannels
                        .Where(x => x.Users.Count == 0)
                        .Take(emptyChannels.Count - 1))
                    {
                        log.Debug("UPDATE GUILD");
                        await channel.DeleteAsync();
                        log.Debug("Deleted Empty Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                    }
                }
            }
        }

        public async Task UpdateOldChannel(SocketVoiceChannel channel) 
        {
            if (channel.Users.Count == 0)
            {
                log.Debug("UPDATE OLD");
                await RevertOrDelete(channel);
            }
            else if (channel.Users.Count == 1)
            {
                log.Debug("UPDATE OLD");
                await UpdateSingleUserChannel(channel);
            }
            else //(channel.Users.Count > 1)
            {
                log.Debug("UPDATE OLD");
                await UpdateToTopGame(channel);
            }
        }

        public async Task UpdateNewChannel (SocketVoiceChannel channel)
        {
            if (channel.Users.Count == 0)
            {
                // Shouldn't really hit this ever if called right
            }
            else if (channel.Users.Count == 1)
            {
                log.Debug("UPDATE NEW");
                await UpdateSingleUserChannel(channel);
                
                // Newly joined channel (one user), create a new channel
                var newChannel = await channel.Guild.CreateVoiceChannelAsync(DEFAULT_DYNAMIC_CHANNEL_NAME);
                await newChannel.AddPermissionOverwriteAsync(dyno, new Discord.OverwritePermissions());
            }
            else //(channel.Users.Count > 1)
            {
                log.Debug("UPDATE NEW");
                await UpdateToTopGame(channel);
            }
        }

        public async Task UpdateCurrentChannel (SocketVoiceChannel channel) 
        {
            if (channel.Users.Count == 0)
            {
                // Shouldn't hit this
            }
            else if (channel.Users.Count == 1)
            {
                log.Debug("UPDATE CURRENT");
                await UpdateSingleUserChannel(channel);
            }
            else //(channel.Users.Count > 1)
            {
                log.Debug("UPDATE CURRENT");
                await UpdateToTopGame(channel);
            }
        }

        private async Task UpdateSingleUserChannel(SocketVoiceChannel channel)
        {
            if (channel.Users.First().Game != null)
            {
                // Set name to user's game
                string gameName = channel.Users.First().Game.Value.Name;
                await channel.ModifyAsync(x => x.Name = gameName);
                log.Debug("Renamed Channel: " + channel.Id + " - \"" + channel.Name + "\" to \"" + gameName + "\"");
            }
            else
            {
                // Set name to user's name
                SocketGuildUser user = channel.Users.First();
                string userChannelName;
                if(user.Nickname != null) 
                {
                    userChannelName = user.Nickname + "'s Domain";
                }
                else
                {
                    userChannelName = user.Username + "'s Domain";
                }
                await channel.ModifyAsync(x => x.Name = userChannelName);
                log.Debug("Renamed Channel: " + channel.Id + " - \"" + channel.Name + "\" to \"" + userChannelName + "\"");
            }
        }

        private async Task UpdateToTopGame(SocketVoiceChannel channel)
        {
            Game? topGame = TryGetTopGameInChannel(channel);
            if (topGame != null) 
            {
                await channel.ModifyAsync(x => x.Name = topGame.Value.Name);
                log.Debug("Renamed Channel: " + channel.Id + " - \"" + channel.Name + "\" to \"" + topGame.Value.Name + "\"");
            }
            else // no game being played
            {
                log.Debug("NOT Modifying Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                //await channel.ModifyAsync(x => x.Name = "Join to Change");
                //Don't rename, someone already changed it or is already set to default
            }
        }

        private async Task RevertOrDelete(SocketVoiceChannel channel) 
        {
            if(!IsLastDynoChannel(channel))
            {
                await channel.DeleteAsync();
                log.Debug("Deleted Empty Channel: " + channel.Id + " - \"" + channel.Name + "\"");
            }
            else 
            {
                // TODO: Don't revert if not needed?
                await channel.ModifyAsync(x => x.Name = DEFAULT_DYNAMIC_CHANNEL_NAME);
                log.Debug("Reverted final dynamic Channel: " + channel.Id + " - \"" + channel.Name + "\"");
            }
        }

        // Try to find the top game, return null if no games exist in channel or no majority game
        private Game? TryGetTopGameInChannel(SocketVoiceChannel channel) 
        {
            var gamesDict = new Dictionary<Game?, int>();
            Game? topGame = null;

            foreach (SocketGuildUser person in channel.Users)
            {
                if (person.Game != null)
                {
                    Game? game = person.Game;
                    int count;
                    if (gamesDict.TryGetValue(game, out count))
                    {
                        gamesDict[game]++;
                    }
                    else
                    {
                        gamesDict.Add(game, 1);
                    }
                }
            }
            
            var list = gamesDict.ToList();
            if (list.Count == 0)
            {
                topGame = null;
            }
            else if (list.Count == 1)
            {
                topGame = list.First().Key;
            }
            else // list.count > 1
            {
                list.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                if (list.ElementAt(list.Count- 1).Value == list.ElementAt(list.Count - 2).Value)
                {
                    // No top game
                    topGame = null;
                }
                else 
                {
                    topGame = list.ElementAt(0).Key;
                }
            }
            return topGame;
        }

        // Check to see if only one dynamic channel exists
        // TODO: Save this in a DB instead? Always updated state needed then...
        private bool IsLastDynoChannel(SocketVoiceChannel channel) 
        {
            // TODO turn this into a linq expression, probably...
            int dynamicChannels = 0;
            foreach(SocketVoiceChannel voiceChannel in channel.Guild.VoiceChannels) 
            {
                if(IsDynamicChannel(voiceChannel))
                {
                    dynamicChannels++;
                }
            }
            if (dynamicChannels > 1)
            {
                return false;
            }
            else 
            {
                return true;
            }
        }

        private List<SocketVoiceChannel> GetDynamicChannels(SocketGuild guild) 
        {
            List<SocketVoiceChannel> dynoChannels = new List<SocketVoiceChannel>();
            foreach(SocketVoiceChannel voiceChannel in guild.VoiceChannels)
            {
                if(IsDynamicChannel(voiceChannel))
                {
                    dynoChannels.Add(voiceChannel);
                }
            }
            return dynoChannels;
        }

        private bool IsDynamicChannel (SocketVoiceChannel voiceChannel) 
        {
            // TODO: Is this a better way?
            // voiceChannel.GetPermissionOverwrite(dyno);
            return voiceChannel.PermissionOverwrites.ToList().Exists(x => x.TargetId == dyno.Id);
        }
    }
}
