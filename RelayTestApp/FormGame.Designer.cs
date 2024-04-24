namespace RelayTestApp
{
    partial class FormGame
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormGame));
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.appToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.logOutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.gameToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.leaveToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.screens = new System.Windows.Forms.TabControl();
            this.screenLogin = new System.Windows.Forms.TabPage();
            this.btnLogin = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.screenLoggingIn = new System.Windows.Forms.TabPage();
            this.lblLoadingText = new System.Windows.Forms.Label();
            this.screenMainMenu = new System.Windows.Forms.TabPage();
            this.btnPlay = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.cboProtocol = new System.Windows.Forms.ComboBox();
            this.screenJoiningLobby = new System.Windows.Forms.TabPage();
            this.label47 = new System.Windows.Forms.Label();
            this.screenLobby = new System.Windows.Forms.TabPage();
            this.lblPlayer = new System.Windows.Forms.Label();
            this.btnColor7 = new System.Windows.Forms.Button();
            this.btnColor6 = new System.Windows.Forms.Button();
            this.btnColor5 = new System.Windows.Forms.Button();
            this.btnColor4 = new System.Windows.Forms.Button();
            this.btnColor3 = new System.Windows.Forms.Button();
            this.btnColor2 = new System.Windows.Forms.Button();
            this.btnColor1 = new System.Windows.Forms.Button();
            this.btnColor0 = new System.Windows.Forms.Button();
            this.btnStart = new System.Windows.Forms.Button();
            this.btnLeave = new System.Windows.Forms.Button();
            this.screenStarting = new System.Windows.Forms.TabPage();
            this.label7 = new System.Windows.Forms.Label();
            this.screenGame = new System.Windows.Forms.TabPage();
            this.picViewport = new System.Windows.Forms.Panel();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.chkRememberMe = new System.Windows.Forms.CheckBox();
            this.chkOrdered = new System.Windows.Forms.CheckBox();
            this.chkReliable = new System.Windows.Forms.CheckBox();
            this.cboChannels = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.panelPlayers = new System.Windows.Forms.Panel();
            this.label1 = new System.Windows.Forms.Label();
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.imageList = new System.Windows.Forms.ImageList(this.components);
            this.menuStrip.SuspendLayout();
            this.screens.SuspendLayout();
            this.screenLogin.SuspendLayout();
            this.screenLoggingIn.SuspendLayout();
            this.screenMainMenu.SuspendLayout();
            this.screenJoiningLobby.SuspendLayout();
            this.screenLobby.SuspendLayout();
            this.screenStarting.SuspendLayout();
            this.screenGame.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.appToolStripMenuItem,
            this.gameToolStripMenuItem});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(950, 24);
            this.menuStrip.TabIndex = 3;
            this.menuStrip.Text = "menuStrip";
            // 
            // appToolStripMenuItem
            // 
            this.appToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.logOutToolStripMenuItem,
            this.toolStripMenuItem1,
            this.exitToolStripMenuItem});
            this.appToolStripMenuItem.Name = "appToolStripMenuItem";
            this.appToolStripMenuItem.Size = new System.Drawing.Size(41, 20);
            this.appToolStripMenuItem.Text = "App";
            // 
            // logOutToolStripMenuItem
            // 
            this.logOutToolStripMenuItem.Enabled = false;
            this.logOutToolStripMenuItem.Name = "logOutToolStripMenuItem";
            this.logOutToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.logOutToolStripMenuItem.Text = "Log Out";
            this.logOutToolStripMenuItem.Click += new System.EventHandler(this.logOutToolStripMenuItem_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(177, 6);
            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.exitToolStripMenuItem.Text = "Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.exitToolStripMenuItem_Click);
            // 
            // gameToolStripMenuItem
            // 
            this.gameToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.leaveToolStripMenuItem});
            this.gameToolStripMenuItem.Name = "gameToolStripMenuItem";
            this.gameToolStripMenuItem.Size = new System.Drawing.Size(50, 20);
            this.gameToolStripMenuItem.Text = "Game";
            // 
            // leaveToolStripMenuItem
            // 
            this.leaveToolStripMenuItem.Enabled = false;
            this.leaveToolStripMenuItem.Name = "leaveToolStripMenuItem";
            this.leaveToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.leaveToolStripMenuItem.Text = "Leave";
            this.leaveToolStripMenuItem.Click += new System.EventHandler(this.leaveToolStripMenuItem_Click);
            // 
            // screens
            // 
            this.screens.Controls.Add(this.screenLogin);
            this.screens.Controls.Add(this.screenLoggingIn);
            this.screens.Controls.Add(this.screenMainMenu);
            this.screens.Controls.Add(this.screenJoiningLobby);
            this.screens.Controls.Add(this.screenLobby);
            this.screens.Controls.Add(this.screenStarting);
            this.screens.Controls.Add(this.screenGame);
            this.screens.Dock = System.Windows.Forms.DockStyle.Fill;
            this.screens.Location = new System.Drawing.Point(0, 24);
            this.screens.Name = "screens";
            this.screens.SelectedIndex = 0;
            this.screens.Size = new System.Drawing.Size(950, 575);
            this.screens.TabIndex = 6;
            // 
            // screenLogin
            // 
            this.screenLogin.BackColor = System.Drawing.SystemColors.Control;
            this.screenLogin.Controls.Add(this.btnLogin);
            this.screenLogin.Controls.Add(this.label3);
            this.screenLogin.Controls.Add(this.txtPassword);
            this.screenLogin.Controls.Add(this.label4);
            this.screenLogin.Controls.Add(this.txtUsername);
            this.screenLogin.Controls.Add(this.label5);
            this.screenLogin.Controls.Add(this.chkRememberMe);
            this.screenLogin.Location = new System.Drawing.Point(4, 22);
            this.screenLogin.Name = "screenLogin";
            this.screenLogin.Size = new System.Drawing.Size(942, 549);
            this.screenLogin.TabIndex = 1;
            this.screenLogin.Text = "Login";
            // 
            // chkRememberMe
            // 
            this.chkRememberMe.AutoSize = true;
            this.chkRememberMe.Checked = true;
            this.chkRememberMe.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkRememberMe.Location = new System.Drawing.Point(338, 300);
            this.chkRememberMe.Name = "chkRememberMe";
            this.chkRememberMe.Size = new System.Drawing.Size(64, 17);
            this.chkRememberMe.TabIndex = 1;
            this.chkRememberMe.Text = "Remember me";
            this.chkRememberMe.UseVisualStyleBackColor = true;
            // 
            // btnLogin
            // 
            this.btnLogin.Location = new System.Drawing.Point(338, 323);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(75, 23);
            this.btnLogin.TabIndex = 17;
            this.btnLogin.Text = "Login";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(552, 274);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(53, 13);
            this.label3.TabIndex = 16;
            this.label3.Text = "Password";
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(338, 271);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.PasswordChar = '*';
            this.txtPassword.Size = new System.Drawing.Size(208, 20);
            this.txtPassword.TabIndex = 15;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(552, 248);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(55, 13);
            this.label4.TabIndex = 14;
            this.label4.Text = "Username";
            // 
            // txtUsername
            // 
            this.txtUsername.Location = new System.Drawing.Point(338, 245);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(208, 20);
            this.txtUsername.TabIndex = 13;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(335, 229);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(211, 13);
            this.label5.TabIndex = 12;
            this.label5.Text = "A new user will be created if it doesn\'t exist.";
            // 
            // screenLoggingIn
            // 
            this.screenLoggingIn.BackColor = System.Drawing.SystemColors.Control;
            this.screenLoggingIn.Controls.Add(this.lblLoadingText);
            this.screenLoggingIn.Location = new System.Drawing.Point(4, 22);
            this.screenLoggingIn.Name = "screenLoggingIn";
            this.screenLoggingIn.Size = new System.Drawing.Size(942, 549);
            this.screenLoggingIn.TabIndex = 4;
            this.screenLoggingIn.Text = "Logging In";
            // 
            // lblLoadingText
            // 
            this.lblLoadingText.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.lblLoadingText.Location = new System.Drawing.Point(317, 235);
            this.lblLoadingText.Name = "lblLoadingText";
            this.lblLoadingText.Size = new System.Drawing.Size(309, 78);
            this.lblLoadingText.TabIndex = 1;
            this.lblLoadingText.Text = "Logging in ...";
            this.lblLoadingText.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // screenMainMenu
            // 
            this.screenMainMenu.BackColor = System.Drawing.SystemColors.Control;
            this.screenMainMenu.Controls.Add(this.btnPlay);
            this.screenMainMenu.Controls.Add(this.label6);
            this.screenMainMenu.Controls.Add(this.cboProtocol);
            this.screenMainMenu.Location = new System.Drawing.Point(4, 22);
            this.screenMainMenu.Name = "screenMainMenu";
            this.screenMainMenu.Size = new System.Drawing.Size(942, 549);
            this.screenMainMenu.TabIndex = 2;
            this.screenMainMenu.Text = "Main Menu";
            // 
            // btnPlay
            // 
            this.btnPlay.Location = new System.Drawing.Point(334, 276);
            this.btnPlay.Name = "btnPlay";
            this.btnPlay.Size = new System.Drawing.Size(75, 23);
            this.btnPlay.TabIndex = 5;
            this.btnPlay.Text = "Play";
            this.btnPlay.UseVisualStyleBackColor = true;
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(563, 252);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(46, 13);
            this.label6.TabIndex = 4;
            this.label6.Text = "Protocol";
            // 
            // cboProtocol
            // 
            this.cboProtocol.DisplayMember = "UDP";
            this.cboProtocol.FormattingEnabled = true;
            this.cboProtocol.Items.AddRange(new object[] {
            "UDP",
            "TCP",
            "WS"});
            this.cboProtocol.Location = new System.Drawing.Point(334, 249);
            this.cboProtocol.Name = "cboProtocol";
            this.cboProtocol.Size = new System.Drawing.Size(223, 21);
            this.cboProtocol.TabIndex = 3;
            this.cboProtocol.Text = "Protocol";
            this.cboProtocol.ValueMember = "UDP";
            // 
            // screenJoiningLobby
            // 
            this.screenJoiningLobby.BackColor = System.Drawing.SystemColors.Control;
            this.screenJoiningLobby.Controls.Add(this.label47);
            this.screenJoiningLobby.Location = new System.Drawing.Point(4, 22);
            this.screenJoiningLobby.Name = "screenJoiningLobby";
            this.screenJoiningLobby.Size = new System.Drawing.Size(942, 549);
            this.screenJoiningLobby.TabIndex = 5;
            this.screenJoiningLobby.Text = "Joining Lobby";
            // 
            // label47
            // 
            this.label47.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label47.Location = new System.Drawing.Point(317, 235);
            this.label47.Name = "label47";
            this.label47.Size = new System.Drawing.Size(309, 78);
            this.label47.TabIndex = 2;
            this.label47.Text = "Joining lobby...";
            this.label47.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // screenLobby
            // 
            this.screenLobby.BackColor = System.Drawing.SystemColors.Control;
            this.screenLobby.Controls.Add(this.lblPlayer);
            this.screenLobby.Controls.Add(this.btnColor7);
            this.screenLobby.Controls.Add(this.btnColor6);
            this.screenLobby.Controls.Add(this.btnColor5);
            this.screenLobby.Controls.Add(this.btnColor4);
            this.screenLobby.Controls.Add(this.btnColor3);
            this.screenLobby.Controls.Add(this.btnColor2);
            this.screenLobby.Controls.Add(this.btnColor1);
            this.screenLobby.Controls.Add(this.btnColor0);
            this.screenLobby.Controls.Add(this.btnStart);
            this.screenLobby.Controls.Add(this.btnLeave);
            this.screenLobby.Location = new System.Drawing.Point(4, 22);
            this.screenLobby.Name = "screenLobby";
            this.screenLobby.Size = new System.Drawing.Size(942, 549);
            this.screenLobby.TabIndex = 3;
            this.screenLobby.Text = "Lobby";
            // 
            // lblPlayer
            // 
            this.lblPlayer.AutoSize = true;
            this.lblPlayer.Location = new System.Drawing.Point(239, 149);
            this.lblPlayer.Name = "lblPlayer";
            this.lblPlayer.Size = new System.Drawing.Size(67, 13);
            this.lblPlayer.TabIndex = 61;
            this.lblPlayer.Text = "Player Name";
            // 
            // btnColor7
            // 
            this.btnColor7.Location = new System.Drawing.Point(593, 88);
            this.btnColor7.Name = "btnColor7";
            this.btnColor7.Size = new System.Drawing.Size(34, 33);
            this.btnColor7.TabIndex = 60;
            this.btnColor7.UseVisualStyleBackColor = true;
            this.btnColor7.Click += new System.EventHandler(this.btnColor7_Click);
            // 
            // btnColor6
            // 
            this.btnColor6.Location = new System.Drawing.Point(553, 88);
            this.btnColor6.Name = "btnColor6";
            this.btnColor6.Size = new System.Drawing.Size(34, 33);
            this.btnColor6.TabIndex = 59;
            this.btnColor6.UseVisualStyleBackColor = true;
            this.btnColor6.Click += new System.EventHandler(this.btnColor6_Click);
            // 
            // btnColor5
            // 
            this.btnColor5.Location = new System.Drawing.Point(513, 88);
            this.btnColor5.Name = "btnColor5";
            this.btnColor5.Size = new System.Drawing.Size(34, 33);
            this.btnColor5.TabIndex = 58;
            this.btnColor5.UseVisualStyleBackColor = true;
            this.btnColor5.Click += new System.EventHandler(this.btnColor5_Click);
            // 
            // btnColor4
            // 
            this.btnColor4.Location = new System.Drawing.Point(473, 88);
            this.btnColor4.Name = "btnColor4";
            this.btnColor4.Size = new System.Drawing.Size(34, 33);
            this.btnColor4.TabIndex = 57;
            this.btnColor4.UseVisualStyleBackColor = true;
            this.btnColor4.Click += new System.EventHandler(this.btnColor4_Click);
            // 
            // btnColor3
            // 
            this.btnColor3.Location = new System.Drawing.Point(433, 88);
            this.btnColor3.Name = "btnColor3";
            this.btnColor3.Size = new System.Drawing.Size(34, 33);
            this.btnColor3.TabIndex = 56;
            this.btnColor3.UseVisualStyleBackColor = true;
            this.btnColor3.Click += new System.EventHandler(this.btnColor3_Click);
            // 
            // btnColor2
            // 
            this.btnColor2.Location = new System.Drawing.Point(393, 88);
            this.btnColor2.Name = "btnColor2";
            this.btnColor2.Size = new System.Drawing.Size(34, 33);
            this.btnColor2.TabIndex = 55;
            this.btnColor2.UseVisualStyleBackColor = true;
            this.btnColor2.Click += new System.EventHandler(this.btnColor2_Click);
            // 
            // btnColor1
            // 
            this.btnColor1.Location = new System.Drawing.Point(353, 88);
            this.btnColor1.Name = "btnColor1";
            this.btnColor1.Size = new System.Drawing.Size(34, 33);
            this.btnColor1.TabIndex = 54;
            this.btnColor1.UseVisualStyleBackColor = true;
            this.btnColor1.Click += new System.EventHandler(this.btnColor1_Click);
            // 
            // btnColor0
            // 
            this.btnColor0.Location = new System.Drawing.Point(313, 88);
            this.btnColor0.Name = "btnColor0";
            this.btnColor0.Size = new System.Drawing.Size(34, 33);
            this.btnColor0.TabIndex = 53;
            this.btnColor0.UseVisualStyleBackColor = true;
            this.btnColor0.Click += new System.EventHandler(this.btnColor0_Click);
            // 
            // btnStart
            // 
            this.btnStart.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnStart.Location = new System.Drawing.Point(688, 437);
            this.btnStart.Name = "btnStart";
            this.btnStart.Size = new System.Drawing.Size(75, 23);
            this.btnStart.TabIndex = 52;
            this.btnStart.Text = "Start";
            this.btnStart.UseVisualStyleBackColor = true;
            this.btnStart.Click += new System.EventHandler(this.btnStart_Click);
            // 
            // btnLeave
            // 
            this.btnLeave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnLeave.Location = new System.Drawing.Point(180, 437);
            this.btnLeave.Name = "btnLeave";
            this.btnLeave.Size = new System.Drawing.Size(75, 23);
            this.btnLeave.TabIndex = 51;
            this.btnLeave.Text = "Leave";
            this.btnLeave.UseVisualStyleBackColor = true;
            this.btnLeave.Click += new System.EventHandler(this.btnLeave_Click);
            // 
            // screenStarting
            // 
            this.screenStarting.BackColor = System.Drawing.SystemColors.Control;
            this.screenStarting.Controls.Add(this.label7);
            this.screenStarting.Location = new System.Drawing.Point(4, 22);
            this.screenStarting.Name = "screenStarting";
            this.screenStarting.Size = new System.Drawing.Size(942, 549);
            this.screenStarting.TabIndex = 6;
            this.screenStarting.Text = "Starting";
            // 
            // label7
            // 
            this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.label7.Location = new System.Drawing.Point(317, 235);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(309, 78);
            this.label7.TabIndex = 3;
            this.label7.Text = "Starting...";
            this.label7.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // screenGame
            // 
            this.screenGame.BackColor = System.Drawing.SystemColors.Control;
            this.screenGame.Controls.Add(this.picViewport);
            this.screenGame.Controls.Add(this.groupBox2);
            this.screenGame.Controls.Add(this.groupBox1);
            this.screenGame.Location = new System.Drawing.Point(4, 22);
            this.screenGame.Name = "screenGame";
            this.screenGame.Padding = new System.Windows.Forms.Padding(3);
            this.screenGame.Size = new System.Drawing.Size(942, 549);
            this.screenGame.TabIndex = 0;
            this.screenGame.Text = "Game";
            // 
            // picViewport
            // 
            this.picViewport.BackColor = System.Drawing.SystemColors.ControlDarkDark;
            this.picViewport.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.picViewport.Location = new System.Drawing.Point(260, 32);
            this.picViewport.Name = "picViewport";
            this.picViewport.Size = new System.Drawing.Size(640, 480);
            this.picViewport.TabIndex = 9;
            this.picViewport.Paint += new System.Windows.Forms.PaintEventHandler(this.picViewport_Paint);
            this.picViewport.MouseDown += new System.Windows.Forms.MouseEventHandler(this.picViewport_MouseDown);
            this.picViewport.MouseMove += new System.Windows.Forms.MouseEventHandler(this.picViewport_MouseMove);
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.chkOrdered);
            this.groupBox2.Controls.Add(this.chkReliable);
            this.groupBox2.Controls.Add(this.cboChannels);
            this.groupBox2.Controls.Add(this.label2);
            this.groupBox2.Location = new System.Drawing.Point(26, 369);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(200, 143);
            this.groupBox2.TabIndex = 8;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Reliable options";
            // 
            // chkOrdered
            // 
            this.chkOrdered.AutoSize = true;
            this.chkOrdered.Checked = true;
            this.chkOrdered.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkOrdered.Location = new System.Drawing.Point(11, 106);
            this.chkOrdered.Name = "chkOrdered";
            this.chkOrdered.Size = new System.Drawing.Size(64, 17);
            this.chkOrdered.TabIndex = 4;
            this.chkOrdered.Text = "Ordered";
            this.chkOrdered.UseVisualStyleBackColor = true;
            // 
            // chkReliable
            // 
            this.chkReliable.AutoSize = true;
            this.chkReliable.Location = new System.Drawing.Point(11, 83);
            this.chkReliable.Name = "chkReliable";
            this.chkReliable.Size = new System.Drawing.Size(64, 17);
            this.chkReliable.TabIndex = 3;
            this.chkReliable.Text = "Reliable";
            this.chkReliable.UseVisualStyleBackColor = true;
            // 
            // cboChannels
            // 
            this.cboChannels.FormattingEnabled = true;
            this.cboChannels.Items.AddRange(new object[] {
            "Channel 0",
            "Channel 1",
            "Channel 2",
            "Channel 3"});
            this.cboChannels.Location = new System.Drawing.Point(11, 55);
            this.cboChannels.Name = "cboChannels";
            this.cboChannels.Size = new System.Drawing.Size(183, 21);
            this.cboChannels.TabIndex = 2;
            this.cboChannels.Text = "Channel 0";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.ForeColor = System.Drawing.SystemColors.GrayText;
            this.label2.Location = new System.Drawing.Point(8, 27);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(97, 13);
            this.label2.TabIndex = 1;
            this.label2.Text = "Only affect position";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.panelPlayers);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Location = new System.Drawing.Point(26, 32);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(200, 302);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Player mask";
            // 
            // panelPlayers
            // 
            this.panelPlayers.AutoScroll = true;
            this.panelPlayers.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPlayers.Location = new System.Drawing.Point(6, 50);
            this.panelPlayers.Name = "panelPlayers";
            this.panelPlayers.Size = new System.Drawing.Size(188, 246);
            this.panelPlayers.TabIndex = 2;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.ForeColor = System.Drawing.SystemColors.GrayText;
            this.label1.Location = new System.Drawing.Point(8, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(121, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Only affect shockwaves";
            // 
            // timer
            // 
            this.timer.Enabled = true;
            this.timer.Interval = 17;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // imageList
            // 
            this.imageList.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList.ImageStream")));
            this.imageList.TransparentColor = System.Drawing.Color.Transparent;
            this.imageList.Images.SetKeyName(0, "arrow0.png");
            this.imageList.Images.SetKeyName(1, "arrow1.png");
            this.imageList.Images.SetKeyName(2, "arrow2.png");
            this.imageList.Images.SetKeyName(3, "arrow3.png");
            this.imageList.Images.SetKeyName(4, "arrow4.png");
            this.imageList.Images.SetKeyName(5, "arrow5.png");
            this.imageList.Images.SetKeyName(6, "arrow6.png");
            this.imageList.Images.SetKeyName(7, "arrow7.png");
            // 
            // FormGame
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(950, 599);
            this.Controls.Add(this.screens);
            this.Controls.Add(this.menuStrip);
            this.MainMenuStrip = this.menuStrip;
            this.Name = "FormGame";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "RelayTestApp";
            this.Load += new System.EventHandler(this.FormGame_Load);
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.screens.ResumeLayout(false);
            this.screenLogin.ResumeLayout(false);
            this.screenLogin.PerformLayout();
            this.screenLoggingIn.ResumeLayout(false);
            this.screenMainMenu.ResumeLayout(false);
            this.screenMainMenu.PerformLayout();
            this.screenJoiningLobby.ResumeLayout(false);
            this.screenLobby.ResumeLayout(false);
            this.screenLobby.PerformLayout();
            this.screenStarting.ResumeLayout(false);
            this.screenGame.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.ToolStripMenuItem appToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem logOutToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem gameToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem leaveToolStripMenuItem;
        private System.Windows.Forms.TabPage screenGame;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.CheckBox chkRememberMe;
        private System.Windows.Forms.CheckBox chkOrdered;
        private System.Windows.Forms.CheckBox chkReliable;
        private System.Windows.Forms.ComboBox cboChannels;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TabPage screenLogin;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TabPage screenMainMenu;
        private System.Windows.Forms.Button btnPlay;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox cboProtocol;
        private System.Windows.Forms.TabPage screenLobby;
        private System.Windows.Forms.Label lblPlayer;
        private System.Windows.Forms.Button btnColor7;
        private System.Windows.Forms.Button btnColor6;
        private System.Windows.Forms.Button btnColor5;
        private System.Windows.Forms.Button btnColor4;
        private System.Windows.Forms.Button btnColor3;
        private System.Windows.Forms.Button btnColor2;
        private System.Windows.Forms.Button btnColor1;
        private System.Windows.Forms.Button btnColor0;
        private System.Windows.Forms.Button btnStart;
        private System.Windows.Forms.Button btnLeave;
        public System.Windows.Forms.TabControl screens;
        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.TabPage screenLoggingIn;
        public System.Windows.Forms.Label lblLoadingText;
        private System.Windows.Forms.TabPage screenJoiningLobby;
        public System.Windows.Forms.Label label47;
        private System.Windows.Forms.TabPage screenStarting;
        public System.Windows.Forms.Label label7;
        private System.Windows.Forms.Panel panelPlayers;
        private System.Windows.Forms.ImageList imageList;
        private System.Windows.Forms.Panel picViewport;
    }
}