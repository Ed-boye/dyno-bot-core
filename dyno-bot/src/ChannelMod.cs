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

        // TODO: Add Guild Name to Debug
        public async Task RenewChannels(SocketVoiceChannel channel, bool userJoined) 
        {
            log.Debug("Renewing Channel: " + channel.Id + " - \"" + channel.Guild.Name + "\\" + channel.Name + "\"");
            //if only user
            if (channel.Users.Count == 1)
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

                if (userJoined)
                {
                    // Add a new channel
                    // TODO: Check if we can create a channel with permissions already on it, Discord API seems to support this...
                    var newChannel = await channel.Guild.CreateVoiceChannelAsync("Join to change name");
                    await newChannel.AddPermissionOverwriteAsync(dyno, channel.GetPermissionOverwrite(dyno).Value);
                    log.Debug("Created New Channel: " + newChannel.Id + " - \"" + newChannel.Name + "\"");
                }                
                
            }
            else if (channel.Users.Count > 1)
            {
                Game? topGame = findTopGameInChannel(channel);
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
            else // nobody here
            {
                if(!checkLastChannel(channel.Guild) && !userJoined)
                {
                    await channel.DeleteAsync();
                    log.Debug("Deleted Empty Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                }
                else 
                {
                    // TODO: Don't revert if not needed?
                    await channel.ModifyAsync(x => x.Name = "Join to change name");
                    log.Debug("Reverted final dynamic Channel: " + channel.Id + " - \"" + channel.Name + "\"");
                }
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
        private bool checkLastChannel(SocketGuild guild) 
        {
            // TODO turn this into a linq expression, probably...
            int dynamicChannels = 0;
            foreach(SocketVoiceChannel channel in guild.VoiceChannels) 
            {
                // TODO: Check this call out? If not null may be a better check
                // channel.GetPermissionOverwrite(dyno); 
                if(channel.PermissionOverwrites.ToList().Exists(x => x.TargetId == dyno.Id))
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
