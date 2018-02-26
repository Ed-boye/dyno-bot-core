# Dyno Bot
A dynamic channel creator for Discord (now in .NET Core)

## Overview

Dyno will rename a voice channel you determine as dynamic (via Dyno's role) to the game currently being played by users in it. Additional channels will also be added/removed as users enter/leave existing dynamic voice channels

## Quick Start

1. [Add Dyno](https://discordapp.com/api/oauth2/authorize?client_id=379109864548335620&scope=bot&permissions=268435472) (in Beta) to one of your servers
2. Now add Dyno (the "MEMBER") as a role to a channel (and only one channel) you'd like to make dynamic
    * Edit Channel Cog --> Permissions --> ROLES/MEMBERS (+) --> MEMBERS/Dyno Test
    * Remember, the Dyno under "MEMBERS" not "ROLES"; if you fuck this up, it's on you
    * You may add Dyno to a whole category but keep in mind that once you do that, all channels in the cateogry will be trated as dynamic channels
3. Join that channel and Dyno gets to work

## Notes
Some quick notes:
* Dyno will create an additional voice channel whenever all voice channels are full
* Dyno will remove all but one dynamic voice channel if all dyanmic voice channels are empty
* Dyno has learned to honor channel categories recently

## Built With
* [Discord.Net](https://github.com/RogueException/Discord.Net)
