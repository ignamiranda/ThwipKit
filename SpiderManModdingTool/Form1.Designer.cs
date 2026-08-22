namespace SpiderManModdingTool;

partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Menu strip for the main form.
        /// </summary>
        private System.Windows.Forms.MenuStrip menuStrip1;

        /// <summary>
        ///  Status strip for the main form.
        /// </summary>
        private System.Windows.Forms.StatusStrip statusStrip1;

        /// <summary>
        ///  File menu item.
        /// </summary>
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemFile;
        /// <summary>
        ///  Edit menu item.
        /// </summary>
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemEdit;
        /// <summary>
        ///  View menu item.
        /// </summary>
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemView;
        /// <summary>
        ///  Help menu item.
        /// </summary>
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemHelp;
        /// <summary>
        ///  Exit menu item.
        /// </summary>
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemExit;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemCleanTemp;
        /// <summary>
        ///  About menu item.
        /// </summary>
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemAbout;
        private System.Windows.Forms.ToolStripMenuItem toolStripMenuItemGameVersion;

        /// <summary>
        ///  Label for game path.
        /// </summary>
        private System.Windows.Forms.Label labelGamePath;
        /// <summary>
        ///  TextBox for game path input/display.
        /// </summary>
        private System.Windows.Forms.TextBox textBoxGamePath;
        /// <summary>
        ///  Button to browse for game folder.
        /// </summary>
        private System.Windows.Forms.Button buttonBrowse;
        /// <summary>
        ///  Button to detect game installation.
        /// </summary>
        private System.Windows.Forms.Button buttonDetect;
        /// <summary>
        ///  Label for texture list.
        /// </summary>
        private System.Windows.Forms.Label labelTextures;
        /// <summary>
        ///  ListBox to display texture names.
        /// </summary>
        private System.Windows.Forms.ListBox listBoxTextures;
        /// <summary>
        ///  TextBox for filtering textures by name.
        /// </summary>
        private System.Windows.Forms.TextBox textBoxSearch;
        /// <summary>
        ///  ProgressBar for scanning progress.
        /// </summary>
        private System.Windows.Forms.ProgressBar progressBarScan;
        /// <summary>
        ///  Button to refresh texture list.
        /// </summary>
        private System.Windows.Forms.Button buttonRefresh;
        /// <summary>
        ///  Button to extract selected texture to PNG.
        /// </summary>
        private System.Windows.Forms.Button buttonExtract;
        /// <summary>
        ///  Button to rebuild PNG to texture.
        /// </summary>
        private System.Windows.Forms.Button buttonRebuild;
private System.Windows.Forms.Button buttonEdit;

        // Backup system controls
        private System.Windows.Forms.CheckBox checkBoxEnableBackups;
        private System.Windows.Forms.Label labelMaxBackups;
        private System.Windows.Forms.NumericUpDown numericUpDownMaxBackups;
        private System.Windows.Forms.Label labelBackupDirectory;
        private System.Windows.Forms.TextBox textBoxBackupDirectory;
        private System.Windows.Forms.Button buttonBrowseBackupDir;
        private System.Windows.Forms.Button buttonCreateBackup;
        private System.Windows.Forms.Button buttonRestoreBackup;
        private System.Windows.Forms.ListBox listBoxBackups;
        private System.Windows.Forms.Label labelBackups;

    /// <summary>
    ///  Clean up any resources being used.
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
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Text = "Spider-Man Modding Tool";
            // 
            // toolStripMenuItemExit
            // 
            this.toolStripMenuItemExit = new System.Windows.Forms.ToolStripMenuItem();
            // 
            // toolStripMenuItemAbout
            // 
            this.toolStripMenuItemAbout = new System.Windows.Forms.ToolStripMenuItem();
            // 
            // toolStripMenuItemFile
            // 
            this.toolStripMenuItemFile = new System.Windows.Forms.ToolStripMenuItem();
            // 
            // toolStripMenuItemEdit
            // 
            this.toolStripMenuItemEdit = new System.Windows.Forms.ToolStripMenuItem();
            // 
            // toolStripMenuItemView
            // 
            this.toolStripMenuItemView = new System.Windows.Forms.ToolStripMenuItem();
            // 
            // toolStripMenuItemHelp
            // 
            this.toolStripMenuItemHelp = new System.Windows.Forms.ToolStripMenuItem();
            // 
            // menuStrip1
            // 
            this.menuStrip1 = new System.Windows.Forms.MenuStrip();
            // 
            // statusStrip1
            // 
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            // 
            // toolStripMenuItemExit
            // 
            this.toolStripMenuItemExit.Name = "toolStripMenuItemExit";
            this.toolStripMenuItemExit.Size = new System.Drawing.Size(116, 26);
