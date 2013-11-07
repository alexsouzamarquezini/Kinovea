﻿#region License
/*
Copyright © Joan Charmant 2012.
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
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

using Kinovea.ScreenManager.Languages;
using Kinovea.Services;

namespace Kinovea.ScreenManager
{
    [XmlType ("CoordinateSystem")]
    public class DrawingCoordinateSystem : AbstractDrawing, IScalable, ITrackable, IMeasurable, IDecorable
    {
        #region Events
        public event EventHandler<TrackablePointMovedEventArgs> TrackablePointMoved; 
        public event EventHandler ShowMeasurableInfoChanged = delegate {}; // not used.
        #endregion
        
        #region Properties
        public override string DisplayName
        {
            get {  return ScreenManagerLang.mnuCoordinateSystem; }
        }
        public override int ContentHash
        {
            get 
            { 
                int hash = Visible.GetHashCode();
                return hash;
            }
        } 
        public DrawingStyle DrawingStyle
        {
            get { return style;}
        }
        public override InfosFading InfosFading
        {
            get { return null; }
            set { }
        }
        public override DrawingCapabilities Caps
        {
            get { return DrawingCapabilities.ConfigureColor | DrawingCapabilities.Track; }
        }
        public override List<ToolStripItem> ContextMenu
        {
            get 
            { 
                // Rebuild the menu to get the localized text.
                List<ToolStripItem> contextMenu = new List<ToolStripItem>();
                
                menuShowAxis.Text = ScreenManagerLang.mnuCoordinateSystemShowAxis;
                menuShowGrid.Text = ScreenManagerLang.mnuCoordinateSystemShowGrid;
                menuShowGraduations.Text = ScreenManagerLang.mnuCoordinateSystemShowTickMarks;
                menuHide.Text = ScreenManagerLang.mnuCoordinateSystemHide;
                
                menuShowAxis.Checked = showAxis;
                menuShowGrid.Checked = showGrid;
                menuShowGraduations.Checked = showGraduations;
                
                contextMenu.Add(menuShowAxis);
                contextMenu.Add(menuShowGrid);
                contextMenu.Add(menuShowGraduations);
                contextMenu.Add(new ToolStripSeparator());
                contextMenu.Add(menuHide);
                
                return contextMenu; 
            }
        }
        
        public bool Visible { get; set; }
        public CalibrationHelper CalibrationHelper {get; set;}
        public bool ShowMeasurableInfo { get; set; }
        #endregion

        #region Members
        private Guid id = Guid.NewGuid();
        private Dictionary<string, Point> points = new Dictionary<string, Point>();
        private bool showAxis = true;
        private bool showGrid = true;
        private bool showGraduations = true;
        private Size imageSize;

        // Decoration
        private StyleHelper styleHelper = new StyleHelper();
        private DrawingStyle style;

        // Context menu
        private ToolStripMenuItem menuShowAxis = new ToolStripMenuItem();
        private ToolStripMenuItem menuShowGrid = new ToolStripMenuItem();
        private ToolStripMenuItem menuShowGraduations = new ToolStripMenuItem();
        private ToolStripMenuItem menuHide = new ToolStripMenuItem();
        
        private const int defaultBackgroundAlpha = 92;
        private const int gridAlpha = 128;
        private const int textMargin = 8;
        #endregion

        #region Constructors
        public DrawingCoordinateSystem(Point origin, DrawingStyle stylePreset)
        {
            points["0"] = origin;
            
            // Decoration & binding with editors
            styleHelper.Bicolor = new Bicolor(Color.Empty);
            styleHelper.Font = new Font("Arial", 10, FontStyle.Bold);
            if(stylePreset != null)
            {
                style = stylePreset.Clone();
                BindStyle();
            }
            
            // Context menu
            menuShowAxis.Click += menuShowAxis_Click;
            menuShowGrid.Click += menuShowGrid_Click;
            menuShowGraduations.Click += menuShowGraduations_Click;
            menuHide.Click += menuHide_Click;
            
            menuShowAxis.Image = Properties.Drawings.coordinates_axis;
            menuShowGrid.Image = Properties.Drawings.coordinates_grid;
            menuShowGraduations.Image = Properties.Drawings.coordinates_graduations;
            menuHide.Image = Properties.Drawings.hide;
        }
        #endregion

        #region AbstractDrawing Implementation
        public override void Draw(Graphics canvas, IImageToViewportTransformer transformer, bool selected, long currentTimestamp)
        {
            if(!Visible || CalibrationHelper == null)
                return;

            RectangleF bounds = CalibrationHelper.GetBoundingRectangle(imageSize);
            float stepSizeWidth = RulerStepSize(bounds.Width, 12);
            float stepSizeHeight = stepSizeWidth;

            if (CalibrationHelper.CalibratorType == CalibratorType.Plane)
                stepSizeHeight = RulerStepSize(bounds.Height, 12);

            using (Pen penLine = styleHelper.GetBackgroundPen(255))
            {
                DrawGrid(canvas, transformer, bounds, stepSizeWidth, stepSizeHeight);
            }
        }
        private void DrawGrid(Graphics canvas, IImageToViewportTransformer transformer, RectangleF bounds, float stepWidth, float stepHeight)
        {
            Pen p = styleHelper.GetBackgroundPen(gridAlpha);
            SolidBrush brushFill = styleHelper.GetBackgroundBrush(defaultBackgroundAlpha);
            SolidBrush fontBrush = styleHelper.GetForegroundBrush(255);
            Font font = styleHelper.GetFont(1.0F);

            float top = bounds.Y;
            float bottom = bounds.Y - bounds.Height;

            // Verticals
            float x = 0;
            while (x <= bounds.Right)
            {
                p.DashStyle = x == 0 ? DashStyle.Solid : DashStyle.Dash;
                TextAlignment alignment = x == 0 ? TextAlignment.BottomRight : TextAlignment.Bottom;
                DrawLine(canvas, transformer, p, new PointF(x, top), new PointF(x, bottom));
                DrawStepTextForPlane(canvas, transformer, new PointF(x, 0), alignment, x, brushFill, fontBrush, font);
                x += stepWidth;
            }
            x = -stepWidth;
            while (x >= bounds.Left)
            {
                DrawLine(canvas, transformer, p, new PointF(x, top), new PointF(x, bottom));
                DrawStepTextForPlane(canvas, transformer, new PointF(x, 0), TextAlignment.Bottom, x, brushFill, fontBrush, font);
                x -= stepWidth;
            }

            // Horizontals
            float y = 0;
            while (y >= bottom)
            {
                p.DashStyle = y == 0 ? DashStyle.Solid : DashStyle.Dash;
                DrawLine(canvas, transformer, p, new PointF(bounds.Left, y), new PointF(bounds.Right, y));
                if (y != 0)
                    DrawStepTextForPlane(canvas, transformer, new PointF(0, y), TextAlignment.Left, y, brushFill, fontBrush, font);
                y -= stepHeight;
            }
            y = stepHeight;
            while (y <= top)
            {
                DrawLine(canvas, transformer, p, new PointF(bounds.Left, y), new PointF(bounds.Right, y));
                DrawStepTextForPlane(canvas, transformer, new PointF(0, y), TextAlignment.Left, y, brushFill, fontBrush, font);
                y += stepHeight;
            }

            font.Dispose();
            fontBrush.Dispose();
            brushFill.Dispose();
            p.Dispose();
        }

        private void DrawStepTextForPlane(Graphics canvas, IImageToViewportTransformer transformer, PointF tickPosition, TextAlignment textAlignment, float tickValue, SolidBrush brushFill, SolidBrush fontBrush, Font font)
        {
            string label = String.Format("{0}", tickValue);
            PointF loc = transformer.Transform(CalibrationHelper.GetImagePoint(tickPosition));

            SizeF labelSize = canvas.MeasureString(label, font);
            PointF textPosition = GetTextPosition(loc, textAlignment, labelSize);
            RectangleF backRectangle = new RectangleF(textPosition, labelSize);

            RoundedRectangle.Draw(canvas, backRectangle, brushFill, font.Height / 4, false, false, null);
            canvas.DrawString(label, font, fontBrush, backRectangle.Location);
        }

        private void DrawLine(Graphics canvas, IImageToViewportTransformer transformer, Pen penLine, PointF a, PointF b)
        {
            Point p1 = transformer.Transform(CalibrationHelper.GetImagePoint(a));
            Point p2 = transformer.Transform(CalibrationHelper.GetImagePoint(b));
            canvas.DrawLine(penLine, p1, p2);
        }

        public override int HitTest(Point point, long currentTimestamp, IImageToViewportTransformer transformer, bool zooming)
        {
            // Convention: miss = -1, object = 0, handle = n.
            if(!Visible)
                return -1;
            
            int result = -1;
            int boxSide = transformer.Untransform(10);
            
            if (points["0"].Box(boxSide).Contains(point))
                return 1;
            
            if(showGrid || showGraduations || showAxis)
            {
                if(IsPointOnHorizontalAxis(point))
                    result = 2;
                else if(IsPointOnVerticalAxis(point))
                    result = 3;
            }
            
            return result;
        }
        public override void MoveHandle(Point point, int handleNumber, Keys modifiers)
        {
            if(handleNumber == 1)
                points["0"] = point;
            else if(handleNumber == 2)
                points["0"] = new Point(points["0"].X, point.Y);
            else if(handleNumber == 3)
                points["0"] = new Point(point.X, points["0"].Y);

            CalibrationHelper.SetOrigin(points["0"]);
            SignalTrackablePointMoved();
        }
        public override void MoveDrawing(int _deltaX, int _deltaY, Keys _ModifierKeys, bool zooming)
        {
        }
        
        #endregion
        
        #region ITrackable implementation and support.
        public Guid ID
        {
            get { return id; }
        }
        public TrackingProfile CustomTrackingProfile
        {
            get { return null; }
        }
        public Dictionary<string, Point> GetTrackablePoints()
        {
            return points;
        }
        public void SetTracking(bool tracking)
        {
        }
        public void SetTrackablePointValue(string name, Point value)
        {
            if(!points.ContainsKey(name))
                throw new ArgumentException("This point is not bound.");
            
            points[name] = value;
            CalibrationHelper.SetOrigin(value);
        }
        private void SignalTrackablePointMoved()
        {
            if(TrackablePointMoved == null)
                return;
            
            TrackablePointMoved(this, new TrackablePointMovedEventArgs("0", points["0"]));
        }
        #endregion

        #region IScalable implementation
        public void Scale(Size imageSize)
        {
            this.imageSize = imageSize;
            points["0"] = new Point(imageSize.Width / 2, imageSize.Height / 2);
        }
        #endregion
        
        #region Context menu
        private void menuShowAxis_Click(object sender, EventArgs e)
        {
            showAxis = !showAxis;
            CallInvalidateFromMenu(sender);
        }
        private void menuShowGrid_Click(object sender, EventArgs e)
        {
            showGrid = !showGrid;
            CallInvalidateFromMenu(sender);
        }
        private void menuShowGraduations_Click(object sender, EventArgs e)
        {
            showGraduations = !showGraduations;
            CallInvalidateFromMenu(sender);
        }
        private void menuHide_Click(object sender, EventArgs e)
        {
            Visible = false;
            CallInvalidateFromMenu(sender);
        }
        #endregion

        #region Serialization
        public void WriteXml(XmlWriter w)
        {
            w.WriteElementString("Visible", Visible.ToString().ToLower());
        }
        public void ReadXml(XmlReader r)
        {
            r.ReadStartElement();
            
            while(r.NodeType == XmlNodeType.Element)
            {
                switch (r.Name)
                {
                    case "Visible":
                        Visible = XmlHelper.ParseBoolean(r.ReadElementContentAsString());
                        break;
                    default:
                        string unparsed = r.ReadOuterXml();
                        break;
                }
            }

            r.ReadEndElement();
        }            
        #endregion

        public void UpdateOrigin()
        {
            if(CalibrationHelper != null)
            {
                PointF p = CalibrationHelper.GetImagePoint(PointF.Empty);
                points["0"] = new Point((int)p.X, (int)p.Y);
            }
        }
        
        #region Lower level helpers
        private void BindStyle()
        {
            style.Bind(styleHelper, "Bicolor", "line color");
        }
        private PointF GetTextPosition(PointF tickPosition, TextAlignment textAlignment, SizeF textSize)
        {
            PointF textPosition = tickPosition;
            
            switch (textAlignment)
            {
                case TextAlignment.Top: 
                    textPosition = new PointF(tickPosition.X - textSize.Width / 2, tickPosition.Y - textSize.Height - textMargin);
                    break;
                case TextAlignment.Left:
                    textPosition = new PointF(tickPosition.X - textSize.Width - textMargin, tickPosition.Y - textSize.Height / 2);
                    break;
                case TextAlignment.Right:
                    textPosition = new PointF(tickPosition.X + textMargin, tickPosition.Y - textSize.Height / 2);
                    break;
                case TextAlignment.Bottom:
                    textPosition = new PointF(tickPosition.X - textSize.Width / 2, tickPosition.Y + textMargin);
                    break;
                case TextAlignment.BottomRight:
                    textPosition = new PointF(tickPosition.X + textMargin, tickPosition.Y + textMargin);
                    break;
            }
            
            return textPosition;
        }
        private bool IsPointOnHorizontalAxis(Point p)
        {
            int widenRadius = 5;
            Rectangle axis = new Rectangle(0, points["0"].Y - widenRadius, imageSize.Width, widenRadius * 2);
            return axis.Contains(p);
        }
        private bool IsPointOnVerticalAxis(Point p)
        {
            int widenRadius = 5;
            Rectangle axis = new Rectangle(points["0"].X - widenRadius, 0, widenRadius * 2, imageSize.Height);
            return axis.Contains(p);
        }
        
        /// <summary>
        /// Utility function to find nice spacing for tick marks.
        /// </summary>
        private static float RulerStepSize(float range, float targetSteps)
        {
            float minimum = range/targetSteps;

            // Find magnitude of the initial guess.
            float magnitude = (float)Math.Floor(Math.Log10(minimum));
            float orderOfMagnitude = (float)Math.Pow(10, magnitude);

            // Reduce the number of steps.
            float residual = minimum / orderOfMagnitude;
            float stepSize;
            
            if(residual > 5)
                stepSize = 10 * orderOfMagnitude;
            else if (residual > 2)
                stepSize = 5 * orderOfMagnitude;
            else if (residual > 1)
                stepSize = 2 * orderOfMagnitude;
            else
                stepSize = orderOfMagnitude;
                
            return stepSize;
        }
        #endregion
        
        private enum TextAlignment
        {
            Top,
            Left,
            Right,
            Bottom,
            BottomRight
        }

    }
}
