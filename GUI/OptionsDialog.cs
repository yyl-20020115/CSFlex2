/* * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * *
 * C# Flex 1.4                                                             *
 * Copyright (C) 2004-2005  Jonathan Gilbert <logic@deltaq.org>            *
 * Derived from:                                                           *
 *                                                                         *
 *   JFlex 1.4                                                             *
 *   Copyright (C) 1998-2004  Gerwin Klein <lsf@jflex.de>                  *
 *   All rights reserved.                                                  *
 *                                                                         *
 * This program is free software; you can redistribute it and/or modify    *
 * it under the terms of the GNU General Public License. See the file      *
 * COPYRIGHT for more information.                                         *
 *                                                                         *
 * This program is distributed in the hope that it will be useful,         *
 * but WITHOUT ANY WARRANTY; without even the implied warranty of          *
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the           *
 * GNU General Public License for more details.                            *
 *                                                                         *
 * You should have received a copy of the GNU General Public License along *
 * with this program; if not, write to the Free Software Foundation, Inc., *
 * 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA                 *
 *                                                                         *
 * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * * */

using CSFlex;

using System;
using System.Drawing;
using System.Windows.Forms;

namespace CSFlex.Gui
{
	/**
	 * A dialog for setting C# Flex options
	 * 
	 * @author Gerwin Klein
	 * @version $Revision: 1.6 $, $Date: 2004/04/12 10:07:48 $
	 * @author Jonathan Gilbert
	 * @version CSFlex 1.4
	 */
	public class OptionsDialog : Form
	{

		private Form owner;

		private Button skelBrowse;
		private TextBox skelFile;

		private Button ok;
		private Button defaults;

		private CheckBox dump;
		private CheckBox verbose;
		private CheckBox jlex;
		private CheckBox no_minimize;
		private CheckBox no_backup;
		private CheckBox time;
		private CheckBox dot;
		private CheckBox csharp;

		private RadioButton tableG;
		private RadioButton switchG;
		private RadioButton packG;


		/**
		 * Create a new options dialog
		 * 
		 * @param owner
		 */
		public OptionsDialog(Form owner)
		{
			this.Text = "Options";

			this.owner = owner;

			Setup();
		}

		public void Setup()
		{
			// create components
			ok = new Button();
			ok.Text = "Ok";

			defaults = new Button();
			defaults.Text = "Defaults";

			skelBrowse = new Button();
			skelBrowse.Text = " Browse";

			skelFile = new TextBox();
			skelFile.ReadOnly = true;

			dump = new CheckBox();
			dump.Text = " dump";

			verbose = new CheckBox();
			verbose.Text = " verbose";

			jlex = new CheckBox();
			jlex.Text = " JLex compatibility";

			no_minimize = new CheckBox();
			no_minimize.Text = " skip minimization";

			no_backup = new CheckBox();
			no_backup.Text = " no backup file";

			time = new CheckBox();
			time.Text = " time statistics";

			dot = new CheckBox();
			dot.Text = " dot graph files";

			csharp = new CheckBox();
			csharp.Text = " C# output";

			tableG = new RadioButton();
			tableG.Text = " table";

			switchG = new RadioButton();
			switchG.Text = " switch";

			packG = new RadioButton();
			packG.Text = " pack";

			switch (Options.gen_method)
			{
				case Options.TABLE: tableG.Checked = true; break;
				case Options.SWITCH: switchG.Checked = true; break;
				case Options.PACK: packG.Checked = true; break;
			}

			// setup interaction
			ok.Click += new EventHandler(Ok_Click);
			defaults.Click += new EventHandler(Defaults_Click);
			skelBrowse.Click += new EventHandler(SkelBrowse_Click);
			tableG.CheckedChanged += new EventHandler(TableG_CheckedChanged);
			switchG.CheckedChanged += new EventHandler(SwitchG_CheckedChanged);
			packG.CheckedChanged += new EventHandler(PackG_CheckedChanged);
			verbose.CheckedChanged += new EventHandler(Verbose_CheckedChanged);
			dump.CheckedChanged += new EventHandler(Dump_CheckedChanged);
			jlex.CheckedChanged += new EventHandler(Jlex_CheckedChanged);
			no_minimize.CheckedChanged += new EventHandler(No_minimize_CheckedChanged);
			no_backup.CheckedChanged += new EventHandler(No_backup_CheckedChanged);
			dot.CheckedChanged += new EventHandler(Dot_CheckedChanged);
			csharp.CheckedChanged += new EventHandler(Csharp_CheckedChanged);
			time.CheckedChanged += new EventHandler(Time_CheckedChanged);

			// setup layout
			GridPanel panel = new GridPanel(4, 7, 10, 10);
			panel.SetInsets(new Insets(10, 5, 5, 10));

			panel.Add(3, 0, ok);
			panel.Add(3, 1, defaults);

			Label lblSkeletonFile = new Label();
			lblSkeletonFile.AutoSize = true;
			lblSkeletonFile.Text = "skeleton file:";

			Label lblCode = new Label();
			lblCode.AutoSize = true;
			lblCode.Text = "code:";

			panel.Add(0, 0, 2, 1, Handles.BOTTOM, lblSkeletonFile);
			panel.Add(0, 1, 2, 1, skelFile);
			panel.Add(2, 1, 1, 1, Handles.TOP, skelBrowse);

			panel.Add(0, 2, 1, 1, Handles.BOTTOM, lblCode);
			panel.Add(0, 3, 1, 1, tableG);
			panel.Add(0, 4, 1, 1, switchG);
			panel.Add(0, 5, 1, 1, packG);

			panel.Add(1, 3, 1, 1, dump);
			panel.Add(1, 4, 1, 1, verbose);
			panel.Add(1, 5, 1, 1, time);

			panel.Add(2, 3, 1, 1, no_minimize);
			panel.Add(2, 4, 1, 1, no_backup);
			panel.Add(2, 5, 1, 1, csharp);

			panel.Add(3, 3, 1, 1, jlex);
			panel.Add(3, 4, 1, 1, dot);

			panel.Size = panel.GetPreferredSize();
			panel.DoLayout();

			Size panel_size = panel.Size;
			Size client_area_size = this.ClientSize;
			Size left_over = new Size(client_area_size.Width - panel_size.Width, client_area_size.Height - panel_size.Height);

			Controls.Add(panel);

			panel.Location = new Point(0, 8);
			this.ClientSize = new Size(panel.Width + 8, panel.Height + 8);
			this.MaximumSize = this.MinimumSize = this.ClientSize;

			UpdateState();
		}

