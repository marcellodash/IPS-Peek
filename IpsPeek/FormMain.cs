﻿using Be.Windows.Forms;
using BrightIdeasSoftware;
using IpsLibNet;
using IpsPeek.IpsLibNet.Patching;
using IpsPeek.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace IpsPeek
{
    public partial class FormMain : Form
    {
        private long _fileSize = 0;
        private int _patchCount = 0;
        private HighlightTextRenderer _highlighter = new HighlightTextRenderer();
        private readonly string optionsPath = Path.Combine(Application.StartupPath, "settings.json");
        #region "Helpers"
        private void CloseFile()
        {
            fastObjectListViewRows.ClearObjects();
            hexBoxData.ByteProvider = null;
            this.Text = Application.ProductName;

            this.closeToolStripMenuItem.Enabled = false;
            this.closeToolStripButton.Enabled = false;

            exportToolStripButton.Enabled = false;
            exportToolStripMenuItem.Enabled = false;

            toolStripStatusLabelRows.Text = string.Format("Row: {0} / {1} ({2} bytes)", 0, 0, 0);
            ToolStripStatusLabelPatchCount.Text = string.Format("Patches: {0}", 0);
            toolStripStatusLabelFileSize.Text = string.Empty;
        }

        private void OpenFile()
        {

            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Filter = "IPS Files (*.ips)|*.ips";

                if (dialog.ShowDialog(this) == System.Windows.Forms.DialogResult.OK)
                {
                    LoadFile(dialog.FileName);
                    filterToolStripTextBox.Clear();
                }
            }
        }
        private void OpenPage(string url)
        {
            Process.Start(url);
        }

        private string GetDisplayName(Type element)
        {
            if (element == typeof(IpsEndOfFileValueElement))
            {
                return "EOF";
            }
            else if (element == typeof(IpsIdValueElement))
            {
                return "ID";
            }
            else if (element  == typeof(IpsPatchElement))
            {
                return "PAT";
            }
            else if (element == typeof(IpsResizeValueElement))
            {
                return "CHS";
            }
            
            else if (element == typeof(IpsRlePatchElement))
            {
                return "RLE";
            }
            else
            {
                return string.Empty;
            }
                
        }

        private void ExportFile()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Filter = "Text Files (*.txt)|*.txt";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    using (StreamWriter writer = new StreamWriter(dialog.FileName, false, Encoding.ASCII))
                    {
                        writer.WriteLine("{0} Version {1}.", Application.ProductName, Application.ProductVersion.ToString());
                        writer.WriteLine();
                        writer.WriteLine("{0,-10}{1,-10}{2,-8}{3,-10}{4,-12}{5,-12}{6}", "Offset", "End", "Size", "Type", "IPS Start", "IPS End", "IPS Size");
                        try
                        {
                            int totalSize = 0;
                            foreach (var patch in fastObjectListViewRows.Objects)
                            {
                                string offset = "N/A";
                                string size = "N/A";
                                string end = "N/A";
                                string type = GetDisplayName(patch.GetType());
                                string rangeStart = ((IpsElement)patch).IpsOffset.ToString("X8");
                                string rangeStop = ((IpsElement)patch).IpsEnd.ToString("X8");
                                string ipsFileSize = ((IpsElement)patch).IpsSize.ToString("X");
                                if (patch is IpsPatchElement)
                                {
                                    offset = ((IpsPatchElement)patch).Offset.ToString("X6");
                                    end = ((IpsPatchElement)patch).End.ToString("X6");
                                    size = ((IpsPatchElement)patch).Size.ToString("X");
                                    totalSize += ((IpsPatchElement)patch).Size;
                                }
                                else if (patch is IpsResizeValueElement)
                                {
                                    offset = ((IpsResizeValueElement)patch).GetIntValue().ToString("X6");
                                    totalSize += ((IpsResizeValueElement)patch).GetIntValue();
                                }
                                writer.WriteLine("{0,-10}{1,-10}{2,-8}{3,-10}{4, -12}{5, -12}{6}", offset, end, size, type, rangeStart, rangeStop, ipsFileSize);
                            }
                            writer.WriteLine();
                            writer.WriteLine("Rows: {0:X} ({0}), Patches: {1:X} ({1}), Modified: {2:X} ({2}) bytes.", fastObjectListViewRows.GetItemCount(), _patchCount, totalSize);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message);
                        }
                    }
                }
            }
        }

        private void LoadFile(string file)
        {
            try
            {
                var scanner = new IpsScanner();
                List<IpsElement> patches = scanner.Scan(file);
                _patchCount = patches.Where((element) => (element is IpsPatchElement)).Count();
                fastObjectListViewRows.SetObjects(patches);
                fastObjectListViewRows.SelectedIndex = 0;
                this.Text = string.Format("{0} - {1}", Application.ProductName, file);

                this.closeToolStripMenuItem.Enabled = true;
                this.closeToolStripButton.Enabled = true;

                exportToolStripButton.Enabled = true;
                exportToolStripMenuItem.Enabled = true;

                _fileSize = new FileInfo(file).Length;

                toolStripStatusLabelFileSize.Text = string.Format("File size: {0} bytes", _fileSize);
                ToolStripStatusLabelPatchCount.Text = string.Format("Patches: {0}", _patchCount);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("Failed to load file: \'{0}.\'" + ex.Message, file));
            }
        }

        private void LoadSettings()
        {
            OptionsManager.Load(optionsPath, new OptionsModel(this.Width, this.Height, this.Top, this.Left, splitContainer1.SplitterDistance, true, true, true));
            this.Size = new Size(OptionsManager.FormWidth, OptionsManager.FormHeight);
            toolbarToolStripMenuItem.Checked = OptionsManager.ToolBarVisible;
            dataViewToolStripMenuItem.Checked = OptionsManager.DataViewVisible;
            stringViewToolStripMenuItem.Checked = OptionsManager.StringViewVisible;
            this.Top = OptionsManager.FormTop;
            this.Left = OptionsManager.FormLeft;
            splitContainer1.SplitterDistance = OptionsManager.PanelHeight;
        }

        private void SaveSettings()
        {
            OptionsManager.DataViewVisible = dataViewToolStripMenuItem.Checked;
            OptionsManager.StringViewVisible = stringViewToolStripMenuItem.Checked;
            OptionsManager.ToolBarVisible = toolbarToolStripMenuItem.Checked;
            OptionsManager.PanelHeight = splitContainer1.SplitterDistance;
            OptionsManager.FormTop = this.Top;
            OptionsManager.FormLeft = this.Left;
            OptionsManager.FormWidth = this.Width;
            OptionsManager.FormHeight = this.Height;
            OptionsManager.Save();
        }
        #endregion

        public FormMain()
        {
            InitializeComponent();
            this.olvColumnEnd.AspectGetter = delegate(object row) {
                try
                {
                    return string.Format("{0:X6}", ((IpsPatchElement)row).End); 
                }
                catch
                {
                    return string.Empty;
                }
            };
            this.olvColumnIpsOffset.AspectGetter = delegate(object row) { return string.Format("{0:X8}", ((IpsElement)row).IpsOffset); };
            this.olvColumnIpsEnd.AspectGetter = delegate(object row) { return string.Format("{0:X8}", ((IpsElement)row).IpsEnd); };
            this.olvColumnIpsSize.AspectGetter = delegate(object row) { return string.Format("{0:X}", ((IpsElement)row).IpsSize); };
            this.olvColumnIpsSize.FillsFreeSpace = true;
            this.olvColumnOffset.AspectGetter = delegate(object row)
            {
                try
                {
                    if (row is IpsResizeValueElement)
                    {
                        return string.Format("{0:X6}", ((IpsResizeValueElement)row).GetIntValue());
                    }
                    else
                    {
                        return string.Format("{0:X6}", ((IpsPatchElement)row).Offset);
                    }
                }
                catch
                {
                    return string.Empty;
                }
            };

            this.olvColumnSize.AspectGetter = delegate(object row)
            {
                try
                {
                    return string.Format("{0:X}", ((IpsPatchElement)row).Size);
                }
                catch
                {
                    return string.Empty;
                }
            };

            this.olvColumnType.AspectGetter = delegate(object row)
            {
                string name = string.Empty;
                try
                {
                    name = GetDisplayName(row.GetType());
                }
                catch
                {
                    
                }
                return name;
            };
            // this.objectListView1.AlternateRowBackColor = Color.FromArgb(0xe2e2e2);
            this.fastObjectListViewRows.UseFiltering = true;
            this.closeToolStripMenuItem.Enabled = false;
            this.closeToolStripButton.Enabled = false;
            hexBoxData.LineInfoVisible = true;
            hexBoxData.ColumnInfoVisible = true;
            hexBoxData.VScrollBarVisible = true;
            hexBoxData.StringViewVisible = true;
            hexBoxData.UseFixedBytesPerLine = true;



            toolStripStatusLabelRows.Text = string.Format("Row: {0} / {1} ({2} bytes)", 0, 0, 0);
            ToolStripStatusLabelPatchCount.Text = string.Format("Patches: {0}", _patchCount);

            toolbarToolStripMenuItem.Checked = true;


            dataViewToolStripMenuItem.Checked = true;


            stringViewToolStripMenuItem.Checked = true;

            this.StartPosition = FormStartPosition.Manual;

            exportToolStripButton.Enabled = false;
            exportToolStripMenuItem.Enabled = false;

            fastObjectListViewRows.DefaultRenderer = _highlighter;


            // Try to load a file from the command line (such as a file that was dropped onto the icon).
            try
            {
                string file = Environment.GetCommandLineArgs()[1];
                LoadFile(file);

            }
            catch
            {
            }

            LoadSettings();
        }

        private void openPatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFile();
        }


        private void objectListView1_SelectionChanged(object sender, EventArgs e)
        {
            if (fastObjectListViewRows.SelectedObjects.Count == 1)
            {
                int size = 0;
                try
                {
                    hexBoxData.LineInfoOffset = (long)((IpsPatchElement)fastObjectListViewRows.SelectedObject).Offset;
                    hexBoxData.ByteProvider = new DynamicByteProvider(((IpsPatchElement)fastObjectListViewRows.SelectedObject).GetData());


                    size = ((IpsPatchElement)fastObjectListViewRows.SelectedObject).Size;
                }
                catch
                {
                    hexBoxData.ByteProvider = null;
                }
                finally
                {
                    try
                    {
                        toolStripStatusLabelRows.Text = string.Format("Row: {0} / {1} ({2} bytes)", fastObjectListViewRows.SelectedIndex + 1, fastObjectListViewRows.Items.Count, size);
                    }
                    catch
                    {
                        toolStripStatusLabelRows.Text = string.Empty;
                    }
                }
            }
            else
            {
                toolStripStatusLabelRows.Text = "";
            }

        }

        private void closeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CloseFile();
        }


        private void openPatchToolStripButton_Click(object sender, EventArgs e)
        {
            OpenFile();
        }



        private void closeToolStripButton_Click(object sender, EventArgs e)
        {
            CloseFile();
        }

        private void exportToolStripButton_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            ExportFile();
            this.Enabled = true;
        }


        private void toolbarToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            toolStrip1.Visible = toolbarToolStripMenuItem.Checked;
        }

        private void dataViewToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            splitContainer1.Panel2Collapsed = !dataViewToolStripMenuItem.Checked;
            stringViewToolStripMenuItem.Enabled = dataViewToolStripMenuItem.Checked;
        }

        private void FormMain_DragDrop(object sender, DragEventArgs e)
        {

            try
            {
                Array data = (Array)e.Data.GetData(DataFormats.FileDrop);
                if ((data != null))
                {
                    var file = data.GetValue(0).ToString();

                    this.BeginInvoke((Action<string>)((string value) => { LoadFile(value); }), new object[] { file });

                    this.Activate();
                }

            }
            catch (Exception)
            {
            }
        }

        private void FormMain_DragEnter(object sender, DragEventArgs e)
        {

            if ((e.Data.GetDataPresent(DataFormats.FileDrop)))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void FormMain_Load(object sender, EventArgs e)
        {

        }
 

        private void filterToolStripTextBox_TextChanged(object sender, EventArgs e)
        {
            if (filterToolStripTextBox.TextLength == 0)
            {
                var filter = TextMatchFilter.Contains(this.fastObjectListViewRows, string.Empty);
                _highlighter.Filter = filter;
                fastObjectListViewRows.ModelFilter = filter;
                fastObjectListViewRows.Refresh();
            }
        }

        private void stringViewToolStripMenuItem_CheckStateChanged(object sender, EventArgs e)
        {
            hexBoxData.StringViewVisible = stringViewToolStripMenuItem.Checked;
        }

        private void filterToolStripTextBox_Enter(object sender, EventArgs e)
        {
            // Kick off SelectAll asyncronously so that it occurs after Click
            BeginInvoke((Action)delegate
            {
                filterToolStripTextBox.SelectAll();
            });
        }

        private void officialForumToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPage("http://www.codeisle.com/forum/product/ips-peek/");
        }

        private void iPSPeekHomeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPage("http://www.codeisle.com/");
        }

        private void helpContentsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenPage("http://help.codeisle.com/ips-peek/");
        }

        private void exportToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Enabled = false;
            ExportFile();
            this.Enabled = true;
        }

        private void aboutIPSPeekToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (FormAbout about = new FormAbout())
            {
                about.StartPosition = FormStartPosition.CenterParent;
                about.ShowDialog(this);
            }
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.olvColumnIpsOffset.AspectGetter = null;
            this.olvColumnIpsEnd.AspectGetter = null;
            this.olvColumnIpsSize.AspectGetter = null;
            this.olvColumnOffset.AspectGetter = null;

            SaveSettings();
        }

        private void filterToolStripTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // var filter  = new TextMatchFilter.Contains(this.objectListView1, filterToolStripTextBox.Text);
                var filter = TextMatchFilter.Contains(this.fastObjectListViewRows, filterToolStripTextBox.Text);
                _highlighter.Filter = filter;
                fastObjectListViewRows.ModelFilter = filter;
                fastObjectListViewRows.Refresh();
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
