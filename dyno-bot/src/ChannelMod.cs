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

        public async Task UpdateOldDynamicChannel(SocketVoiceChannel channel) 
        {
            
            if (channel.Users.Count == 0 /*&& channel.Name.Equals(DEFAULT_DYNAMIC_CHANNEL_NAME)*/)
            {
                await TryFixRatio(channel.Guild);
            }
            else if (channel.Users.Count == 1)
            {
                await UpdateSingleUserChannel(channel);
            }
            else //(channel.Users.Count > 1)
            {
                await TryUpdateToTopGame(channel);
            }
        }

        public async Task UpdateNewDynamicChannel (SocketVoiceChannel channel)
        {
            if (channel.Users.Count == 0)
            {
                // Shouldn't really hit this ever if called right
            }
            else if (channel.Users.Count == 1)
            {
                // Newly joined channel has one user in it
                await UpdateSingleUserChannel(channel);
            }
            else //(channel.Users.Count > 1)
            {
                // Newly joined channel has users in it
                await TryUpdateToTopGame(channel);
            }
            
            // If no empty dynamic channels exist after joining, create one
            if(GetDynamicChannels(channel.Guild).FindAll(x => x.Users.Count == 0).Count == 0) 
            {
                await CreateChannel(channel.Guild);
            }
        }

        public async Task UpdateCurrentDynamicChannel (SocketVoiceChannel channel) 
        {
            if (channel.Users.Count == 0)
            {
                // Shouldn't hit this
            }
            else if (channel.Users.Count == 1)
            {
                await UpdateSingleUserChannel(channel);
            }
            else //(channel.Users.Count > 1)
            {
                await TryUpdateToTopGame(channel);
            }
        }

        #endregion

        #region Helper Methods

        private async Task UpdateSingleUserChannel(SocketVoiceChannel voiceChannel)
        {
            if (voiceChannel.Users.First().Game != null)
            {
                // Set name to user's game
                string gameName = gamesRepo.GetFriendlyName(voiceChannel.Users.First().Game.Value.Name);
                await voiceChannel.ModifyAsync(x => x.Name = gameName);
                log.Debug("Renamed channel: " + voiceChannel.Id + " - \"" + voiceChannel.Name + "\" to \"" + gameName + "\"");
            }
            else
            {
                var user = voiceChannel.Users.First();
                await TryRenameVoiceChannelToUser(voiceChannel, user);
            }
        }

        private async Task<bool> TryRenameChannel (SocketVoiceChannel voiceChannel)
        {
            if (voiceChannel.Users.Count == 0 && !voiceChannel.Name.Equals(DEFAULT_DYNAMIC_CHANNEL_NAME))
            {
                string oldName = voiceChannel.Name;
                await voiceChannel.ModifyAsync(x => x.Name = DEFAULT_DYNAMIC_CHANNEL_NAME);
                log.Debug("Reverted channel: " + voiceChannel.Id + " - \"" + oldName + "\"");
                return true;
            }
            /*else if (voiceChannel.Users.Count == 1)
            {
                await TryRenameVoiceChannelToUser(voiceChannel, voiceChannel.Users.First());
                return true;
            }*/
            else if(voiceChannel.Users.Count >= 1) 
            {
                if(!await TryUpdateToTopGame(voiceChannel))
                {
                    // If no top game was updated, then just default to the first user's name, whatever.
                    // TODO: Currently changing the channels name even if a game was previously being played.
                    // If channel contains name of something currently being played.
                    var user = voiceChannel.Users.First();
                    log.Debug("No top game in channel: " + voiceChannel.Id + " - \"" + voiceChannel.Name + "\"");
                    await TryRenameVoiceChannelToUser(voiceChannel, user);
                };
                return true;
            }
            else
            {
                return false;
            }
        }

        private async Task<bool> TryUpdateToTopGame(SocketVoiceChannel channel)
        {
            var topGame = TryGetTopGameInChannel(channel);
            if (topGame != null && (!channel.Name.Equals(topGame.Value.Name) || !channel.Name.Equals(gamesRepo.GetFriendlyName(topGame.Value.Name)))) 
            {
                var oldChannelName = channel.Name;
                await channel.ModifyAsync(x => x.Name = gamesRepo.GetFriendlyName(topGame.Value.Name));
                log.Debug("TryUpdateToTopGame -> Renamed channel: " + channel.Id + " - \"" + oldChannelName + "\" to \"" + channel.Name + "\"");
                return true;
            }
            else // Already top game set or game not being played.
            {
                return false;
            }
        }

        public async Task TryFixRatio(SocketGuild guild)
        {
            var dynamicChannels = GetDynamicChannels(guild);
            // More than one empty channel
            if (dynamicChannels.FindAll(x => x.Users.Count == 0).Count > 1)
            {
                var emptyChannels = dynamicChannels.Where(x => x.Users.Count == 0).ToList();
                var keepChannels = dynamicChannels.Where(x => x.Users.Count >= 1).ToList();
                keepChannels.Add(emptyChannels.ElementAt(0));
                emptyChannels.RemoveAt(0);
                
                await Task.WhenAll(
                    Task.Factory.StartNew(async () => 
                    {
                        foreach(var channel in emptyChannels)
                        {
                            await channel.DeleteAsync();
                        }
                    }),
                    Task.Factory.StartNew(async () => 
                    {
                        foreach(var channel in keepChannels)
                        {
                            await TryRenameChannel(channel);
                        }
                    })
                );
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
            return guild.VoiceChannels.ToList()
                .FindAll(x => x.PermissionOverwrites.ToList()
                .Exists(y => y.TargetId == dyno.Id));
        }

        private async Task CreateChannel(SocketGuild guild)
        {
            var newChannel = await guild.CreateVoiceChannelAsync(DEFAULT_DYNAMIC_CHANNEL_NAME);
            await newChannel.AddPermissionOverwriteAsync(dyno, new Discord.OverwritePermissions());
            log.Debug("Created channel: " + newChannel.Id + " - \"" + newChannel.Name + "\"");
        }

        private async Task<bool> TryRenameVoiceChannelToUser(SocketVoiceChannel voiceChannel, SocketGuildUser user)
        {
            // TODO: This is probably garbage way
            if(user.Nickname != null && (!voiceChannel.Name.Contains(user.Nickname) || !voiceChannel.Name.Contains(user.Username)))
            {
                await voiceChannel.ModifyAsync(x => x.Name = user.Nickname + "'s Domain");
                log.Debug("Renamed channel: " + voiceChannel.Id + " - \"" + voiceChannel.Name + "\" to \"" + user.Nickname + "'s Domain\"");
                return true;
            }
            else if (!voiceChannel.Name.Contains(user.Username))
            {
                await voiceChannel.ModifyAsync(x => x.Name = user.Username + "'s Domain");
                log.Debug("Renamed channel: " + voiceChannel.Id + " - \"" + voiceChannel.Name + "\" to \"" + user.Username + "'s Domain\"");
                return true;
            }

            return false;
        }

        #endregion
    }
}
