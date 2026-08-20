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
        Game,
        MatchSummary
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
        static public long lobbyJoinedAtMs = 0; // UTC epoch ms when this lobby was first entered; drives the INFO tab's "time in lobby"

        // Global chat (this-lobby chat lives on Lobby.chatMessages instead, since it
        // must be carried forward across the wholesale Lobby rebuilds OnLobbyEvent does)
        static public List<ChatMessage> chatMessagesGlobal = new List<ChatMessage>();

        // Leaderboard ids — defaults match the cpp reference client; overridable via
        // the same Global App Properties mechanism as Colors/SplotchDuration above.
        static public string pointsLeaderboardId = "CursorParty_Points";
        static public string pointsLeaderboardIdQuarterly = "CursorParty_Points_Quarterly";
        static public string coverageLeaderboardId = "CursorParty_HighestCoverage";
        static public string coverageLeaderboardIdQuarterly = "CursorParty_HighestCoverage_Quarterly";

        // Coverage tracking
        static public List<Coverage.CoverageEntry> coverage = new List<Coverage.CoverageEntry>(); // live in-match rank board
        static public long splotchGeneration = 0;   // bumped on every splotch add/clear/sync; gates the debounced recompute below
        static public long coverageComputedGen = -1;
        static public long coverageComputedAtMs = 0;

        // Match summary / post-match leaderboard posting
        static public MatchResult matchResult = new MatchResult(); // valid=false until the host's broadcast (or a local fallback) lands
        static public long matchSummaryArrivalTime = 0; // UTC epoch ms, drives the 45s auto-queue and 8s "leaderboard unavailable" timeouts
        static public int leaderboardPostedRound = -1;  // idempotency guard — the points board is cumulative, a double-post can't be undone
    }
}
