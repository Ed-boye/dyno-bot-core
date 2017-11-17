using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;
using Dynobot;
using Dynobot.Repositories;
using log4net;

namespace Dynobot.Services 
{
    public class ChannelMod
    {
        private const string DEFAULT_DYNAMIC_CHANNEL_NAME = "Join to change name";
        private IUser dyno;
        private log4net.ILog log;
        private GameRepository gamesRepo;

        public ChannelMod (IUser dyno, GameRepository gamesRepo, ILog log) 
        {
           this.dyno = dyno;
           this.log = log;
           this.gamesRepo = gamesRepo;
        }

        public async Task UpdateGuild(SocketGuild guild)
        {
            var dynamicChannels = GetDynamicChannels(guild);
            
            // Properly configured
            if (dynamicChannels.FindAll(x => x.Users.Count >= 1).Count == dynamicChannels.Count - 1)
            {
                log.Debug("Guild is good: " + guild.Name);
            }
            // Not the right ratio
            if (dynamicChannels.FindAll(x => x.Users.Count == 0).Count == 1 && dynamicChannels.Any(x => x.Users.Count >= 1))
            {
                log.Debug("Guild channel ratio improperly configured: " + guild.Name);
                await TryRenameChannel(dynamicChannels.FirstOrDefault(x => x.Users.Count == 0));
            }
            // only one with users
            else if (dynamicChannels.Count == 1 && dynamicChannels.First().Users.Count >= 1)
            {
                // create a new one
                log.Debug("Only one channel (with people) in guild: " + guild.Name);
                await CreateChannel(guild);
            }
            // if none
            else if (dynamicChannels.Count == 0)
            {
                // TODO: Determine if we can collapse this into above
                // create a new one
                log.Debug("No dynamic channels in guild: " + guild.Name);
                await CreateChannel(guild);
            }
            // if all empty
            else if (dynamicChannels.All(x => x.Users.Count == 0))
            {
                // delete all but one
                log.Debug("All dynamic channels are empty in: {0}," + guild.Name + ", Deleting all but one");
                foreach(SocketVoiceChannel channel in dynamicChannels)
                {
                    if(!IsLastDynoChannel(channel))
                    {
                        log.Debug("Deleting Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                        await channel.DeleteAsync(); // TODO: put in helper method to add logging
                    }
                    else // if this is the last channel
                    {
                        log.Debug("Renaming (if needed): " + channel.Id + " - \"" + channel.Name + "\"");
                        await TryRenameChannel(channel);
                    }
                }
            }
            // if all full
            else if (dynamicChannels.All(x => x.Users.Count >= 1))
            {
                // create one
                log.Debug("All dynamic channels are full in: " + guild.Name);
                await CreateChannel(guild);
            }
            // if some full
            else if (!dynamicChannels.All(x => x.Users.Count >= 1))
            {
                // delete only those needed & make sure last one is named right
                log.Debug("Not all channels are empty: " + guild.Name + " Deleting unecessary channels");
                var emptyChannels = dynamicChannels.Where(x => x.Users.Count == 0);
                for (int i = 0; i < emptyChannels.Count(); i++)
                {
                    if (i != emptyChannels.Count() - 1) // this isn't the last empty channel
                    {
                        await emptyChannels.ElementAt(i).DeleteAsync();
                    }
                    else //is is last channel
                    {
                        var lastEmptyChannel = emptyChannels.ElementAt(i);
                        log.Debug("Renaming (if needed): " + lastEmptyChannel.Id + " - \"" + lastEmptyChannel.Name + "\"");
                        await TryRenameChannel(lastEmptyChannel);
                    }
                }
            }
        }

        #region Channel Modifiers

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

        #endregion

        #region Helper Methods

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

        private async Task<bool> TryRenameChannel (SocketVoiceChannel voiceChannel)
        {
            if (!voiceChannel.Name.Equals(DEFAULT_DYNAMIC_CHANNEL_NAME))
            {
                string oldName = voiceChannel.Name;
                await voiceChannel.ModifyAsync(x => x.Name = DEFAULT_DYNAMIC_CHANNEL_NAME);
                log.Debug("Reverted Channel: " + voiceChannel.Id + " - \"" + oldName + "\"");
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task CreateChannel(SocketGuild guild)
        {
            // TODO: If no permissions exist?
            var newChannel = await guild.CreateVoiceChannelAsync(DEFAULT_DYNAMIC_CHANNEL_NAME);
            await newChannel.AddPermissionOverwriteAsync(dyno, new Discord.OverwritePermissions());
            log.Debug("Created Channel: " + newChannel.Id + " - \"" + newChannel.Name + "\"");
        }

        #endregion
    }
}
