namespace LunarLander
{
    public enum GameState
    {
        Paused,
        MainMenu,
        ShipCrashed,
        PassedLevel,
        BeatGame,
        Running
    }

    public enum MenuState
    {
        Main,
        Controls,
        HighScores,
        Credits
    }

    public class MenuItem
    {
        public string ItemName;
        public bool Selected;

        public MenuItem(string itemName)
        {
            ItemName = itemName;
            Selected = false;
        }

        public MenuItem(string itemName, bool selected)
        {
            ItemName = itemName;
            Selected = selected;
        }
    }
}