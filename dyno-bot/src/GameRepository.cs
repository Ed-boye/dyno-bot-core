using System.Collections.Generic;

namespace Dynobot.Repositories 
{
    public class GameRepository
    {
        private Dictionary<string, string> gamesTable;
        public GameRepository() 
        {
            // TODO: At least get from file.
            gamesTable = new Dictionary<string, string>
            {
                { "PLAYERUNKNOWN'S BATTLEGROUNDS", "PUBG" },
                { "Counter-Strike Global Offensive", "CS:GO" },
                { "Tom Clancy's Rainbow Six Siege", "R6 Siege" },
                { "Leage of Legends", "LoL"},
                { "Spotify", "Listening to Music"}
            };
        }

        public string GetFriendlyName(string game)
        {
            string friendlyName;
            gamesTable.TryGetValue(game, out friendlyName);
            return friendlyName != null ? friendlyName : game;
        }
    }
}