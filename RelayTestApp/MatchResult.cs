using System.Collections.Generic;

namespace RelayTestApp
{
    // Mirrors the cpp reference client's globals.h MatchResult/MatchResultEntry/
    // LeaderboardDelta/LeaderboardPeriodDelta structs (also ported to Java as-is).
    class MatchResult
    {
        public class PeriodDelta
        {
            public bool improved = false;
            public int rankBefore = -1;
            public int rankAfter = -1;
        }

        public class LeaderboardDelta
        {
            public bool ready = false;
            public PeriodDelta pointsLifetime = new PeriodDelta();
            public PeriodDelta pointsQuarterly = new PeriodDelta();
            public PeriodDelta coverageLifetime = new PeriodDelta();
            public PeriodDelta coverageQuarterly = new PeriodDelta();
        }

        public class Entry
        {
            public string cxId = "";
            public int rank = 0;
            public float coveragePct = 0f;
            public int beaten = 0;
            public LeaderboardDelta lbDelta = new LeaderboardDelta();
        }

        public bool valid = false;
        public int round = -1;
        public List<Entry> entries = new List<Entry>();
    }
}
