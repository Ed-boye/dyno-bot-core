using System.Collections.Generic;

namespace Dynobot.Repositories 
{
    public class GameRepository
    {
        private Dictionary<string, string> gamesTable;
        public GameRepository() 
        {
            gamesTable = new Dictionary<string, string>
            {
                { "PLAYERUNKNOWN'S BATTLEGROUNDS", "PUBG" },
                { "Counter-Strike Global Offensive", "CS:GO" }
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