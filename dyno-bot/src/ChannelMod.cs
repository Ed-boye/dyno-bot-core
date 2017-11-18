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
            
            // Proper ratio
            if (dynamicChannels.FindAll(x => x.Users.Count >= 1).Count == dynamicChannels.Count - 1)
            {
                foreach(var channel in dynamicChannels) 
                {
                    await TryRenameChannel(channel);
                }
            }
            // Ratio is off 
            else if (dynamicChannels.FindAll(x => x.Users.Count == 0).Count != 1)
            {
                await TryFixRatio(guild);
            }
        }

        #region Channel Modifiers

        public async Task UpdateOldChannel(SocketVoiceChannel channel) 
        {
            if (channel.Users.Count == 0 && !channel.Name.Equals(DEFAULT_DYNAMIC_CHANNEL_NAME))
            {
                log.Debug("UPDATE OLD");
                await TryFixRatio(channel.Guild);
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
                
                // Newly joined channel (one user), create a new channel if needed
                if(GetDynamicChannels(channel.Guild).FindAll(x => x.Users.Count == 0).Count != 1) 
                {
                    await CreateChannel(channel.Guild);
                }
                //await TryFixRatio(channel.Guild);

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
            if (topGame != null && !channel.Name.Equals(topGame.Value.Name)) 
            {
                await channel.ModifyAsync(x => x.Name = topGame.Value.Name);
                log.Debug("Renamed Channel: " + channel.Id + " - \"" + channel.Name + "\" to \"" + topGame.Value.Name + "\"");
            }
            else // no game being played
            {
                log.Debug("NOT Modifying Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                // Don't rename, someone already changed it or is already set to default
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
                    topGame = list.ElementAt(list.Count - 1).Key;
                }
            }
            return topGame;
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
            if (voiceChannel.Users.Count == 0 && !voiceChannel.Name.Equals(DEFAULT_DYNAMIC_CHANNEL_NAME))
            {
                string oldName = voiceChannel.Name;
                await voiceChannel.ModifyAsync(x => x.Name = DEFAULT_DYNAMIC_CHANNEL_NAME);
                log.Debug("Reverted Channel: " + voiceChannel.Id + " - \"" + oldName + "\"");
                return true;
            }
            else if(voiceChannel.Users.Count >= 1) 
            {
                await UpdateToTopGame(voiceChannel);
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task TryFixRatio(SocketGuild guild)
        {
            var dynamicChannels = GetDynamicChannels(guild);
            // More than one empty channel
            if (dynamicChannels.FindAll(x => x.Users.Count == 0).Count > 1)
            {
                var emptyChannels = dynamicChannels.Where(x => x.Users.Count == 0).ToList();
                var keepChannels = dynamicChannels.Where(x => x.Users.Count >= 1).ToList();
                keepChannels.Add(emptyChannels.ElementAt(0));
                emptyChannels.RemoveAt(0);
                
                foreach(var channel in emptyChannels)
                {
                    await channel.DeleteAsync();
                }
                
                foreach(var channel in keepChannels)
                {
                    await TryRenameChannel(channel);
                }
            }
            // No empty channels
            else if (dynamicChannels.FindAll(x => x.Users.Count == 0).Count < 1)
            {
                await CreateChannel(guild);
            }
            // Ratio is correct
            else 
            {
                foreach(var channel in dynamicChannels.FindAll(x => x.Users.Count == 0))
                {
                    await TryRenameChannel(channel);
                }
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