this.toolStripMenuItemExit.Text = "E&xit";
            this.toolStripMenuItemExit.Click += new System.EventHandler(this.toolStripMenuItemExit_Click);
            //
            // toolStripMenuItemCleanTemp
            //
            this.toolStripMenuItemCleanTemp = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemCleanTemp.Name = "toolStripMenuItemCleanTemp";
            this.toolStripMenuItemCleanTemp.Size = new System.Drawing.Size(180, 26);
            this.toolStripMenuItemCleanTemp.Text = "Clean Temporary Files";
            this.toolStripMenuItemCleanTemp.Click += new System.EventHandler(this.toolStripMenuItemCleanTemp_Click);
            //
            // toolStripMenuItemFile
            //
            this.toolStripMenuItemFile.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemCleanTemp,
            this.toolStripMenuItemExit});
            this.toolStripMenuItemFile.Name = "toolStripMenuItemFile";
            this.toolStripMenuItemFile.Size = new System.Drawing.Size(46, 24);
            this.toolStripMenuItemFile.Text = "&File";
            // 
            // toolStripMenuItemEdit
            // 
            this.toolStripMenuItemEdit.Name = "toolStripMenuItemEdit";
            this.toolStripMenuItemEdit.Size = new System.Drawing.Size(49, 24);
            this.toolStripMenuItemEdit.Text = "&Edit";
            // 
            // toolStripMenuItemView
            // 
            this.toolStripMenuItemView.Name = "toolStripMenuItemView";
            this.toolStripMenuItemView.Size = new System.Drawing.Size(55, 24);
            this.toolStripMenuItemView.Text = "&View";
            // 
// toolStripMenuItemAbout
            //
            this.toolStripMenuItemAbout.Name = "toolStripMenuItemAbout";
            this.toolStripMenuItemAbout.Size = new System.Drawing.Size(133, 26);
            this.toolStripMenuItemAbout.Text = "&About...";
            this.toolStripMenuItemAbout.Click += new System.EventHandler(this.toolStripMenuItemAbout_Click);
            //
            // toolStripMenuItemGameVersion
            //
            this.toolStripMenuItemGameVersion = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItemGameVersion.Name = "toolStripMenuItemGameVersion";
            this.toolStripMenuItemGameVersion.Size = new System.Drawing.Size(133, 26);
            this.toolStripMenuItemGameVersion.Text = "Game &Version Info";
            this.toolStripMenuItemGameVersion.Click += new System.EventHandler(this.toolStripMenuItemGameVersion_Click);
            //
            // toolStripMenuItemHelp
            //
            this.toolStripMenuItemHelp.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemAbout,
            this.toolStripMenuItemGameVersion});
this.toolStripMenuItemHelp.Name = "toolStripMenuItemHelp";
            this.toolStripMenuItemHelp.Size = new System.Drawing.Size(55, 24);
            this.toolStripMenuItemHelp.Text = "&Help";
//
// labelGamePath
// 
            this.labelGamePath = new System.Windows.Forms.Label();
            this.labelGamePath.AutoSize = true;
            this.labelGamePath.Location = new System.Drawing.Point(12, 35);
            this.labelGamePath.Name = "labelGamePath";
            this.labelGamePath.Size = new System.Drawing.Size(71, 15);
            this.labelGamePath.TabIndex = 0;
            this.labelGamePath.Text = "Game Path:";
            // 
            // textBoxGamePath
            // 
            this.textBoxGamePath = new System.Windows.Forms.TextBox();
            this.textBoxGamePath.Location = new System.Drawing.Point(89, 32);
            this.textBoxGamePath.Name = "textBoxGamePath";
            this.textBoxGamePath.Size = new System.Drawing.Size(400, 23);
            this.textBoxGamePath.TabIndex = 1;
