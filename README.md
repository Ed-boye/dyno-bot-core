```diff
- Dyno is not yet deployed utilizng this version; auto add/remove is not live.
- These instructions do not (fully) apply to the current version out there.
```

# Dyno Bot
A dynamic channel creator for Discord (now in .NET Core)

## Overview

Dyno will rename a voice channel you determine as dynamic (via Dyno's role) to the game currently being played by users in it. Additional channels will also be added/removed as users enter/leave existing dynamic voice channels

## Quick Start

1. [Add Dyno](https://discordapp.com/api/oauth2/authorize?client_id=373286855023656962&scope=bot&permissions=268435472) to one of your servers
2. Now add Dyno (the "MEMBER") as a role to a channel (and only one channel) you'd like to make dynamic
    * Cog --> Permissions --> ROLES/MEMBERS (+) --> Dyno
    * Remember, the Dyno under "MEMBERS" not "ROLES"; if you fuck this up, it's on you
3. Join that channel and Dyno gets to work

## Notes
Some quick notes
* Dyno will create another voice channel as long as someone is in an existing dynamic channel
* Dyno will remove all but one dynamic voice channel if all dyanmic voice channels are empty
* Dyno does not care about your channel categories, he says, "Fuck that shit, that ain't me," in Dyno speak
    * Dyno will learn to handle this later, somehow...

## Built With
* [Discord.Net](https://github.com/RogueException/Discord.Net)
