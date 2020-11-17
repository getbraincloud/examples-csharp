using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RelayTestApp
{
    public partial class FormGame : Form
    {
        private Dictionary<User, PictureBox> m_cursors = new Dictionary<User, PictureBox>();

        public Label[] lblLobbyPlayers = new Label[40];

        static Color ColorFromHex(UInt32 hex)
        {
            return Color.FromArgb(
                (int)((hex) & 0xFF), 
                (int)((hex >> 8) & 0xFF), 
                (int)((hex >> 16 )& 0xFF));
        }

        static readonly Color[] COLORS = new Color[]
        {
            ColorFromHex(0xFF000000),
            ColorFromHex(0xFF5f4155),
            ColorFromHex(0xFF646964),
            ColorFromHex(0xFF5573d7),
            ColorFromHex(0xFFd78c50),
            ColorFromHex(0xFF64b964),
            ColorFromHex(0xFF6ec8e6),
            ColorFromHex(0xFFfff5dc)
        };

        public FormGame()
        {
            InitializeComponent();
        }

        private void FormGame_Load(object sender, EventArgs e)
        {
            // We hide tab control header
            screens.Appearance = TabAppearance.FlatButtons;
            screens.ItemSize = new Size(0, 1);
            screens.SizeMode = TabSizeMode.Fixed;
            foreach (TabPage tab in screens.TabPages)
            {
                tab.Text = "";
            }

            // Fill in defaults
            txtUsername.Text = Settings.username;
            txtPassword.Text = Settings.password;
            switch (Settings.protocol)
            {
                case BrainCloud.RelayConnectionType.UDP:
                    cboProtocol.SelectedIndex = 0;
                    break;
                case BrainCloud.RelayConnectionType.TCP:
                    cboProtocol.SelectedIndex = 1;
                    break;
                case BrainCloud.RelayConnectionType.WEBSOCKET:
                    cboProtocol.SelectedIndex = 2;
                    break;
            }

            // Lobby's color buttons
            btnColor0.BackColor = COLORS[0];
            btnColor1.BackColor = COLORS[1];
            btnColor2.BackColor = COLORS[2];
            btnColor3.BackColor = COLORS[3];
            btnColor4.BackColor = COLORS[4];
            btnColor5.BackColor = COLORS[5];
            btnColor6.BackColor = COLORS[6];
            btnColor7.BackColor = COLORS[7];

            // Player labels
            for (int i = 0; i < 40; ++i)
            {
                var label = new Label();
                var location = lblPlayer.Location;
                location.X += (i % 4) * 128;
                location.Y += (i / 4) * 24;
                label.Location = location;
                lblPlayer.Parent.Controls.Add(label);
                lblPlayer.Visible = false;
                lblLobbyPlayers[i] = label;
            }
            lblPlayer.Visible = false;
        }

        public void UpdateLobby()
        {
            for (int i = 0; i < 40; ++i)
            {
                if (i < State.lobby.members.Count)
                {
                    var user = State.lobby.members[i];
                    lblLobbyPlayers[i].Text = user.name;
                    lblLobbyPlayers[i].ForeColor = COLORS[user.colorIndex];
                    lblLobbyPlayers[i].Visible = true;
                }
                else
                {
                    lblLobbyPlayers[i].Visible = false;
                }
            }
        }

        public void UpdateGameViewport()
        {
            // Remove players from the list that are gone.
            // Here it's a bit more complex. We do this instead of clearing
            // the list and rebuilding it. Because we don't want the UI to flicker.
            for (int i = 0; i < panelPlayers.Controls.Count; ++i)
            {
                bool found = false;

                {
                    var checkbox = panelPlayers.Controls[i];
                    foreach (var user in State.lobby.members)
                    {
                        if (checkbox.Text == user.name)
                        {
                            found = true;
                            break;
                        }
                    }
                }

                if (!found)
                {
                    panelPlayers.Controls.RemoveAt(i);

                    // Shift position of all following
                    for (int j = i; j < panelPlayers.Controls.Count; ++j)
                    {
                        var checkbox = panelPlayers.Controls[j];
                        var location = checkbox.Location;
                        location.Y -= 24;
                        checkbox.Location = location;
                    }
                }
            }

            // Add new players
            foreach (var user in State.lobby.members)
            {
                bool found = false;
                for (int i = 0; i < panelPlayers.Controls.Count; ++i)
                {
                    if (panelPlayers.Controls[i].Text == user.name)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    var checkbox = new CheckBox();
                    checkbox.Text = user.name;
                    checkbox.ForeColor = COLORS[user.colorIndex];
                    checkbox.Checked = user.allowSendTo;
                    checkbox.Location = new Point(4, 4 + panelPlayers.Controls.Count * 24);
                    panelPlayers.Controls.Add(checkbox);
                }
            }
        }

        public void SetCursor(User user, Point pos)
        {
       /*     PictureBox pictureBox = null;
            if (m_cursors.ContainsKey(user))
            {
                pictureBox = m_cursors[user];
            }
            else
            {
                pictureBox = new PictureBox();
                pictureBox.Image = imageList.Images[user.colorIndex];
                pictureBox.Size = new Size(64, 64);

                picViewport.Controls.Add(pictureBox);
                m_cursors[user] = pictureBox;
            }

            pictureBox.Location = pos;*/
        }

        public void UpdateShockwaves()
        {
            var now = DateTime.Now;
            for (int i = 0; i < State.shockwaves.Count; ++i)
            {
                var shockwave = State.shockwaves[i];
                var elapsed = (now - shockwave.startTime).TotalMilliseconds;

                // Kill it if too old
                if (elapsed >= 1000.0)
                {
                    State.shockwaves.RemoveAt(i);
                    --i;
                }
            }

            picViewport.Invalidate();
        }

        public void UpdateMenuStates()
        {
            m_cursors.Clear();

            switch (State.screenState)
            {
                case ScreenState.Login:
                    logOutToolStripMenuItem.Enabled = false;
                    exitToolStripMenuItem.Enabled = true;
                    leaveToolStripMenuItem.Enabled = false;
                    break;
                case ScreenState.LoggingIn:
                    logOutToolStripMenuItem.Enabled = false;
                    exitToolStripMenuItem.Enabled = true;
                    leaveToolStripMenuItem.Enabled = false;
                    break;
                case ScreenState.MainMenu:
                    logOutToolStripMenuItem.Enabled = true;
                    exitToolStripMenuItem.Enabled = true;
                    leaveToolStripMenuItem.Enabled = false;
                    break;
                case ScreenState.JoiningLobby:
                    logOutToolStripMenuItem.Enabled = true;
                    exitToolStripMenuItem.Enabled = true;
                    leaveToolStripMenuItem.Enabled = false;
                    break;
                case ScreenState.Lobby:
                    logOutToolStripMenuItem.Enabled = true;
                    exitToolStripMenuItem.Enabled = true;
                    leaveToolStripMenuItem.Enabled = true;
                    break;
                case ScreenState.Starting:
                    logOutToolStripMenuItem.Enabled = true;
                    exitToolStripMenuItem.Enabled = true;
                    leaveToolStripMenuItem.Enabled = true;
                    break;
                case ScreenState.Game:
                    logOutToolStripMenuItem.Enabled = true;
                    exitToolStripMenuItem.Enabled = true;
                    leaveToolStripMenuItem.Enabled = true;
                    break;
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            State.app.Exit();
        }

        private void leaveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            State.app.CloseGame();
        }

        private void logOutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            State.app.LogOut();
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (txtUsername.Text.Length == 0 || txtPassword.Text.Length == 0) return;

            Settings.username = txtUsername.Text;
            Settings.password = txtPassword.Text;
            Settings.SaveConfigs();

            State.app.Login(Settings.username, Settings.password);
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            State.app.Update();
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            BrainCloud.RelayConnectionType protocol = BrainCloud.RelayConnectionType.UDP;
            switch (cboProtocol.SelectedIndex)
            {
                case 0: protocol = BrainCloud.RelayConnectionType.UDP; break;
                case 1: protocol = BrainCloud.RelayConnectionType.TCP; break;
                case 2: protocol = BrainCloud.RelayConnectionType.WEBSOCKET; break;
            }
            State.app.Play(protocol);
        }

        private void btnColor0_Click(object sender, EventArgs e)
        {
            State.app.ChangeUserColor(0);
        }

        private void btnColor1_Click(object sender, EventArgs e)
        {
            State.app.ChangeUserColor(1);
        }

        private void btnColor2_Click(object sender, EventArgs e)
        {
            State.app.ChangeUserColor(2);
        }

        private void btnColor3_Click(object sender, EventArgs e)
        {
            State.app.ChangeUserColor(3);
        }

        private void btnColor4_Click(object sender, EventArgs e)
        {
            State.app.ChangeUserColor(4);
        }

        private void btnColor5_Click(object sender, EventArgs e)
        {
            State.app.ChangeUserColor(5);
        }

        private void btnColor6_Click(object sender, EventArgs e)
        {
            State.app.ChangeUserColor(6);
        }

        private void btnColor7_Click(object sender, EventArgs e)
        {
            State.app.ChangeUserColor(7);
        }

        private void btnLeave_Click(object sender, EventArgs e)
        {
            State.app.CloseGame();
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            State.app.StartGame();
        }

        private void picViewport_MouseMove(object sender, MouseEventArgs e)
        {
            State.app.MouseMoved(e.Location);
        }

        private void picViewport_MouseDown(object sender, MouseEventArgs e)
        {
            State.app.Shockwave(e.Location);
        }

        private void picViewport_Paint(object sender, PaintEventArgs e)
        {
            // Shockwaves
            var now = DateTime.Now;
            foreach (var shockwave in State.shockwaves)
            {
                var elapsed = (now - shockwave.startTime).Milliseconds;

                // Calculate percent
                float percent = (float)elapsed / 1000.0f;

                // East out
                percent = 1.0f - (1.0f - percent) * (1.0f - percent);

                // Display
                float size = percent * 64.0f;
                e.Graphics.DrawEllipse(
                    new Pen(COLORS[shockwave.colorIndex], 1f),
                    (float)shockwave.pos.X - size * 0.5f,
                    (float)shockwave.pos.Y - size * 0.5f,
                    size, size);
            }

            // Cursors
            foreach (var user in State.lobby.members)
            {
                if (user.isAlive)
                {
                    e.Graphics.DrawImage(imageList.Images[user.colorIndex], user.pos);
                }
            }
        }
    }
}