//
// buttonBrowse
// 
            this.buttonBrowse = new System.Windows.Forms.Button();
            this.buttonBrowse.Location = new System.Drawing.Point(495, 31);
            this.buttonBrowse.Name = "buttonBrowse";
            this.buttonBrowse.Size = new System.Drawing.Size(75, 23);
            this.buttonBrowse.TabIndex = 2;
            this.buttonBrowse.Text = "Browse...";
            this.buttonBrowse.UseVisualStyleBackColor = true;
            this.buttonBrowse.Click += new System.EventHandler(this.buttonBrowse_Click);
            // 
            // buttonDetect
            // 
            this.buttonDetect = new System.Windows.Forms.Button();
            this.buttonDetect.Location = new System.Drawing.Point(576, 31);
            this.buttonDetect.Name = "buttonDetect";
            this.buttonDetect.Size = new System.Drawing.Size(75, 23);
            this.buttonDetect.TabIndex = 3;
            this.buttonDetect.Text = "Detect";
            this.buttonDetect.UseVisualStyleBackColor = true;
            this.buttonDetect.Click += new System.EventHandler(this.buttonDetect_Click);
            // 
            // labelTextures
            // 
            this.labelTextures = new System.Windows.Forms.Label();
            this.labelTextures.AutoSize = true;
            this.labelTextures.Location = new System.Drawing.Point(12, 70);
            this.labelTextures.Name = "labelTextures";
            this.labelTextures.Size = new System.Drawing.Size(50, 15);
            this.labelTextures.TabIndex = 4;
            this.labelTextures.Text = "Textures:";
            // 
            // textBoxSearch
            // 
            this.textBoxSearch = new System.Windows.Forms.TextBox();
            this.textBoxSearch.Location = new System.Drawing.Point(89, 67);
            this.textBoxSearch.Name = "textBoxSearch";
            this.textBoxSearch.Size = new System.Drawing.Size(176, 23);
            this.textBoxSearch.TabIndex = 21;
            this.textBoxSearch.PlaceholderText = "Type to search...";
            this.textBoxSearch.TextChanged += new System.EventHandler(this.textBoxSearch_TextChanged);
            // 
            // listBoxTextures
            // 
            this.listBoxTextures = new System.Windows.Forms.ListBox();
            this.listBoxTextures.FormattingEnabled = true;
            this.listBoxTextures.ItemHeight = 15;
            this.listBoxTextures.Location = new System.Drawing.Point(15, 88);
            this.listBoxTextures.Name = "listBoxTextures";
            this.listBoxTextures.Size = new System.Drawing.Size(250, 244);
            this.listBoxTextures.TabIndex = 5;
            this.listBoxTextures.SelectedIndexChanged += new System.EventHandler(this.listBoxTextures_SelectedIndexChanged);
            // 
            // progressBarScan
            // 
            this.progressBarScan = new System.Windows.Forms.ProgressBar();
            this.progressBarScan.Location = new System.Drawing.Point(280, 88);
            this.progressBarScan.Name = "progressBarScan";
            this.progressBarScan.Size = new System.Drawing.Size(371, 23);
            this.progressBarScan.TabIndex = 6;
            // 
            // buttonRefresh
            // 
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonRefresh.Location = new System.Drawing.Point(576, 309);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(75, 23);
            this.buttonRefresh.TabIndex = 7;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.buttonRefresh_Click);
            // 
            // buttonExtract
            // 
            this.buttonExtract = new System.Windows.Forms.Button();
            this.buttonExtract.Enabled = false;
            this.buttonExtract.Location = new System.Drawing.Point(495, 309);
            this.buttonExtract.Name = "buttonExtract";
            this.buttonExtract.Size = new System.Drawing.Size(75, 23);
            this.buttonExtract.TabIndex = 8;
            this.buttonExtract.Text = "Extract";
            this.buttonExtract.UseVisualStyleBackColor = true;
            this.buttonExtract.Click += new System.EventHandler(this.buttonExtract_Click);
            // 
            // buttonRebuild
            // 
            this.buttonRebuild = new System.Windows.Forms.Button();
            this.buttonRebuild.Enabled = false;
            this.buttonRebuild.Location = new System.Drawing.Point(576, 309);
            this.buttonRebuild.Name = "buttonRebuild";
            this.buttonRebuild.Size = new System.Drawing.Size(75, 23);
            this.buttonRebuild.TabIndex = 9;
            this.buttonRebuild.Text = "Rebuild";
            this.buttonRebuild.UseVisualStyleBackColor = true;
            this.buttonRebuild.Click += new System.EventHandler(this.buttonRebuild_Click);
            // 
            // buttonEdit
            // 
            this.buttonEdit = new System.Windows.Forms.Button();
            this.buttonEdit.Enabled = false;
            this.buttonEdit.Location = new System.Drawing.Point(414, 309);
            this.buttonEdit.Name = "buttonEdit";
            this.buttonEdit.Size = new System.Drawing.Size(75, 23);
            this.buttonEdit.TabIndex = 10;
            this.buttonEdit.Text = "Edit";
            this.buttonEdit.UseVisualStyleBackColor = true;
