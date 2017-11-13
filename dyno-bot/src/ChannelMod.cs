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
        IUser dyno;
        log4net.ILog log;
        public ChannelMod (IUser dyno, ILog log) 
        {
           this.dyno = dyno;
           this.log = log;
        }

        public async Task UpdateOldChannel(SocketVoiceChannel channel) 
        {
            if (channel.Users.Count == 0)
            {
                await revertOrDelete(channel);
            }
            else if (channel.Users.Count == 1)
            {
                await updateOneUser(channel);
            }
            else //(channel.Users.Count > 1)
            {
                await updateToTopGame(channel);
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
                await updateOneUser(channel);
                
                // Only user here, create a new channel
                var newChannel = await channel.Guild.CreateVoiceChannelAsync("Join to change name");
                await newChannel.AddPermissionOverwriteAsync(dyno, channel.GetPermissionOverwrite(dyno).Value);
                log.Debug("Nu Created New Channel: " + newChannel.Id + " - \"" + newChannel.Name + "\"");
            }
            else //(channel.Users.Count > 1)
            {
                await updateToTopGame(channel);
            }
        }

        public async Task UpdateExistingChannel (SocketVoiceChannel channel) 
        {
            if (channel.Users.Count == 0)
            {
                // Shouldn't hit this
            }
            else if (channel.Users.Count == 1)
            {
                await updateOneUser(channel);
            }
            else //(channel.Users.Count > 1)
            {
                await updateToTopGame(channel);
            }
        }

        // TODO: Currently delets all channels vs just leaving one. Fix
        public async Task RenewAllChannels(SocketVoiceChannel channel) 
        {
            //log.Debug("Renewing Channel: " + channel.Id + " - \"" + channel.Guild.Name + "\\" + channel.Name + "\"");
            //if only user
            if (channel.Users.Count == 1)
            {
                await updateOneUser(channel);            
            }
            else if (channel.Users.Count > 1)
            {
                await updateToTopGame(channel);
                if(checkIsLastChannel(channel))
                {
                    var newChannel = await channel.Guild.CreateVoiceChannelAsync("Join to change name");
                    await newChannel.AddPermissionOverwriteAsync(dyno, channel.GetPermissionOverwrite(dyno).Value);
                    log.Debug("RA Created New Channel: " + newChannel.Id + " - \"" + newChannel.Name + "\"");
                }
            }
            else // nobody here
            {
                await revertOrDelete(channel);
            }
        }

        private async Task updateOneUser(SocketVoiceChannel channel)
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
                    string userChannelName = channel.Users.First().Username + "'s Domain";
                    await channel.ModifyAsync(x => x.Name = userChannelName);
                    log.Debug("Renamed Channel: " + channel.Id + " - \"" + channel.Name + "\" to \"" + userChannelName + "\"");
                }
        }

        private async Task updateToTopGame(SocketVoiceChannel channel)
        {
            Game? topGame = findTopGameInChannel(channel);
            if (topGame != null) 
            {
                await channel.ModifyAsync(x => x.Name = topGame.Value.Name);
                log.Debug("Renamed Channel: " + channel.Id + " - \"" + channel.Name + "\" to \"" + topGame.Value.Name + "\"");
            }
            else // no game being played
            {
                log.Debug("Nu NOT Modifying Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                //await channel.ModifyAsync(x => x.Name = "Join to Change");
                //Don't rename, someone already changed it or is already set to default
            }
        }

        private async Task revertOrDelete(SocketVoiceChannel channel) 
        {
            if(!checkIsLastChannel(channel))
                {
                    await channel.DeleteAsync();
                    log.Debug("OL Deleted Empty Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                }
                else 
                {
                    // TODO: Don't revert if not needed?
                    await channel.ModifyAsync(x => x.Name = "Join to change name");
                    log.Debug("OL Reverted final dynamic Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                }
        }

        // Try to find the top game, return null if no games exist in channel or no majority game
        private Game? findTopGameInChannel(SocketVoiceChannel channel) 
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
                list.Reverse();
                if (list.ElementAt(0).Value == list.ElementAt(1).Value)
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
        private bool checkIsLastChannel(SocketVoiceChannel channel) 
        {
            // TODO turn this into a linq expression, probably...
            int dynamicChannels = 0;
            foreach(SocketVoiceChannel voiceChannel in channel.Guild.VoiceChannels) 
            {
                // TODO: Check this call out? If not null may be a better check
                // channel.GetPermissionOverwrite(dyno); 
                if(voiceChannel.PermissionOverwrites.ToList().Exists(x => x.TargetId == dyno.Id))
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
    }
}
