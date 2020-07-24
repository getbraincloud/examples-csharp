using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RelayTestApp
{
    enum ScreenState
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
        static public App app = new App();

        static public FormGame form;

        static public ScreenState screenState = ScreenState.Login; /* Current screen we are on */
        static public User user = null; /* Our user */
        static public Lobby lobby = null; /* Lobby with its members as received from brainCloud Lobby Service */
        static public Server server = null; /* Server info (IP, port, protocol, passcode) */
        static public List<Shockwave> shockwaves = new List<Shockwave>(); /* Players' created shockwaves */
        static public int mouseX = 0;
        static public int mouseY = 0;
    }
}
