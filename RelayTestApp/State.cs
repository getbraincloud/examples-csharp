using System.Collections.Generic;

namespace RelayTestApp
{
    public enum ScreenState
    {
        Login,
        LoggingIn,
        MainMenu,
        JoiningLobby,
        Lobby,
        Starting,
        Game
    }

    static class State
    {
        static public GameApp app = new GameApp();
        static public MainWindow form;

        static public ScreenState screenState = ScreenState.Login;
        static public User user = null;
        static public Lobby lobby = null;
        static public Server server = null;
        static public List<Shockwave> shockwaves = new List<Shockwave>();
        static public List<Splotch> splotches = new List<Splotch>();
        static public int mouseX = 0;
        static public int mouseY = 0;

        // Game session state
        static public long gameStartTime = 0;       // UTC epoch ms when current round started (0 = not in game)
        static public int  roundNumber   = 0;       // Increments each round within a lobby session
        static public int  splotchDurationSec = -1; // -1 = forever; >0 = lifetime in seconds
        static public string lobbyStatusText = "";  // Status shown on the joining-lobby screen
        static public List<string> appLobbies = new List<string>();  // Lobby types from GlobalProperties
        static public long lobbySearchStartTime = 0;   // UTC epoch ms when lobby search started
        static public long lobbyStatusStartTime = 0;   // UTC epoch ms when STARTING event fired
        static public Dictionary<string, int> pingData = new Dictionary<string, int>(); // our measured region latencies (ms)
    }
}
