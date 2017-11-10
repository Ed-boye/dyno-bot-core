using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Net;
using Discord.WebSocket;

namespace Dynobot.Services 
{
    public class ChannelMod
    {
        IUser dyno;
        public ChannelMod (IUser dyno) 
        {
           this.dyno = dyno;
        }

        public async Task RenewChannels(SocketVoiceChannel channel) 
        {
            //if only user
            if (channel.Users.Count == 1)
            {
                if (channel.Users.First().Game != null)
                {
                    // Set name to user's game
                    await channel.ModifyAsync(x => x.Name = channel.Users.First().Game.Value.Name);

                    // Add a new channel
                    ///
                    // TODO: Why aren't I able to add dyno to the channel?
                    ///
                    var newChannel = await channel.Guild.CreateVoiceChannelAsync("Join to change name");
                    await newChannel.AddPermissionOverwriteAsync(dyno, channel.GetPermissionOverwrite(dyno).Value);
                }
                else 
                {
                    // Set name to user's name
                    await channel.ModifyAsync(x => x.Name = channel.Users.First().Username + "'s Channel");

                    // Add a new channel
                    var newChannel = await channel.Guild.CreateVoiceChannelAsync("Join to change name");
                    await newChannel.AddPermissionOverwriteAsync(dyno, channel.GetPermissionOverwrite(dyno).Value);
                }
            }
            else if (channel.Users.Count > 1)
            {
                var topGame = findTopGameInChannel(channel);
                if (topGame != null) 
                {
                    await channel.ModifyAsync(x => x.Name = topGame.Value.Name);
                }
                else // no game being played
                {
                    //await channel.ModifyAsync(x => x.Name = "Join to Change");
                    //Don't rename, someone already changed it or is already set to default
                }
            }
            else // nobody here
            {
                await channel.ModifyAsync(x => x.Name = "Join to change name");
                if(!checkLastChannel(channel.Guild))
                {
                    await channel.DeleteAsync();
                    // TODO: Currently deletes and creates if you switch from one dynamic to the second dynamic, fix is probably needed here?
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