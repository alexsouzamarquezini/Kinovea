#region License
/*
Copyright � Joan Charmant 2008.
joan.charmant@gmail.com 
 
This file is part of Kinovea.

Kinovea is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License version 2 
as published by the Free Software Foundation.

Kinovea is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with Kinovea. If not, see http://www.gnu.org/licenses/.
*/
#endregion

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Threading;
using System.Windows.Forms;

using Kinovea.Camera;
using Kinovea.ScreenManager.Languages;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    public partial class ScreenManagerUserInterface : UserControl
    {
        #region Delegates
        public DelegateUpdateTrackerFrame delegateUpdateTrackerFrame;
        #endregion

        public event EventHandler<FileLoadAskedEventArgs> FileLoadAsked;
        
        #region Properties
        public bool CommonControlsVisible 
        {
            get { return !splitScreensPanel.Panel2Collapsed; }
        }
        public bool CommonPlaying
        {
            get { return cctrlsPlayers.Playing; }
            set { cctrlsPlayers.Playing = value; }
        }
        public bool Merging
        {
            get { return cctrlsPlayers.SyncMerging; }
            set { cctrlsPlayers.SyncMerging = value; }
        }
        #endregion
        
        #region Members
        private ThumbnailViewerContainer thumbnailViewerContainer = new ThumbnailViewerContainer();
        private CommonControlsPlayers cctrlsPlayers = new CommonControlsPlayers();
        private CommonControlsCapture cctrlsCapture = new CommonControlsCapture();
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        #endregion
        
        public ScreenManagerUserInterface(ICommonControlsManager screenManager)
        {
            log.Debug("Constructing ScreenManagerUserInterface.");
            InitializeComponent();

            cctrlsPlayers.Dock = DockStyle.Fill;
            cctrlsPlayers.SetManager(screenManager);
            cctrlsCapture.Dock = DockStyle.Fill;
            cctrlsCapture.SetManager(screenManager);

            BackColor = Color.White;
            Dock = DockStyle.Fill;
            
            InitializeThumbnailsContainer();
            
            delegateUpdateTrackerFrame = UpdateTrkFrame;

            thumbnailViewerContainer.BringToFront();
            pnlScreens.BringToFront();
            pnlScreens.Dock = DockStyle.Fill;

            Application.Idle += this.IdleDetector;
        }
        
        #region Public methods
        public void RefreshUICulture()
        {
            cctrlsPlayers.RefreshUICulture();
            thumbnailViewerContainer.RefreshUICulture();
        }
        public void ShowCommonControls(bool show, Pair<Type, Type> types)
        {
            splitScreensPanel.Panel2Collapsed = !show;
            if (types == null)
                return;

            splitScreensPanel.Panel2.Controls.Clear();

            if (types.First == typeof(PlayerScreen))
                splitScreensPanel.Panel2.Controls.Add(cctrlsPlayers);
            else
                splitScreensPanel.Panel2.Controls.Add(cctrlsCapture);
        }
        public void ToggleCommonControls()
        {
            splitScreensPanel.Panel2Collapsed = !splitScreensPanel.Panel2Collapsed;
        }
        public void OrganizeScreens(List<AbstractScreen> screenList)
        {
            if(screenList.Count == 0)
            {
                pnlScreens.Visible = false;
                this.AllowDrop = true;
                ClearLeftScreen();
                ClearRightScreen();

                thumbnailViewerContainer.Unhide();
            }
            else
            {
                pnlScreens.Visible = true;
                this.AllowDrop = false;
                
                thumbnailViewerContainer.HideContent();
                
                splitScreens.Panel1.Controls.Clear();
                splitScreens.Panel2.Controls.Clear();
                
                PrepareLeftScreen(screenList[0].UI);
                
                if(screenList.Count == 2)
                    PrepareRightScreen(screenList[1].UI);
                else
                    ClearRightScreen();
            }
        }
        
        #region Forwarded to common controls
        public void SetupTrkFrame(long min, long max, long pos)
        {
            cctrlsPlayers.SetupTrkFrame(min, max, pos);
        }
        public void UpdateTrkFrame(long position)
        {
            cctrlsPlayers.UpdateTrkFrame(position);
        }
        public void UpdateSyncPosition(long position)
        {
            cctrlsPlayers.UpdateSyncPosition(position);
        }
        public void DisplayAsPaused()
        {
            cctrlsPlayers.Playing = false;
        }
        #endregion

        #endregion

        private void InitializeThumbnailsContainer()
        {
            thumbnailViewerContainer.Dock = DockStyle.Fill;
            thumbnailViewerContainer.Visible = true;
            thumbnailViewerContainer.FileLoadAsked += (s,e) => {
                if(FileLoadAsked != null)
                    FileLoadAsked(this, e);
            };
            
            this.Controls.Add(thumbnailViewerContainer);
        }
        private void IdleDetector(object sender, EventArgs e)
        {
            log.Debug("Application is idle in ScreenManagerUserInterface.");
            
            // This is a one time only routine.
            Application.Idle -= new EventHandler(this.IdleDetector);
            
            // Launch file.
            string filePath = CommandLineArgumentManager.Instance().InputFile;
            if(filePath != null && File.Exists(filePath) && FileLoadAsked != null)
                FileLoadAsked(this, new FileLoadAskedEventArgs(filePath, -1));
        }
        private void pnlScreens_Resize(object sender, EventArgs e)
        {
            // Reposition Common Controls panel so it doesn't take more space than necessary.
            splitScreensPanel.SplitterDistance = pnlScreens.Height - 50;
        }
        private void ScreenManagerUserInterface_DoubleClick(object sender, EventArgs e)
        {
            NotificationCenter.RaiseLaunchOpenDialog(this);
        }

        #region Screen management
        private void PrepareLeftScreen(UserControl screenUI)
        {
            splitScreens.Panel1Collapsed = false;
            splitScreens.Panel1.AllowDrop = true;
            splitScreens.Panel1.Controls.Add(screenUI);
            screenUI.Dock = DockStyle.Fill;
        }
        private void PrepareRightScreen(UserControl screenUI)
        {
            splitScreens.Panel2Collapsed = false;
            splitScreens.Panel2.AllowDrop = true;
            splitScreens.Panel2.Controls.Add(screenUI);
            screenUI.Dock = DockStyle.Fill;
        }
        private void ClearLeftScreen()
        {
            splitScreens.Panel1.Controls.Clear();
            splitScreens.Panel1Collapsed = true;
            splitScreens.Panel1.AllowDrop = false;
        }
        private void ClearRightScreen()
        {
            splitScreens.Panel2.Controls.Clear();
            splitScreens.Panel2Collapsed = true;
            splitScreens.Panel2.AllowDrop = false;
        }
        #endregion
        
        #region DragDrop
        private void DroppableArea_DragOver(object sender, DragEventArgs e)
        {
            e.Effect = DragDropEffects.All;
        }
        private void ScreenManagerUserInterface_DragDrop(object sender, DragEventArgs e)
        {
            Drop(e, -1);
        }
        private void splitScreens_Panel1_DragDrop(object sender, DragEventArgs e)
        {
            Drop(e, 0);
        }
        private void splitScreens_Panel2_DragDrop(object sender, DragEventArgs e)
        {
            Drop(e, 1);
        }
        private void Drop(DragEventArgs e, int target)
        {
            if(e.Data.GetDataPresent(typeof(CameraSummary)))
            {
                CameraSummary summary = (CameraSummary)e.Data.GetData(typeof(CameraSummary));
                if(summary != null)
                    CameraTypeManager.LoadCamera(summary, target);
            }
            else if(e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                string filename = (string)e.Data.GetData(DataFormats.StringFormat);
                FileLoadAsked(this, new FileLoadAskedEventArgs(filename, target));
            }
            else if(e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Array fileArray = (Array)e.Data.GetData(DataFormats.FileDrop);
                if (fileArray != null)
                {
                   string filename = fileArray.GetValue(0).ToString();
                   FileLoadAsked(this, new FileLoadAskedEventArgs(filename, target));
                }
            }
        }
        #endregion
    }
}