this.buttonEdit.Click += new System.EventHandler(this.buttonEdit_Click);
            //
            // checkBoxEnableBackups
            //
            this.checkBoxEnableBackups = new System.Windows.Forms.CheckBox();
            this.checkBoxEnableBackups.AutoSize = true;
            this.checkBoxEnableBackups.Location = new System.Drawing.Point(270, 90);
            this.checkBoxEnableBackups.Name = "checkBoxEnableBackups";
            this.checkBoxEnableBackups.Size = new System.Drawing.Size(105, 19);
            this.checkBoxEnableBackups.TabIndex = 11;
            this.checkBoxEnableBackups.Text = "Enable Backups";
            this.checkBoxEnableBackups.UseVisualStyleBackColor = true;
            this.checkBoxEnableBackups.CheckedChanged += new System.EventHandler(this.checkBoxEnableBackups_CheckedChanged);
            //
            // labelMaxBackups
            //
            this.labelMaxBackups = new System.Windows.Forms.Label();
            this.labelMaxBackups.AutoSize = true;
            this.labelMaxBackups.Location = new System.Drawing.Point(270, 120);
            this.labelMaxBackups.Name = "labelMaxBackups";
            this.labelMaxBackups.Size = new System.Drawing.Size(88, 15);
            this.labelMaxBackups.TabIndex = 12;
            this.labelMaxBackups.Text = "Max Backups:";
            //
            // numericUpDownMaxBackups
            //
            this.numericUpDownMaxBackups = new System.Windows.Forms.NumericUpDown();
            this.numericUpDownMaxBackups.Location = new System.Drawing.Point(364, 118);
            this.numericUpDownMaxBackups.Maximum = new decimal(new int[] {
            100,
            0,
            0,
            0});
            this.numericUpDownMaxBackups.Minimum = new decimal(new int[] {
            1,
            0,
            0,
            0});
            this.numericUpDownMaxBackups.Name = "numericUpDownMaxBackups";
            this.numericUpDownMaxBackups.Size = new System.Drawing.Size(60, 23);
            this.numericUpDownMaxBackups.TabIndex = 13;
            this.numericUpDownMaxBackups.Value = new decimal(new int[] {
            10,
            0,
            0,
            0});
            //
            // labelBackupDirectory
            //
            this.labelBackupDirectory = new System.Windows.Forms.Label();
            this.labelBackupDirectory.AutoSize = true;
            this.labelBackupDirectory.Location = new System.Drawing.Point(270, 150);
            this.labelBackupDirectory.Name = "labelBackupDirectory";
            this.labelBackupDirectory.Size = new System.Drawing.Size(92, 15);
            this.labelBackupDirectory.TabIndex = 14;
            this.labelBackupDirectory.Text = "Backup Dir:";
            //
            // textBoxBackupDirectory
            //
            this.textBoxBackupDirectory = new System.Windows.Forms.TextBox();
            this.textBoxBackupDirectory.Location = new System.Drawing.Point(364, 147);
            this.textBoxBackupDirectory.Name = "textBoxBackupDirectory";
            this.textBoxBackupDirectory.Size = new System.Drawing.Size(200, 23);
            this.textBoxBackupDirectory.TabIndex = 15;
            this.textBoxBackupDirectory.Text = "backups";
            //
            // buttonBrowseBackupDir
            //
            this.buttonBrowseBackupDir = new System.Windows.Forms.Button();
            this.buttonBrowseBackupDir.Location = new System.Drawing.Point(570, 146);
            this.buttonBrowseBackupDir.Name = "buttonBrowseBackupDir";
            this.buttonBrowseBackupDir.Size = new System.Drawing.Size(75, 23);
            this.buttonBrowseBackupDir.TabIndex = 16;
            this.buttonBrowseBackupDir.Text = "Browse";
            this.buttonBrowseBackupDir.UseVisualStyleBackColor = true;
            this.buttonBrowseBackupDir.Click += new System.EventHandler(this.buttonBrowseBackupDir_Click);
            //
            // buttonCreateBackup
            //
            this.buttonCreateBackup = new System.Windows.Forms.Button();
            this.buttonCreateBackup.Location = new System.Drawing.Point(270, 180);
            this.buttonCreateBackup.Name = "buttonCreateBackup";
            this.buttonCreateBackup.Size = new System.Drawing.Size(105, 23);
            this.buttonCreateBackup.TabIndex = 17;
            this.buttonCreateBackup.Text = "Create Backup";
            this.buttonCreateBackup.UseVisualStyleBackColor = true;
            this.buttonCreateBackup.Click += new System.EventHandler(this.buttonCreateBackup_Click);
            //
            // buttonRestoreBackup
            //
            this.buttonRestoreBackup = new System.Windows.Forms.Button();
            this.buttonRestoreBackup.Location = new System.Drawing.Point(385, 180);
            this.buttonRestoreBackup.Name = "buttonRestoreBackup";
            this.buttonRestoreBackup.Size = new System.Drawing.Size(105, 23);
            this.buttonRestoreBackup.TabIndex = 18;
            this.buttonRestoreBackup.Text = "Restore Backup";
            this.buttonRestoreBackup.UseVisualStyleBackColor = true;
            this.buttonRestoreBackup.Click += new System.EventHandler(this.buttonRestoreBackup_Click);
            //
            // labelBackups
            //
            this.labelBackups = new System.Windows.Forms.Label();
            this.labelBackups.AutoSize = true;
            this.labelBackups.Location = new System.Drawing.Point(270, 215);
            this.labelBackups.Name = "labelBackups";
            this.labelBackups.Size = new System.Drawing.Size(53, 15);
            this.labelBackups.TabIndex = 19;
            this.labelBackups.Text = "Backups:";
            //
            // listBoxBackups
            //
            this.listBoxBackups = new System.Windows.Forms.ListBox();
            this.listBoxBackups.FormattingEnabled = true;
            this.listBoxBackups.ItemHeight = 15;
            this.listBoxBackups.Location = new System.Drawing.Point(270, 235);
            this.listBoxBackups.Name = "listBoxBackups";
            this.listBoxBackups.Size = new System.Drawing.Size(375, 154);
            this.listBoxBackups.TabIndex = 20;
            //
            // Form1
            // 
            this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripMenuItemFile,
            this.toolStripMenuItemEdit,
            this.toolStripMenuItemView,
            this.toolStripMenuItemHelp});
            this.Controls.Add(this.buttonRefresh);
            this.Controls.Add(this.buttonExtract);
            this.Controls.Add(this.buttonEdit);
            this.Controls.Add(this.buttonRebuild);
            this.Controls.Add(this.progressBarScan);
            this.Controls.Add(this.textBoxSearch);
            this.Controls.Add(this.listBoxTextures);
            this.Controls.Add(this.labelTextures);
            this.Controls.Add(this.buttonDetect);
            this.Controls.Add(this.buttonBrowse);
            this.Controls.Add(this.textBoxGamePath);
            this.Controls.Add(this.labelGamePath);
            this.Controls.Add(this.labelMaxBackups);
            this.Controls.Add(this.numericUpDownMaxBackups);
            this.Controls.Add(this.labelBackupDirectory);
            this.Controls.Add(this.textBoxBackupDirectory);
            this.Controls.Add(this.buttonBrowseBackupDir);
            this.Controls.Add(this.buttonCreateBackup);
            this.Controls.Add(this.buttonRestoreBackup);
            this.Controls.Add(this.labelBackups);
            this.Controls.Add(this.listBoxBackups);
            this.Controls.Add(this.checkBoxEnableBackups);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.menuStrip1);
            this.MainMenuStrip = this.menuStrip1;
        }

    #endregion
}