		private void Do_skelBrowse()
		{
			OpenFileDialog d = new OpenFileDialog();

			d.Title = "Choose file";

			DialogResult result = d.ShowDialog();

			if (result != DialogResult.Cancel)
			{
				string skel = d.FileName;
				try
				{
					Skeleton.ReadSkelFile(skel);
					skelFile.Text = skel;
				}
				catch (GeneratorException)
				{
					// do nothing
				}
			}

			d.Dispose();
		}

		private void SetGenMethod()
		{
			if (tableG.Checked)
			{
				Options.gen_method = Options.TABLE;
				return;
			}

			if (switchG.Checked)
			{
				Options.gen_method = Options.SWITCH;
				return;
			}

			if (packG.Checked)
			{
				Options.gen_method = Options.PACK;
				return;
			}
		}

		private void UpdateState()
		{
			dump.Checked = Options.dump;
			verbose.Checked = Options.verbose;
			jlex.Checked = Options.jlex;
			no_minimize.Checked = Options.no_minimize;
			no_backup.Checked = Options.no_backup;
			time.Checked = Options.time;
			dot.Checked = Options.dot;

			switch (Options.gen_method)
			{
				case Options.TABLE: tableG.Checked = true; break;
				case Options.SWITCH: switchG.Checked = true; break;
				case Options.PACK: packG.Checked = true; break;
			}
		}

		private void SetDefaults()
		{
			Options.setDefaults();
			Skeleton.ReadDefault();
			skelFile.Text = "";
			UpdateState();
		}

		private void Ok_Click(object sender, EventArgs e)
		{
			DialogResult = DialogResult.OK;
		}

		private void Defaults_Click(object sender, EventArgs e)
		{
			SetDefaults();
		}

		private void SkelBrowse_Click(object sender, EventArgs e)
		{
			Do_skelBrowse();
		}

		private void TableG_CheckedChanged(object sender, EventArgs e)
		{
			if (tableG.Checked)
				SetGenMethod();
		}

		private void SwitchG_CheckedChanged(object sender, EventArgs e)
		{
			if (switchG.Checked)
				SetGenMethod();
		}

		private void PackG_CheckedChanged(object sender, EventArgs e)
		{
			if (packG.Checked)
				SetGenMethod();
		}

		private void Verbose_CheckedChanged(object sender, EventArgs e)
		{
			Options.verbose = verbose.Checked;
		}

		private void Dump_CheckedChanged(object sender, EventArgs e)
		{
			Options.dump = dump.Checked;
		}

		private void Jlex_CheckedChanged(object sender, EventArgs e)
		{
			Options.jlex = jlex.Checked;
		}

		private void No_minimize_CheckedChanged(object sender, EventArgs e)
		{
			Options.no_minimize = no_minimize.Checked;
		}

		private void No_backup_CheckedChanged(object sender, EventArgs e)
		{
			Options.no_backup = no_backup.Checked;
		}

		private void Dot_CheckedChanged(object sender, EventArgs e)
		{
			Options.dot = dot.Checked;
		}

		private void Time_CheckedChanged(object sender, EventArgs e)
		{
			Options.time = time.Checked;
		}

		private void Csharp_CheckedChanged(object sender, EventArgs e)
		{
			Options.emit_csharp = csharp.Checked;
		}
	}
}
