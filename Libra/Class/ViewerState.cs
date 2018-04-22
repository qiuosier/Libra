using System;
using Windows.UI.Xaml.Controls;

namespace Libra.Class
{
    /// <summary>
    /// Data to represent the state of a viewer page.
    /// </summary>
    public class ViewerState
    {
        // The access token of the pdf file.
        public string pdfToken { get; set; }
        // Indicates if a file is loaded.
        public bool fileLoaded { get; set; }
        // Indicates if the viewer is showing the PDF horizontally
        public bool isHorizontalView { get; set; }

        public double hOffset { get; set; }
        public double vOffset { get; set; }
        public double panelHeight { get; set; }
        public double panelWidth { get; set; }
        public float zFactor { get; set; }
        public int version { get; set; }
        public DateTime lastViewed { get; set; }
        public PageRange visibleRange { get; set; }

        public const int CURRENT_VIEWER_STATE_VERSION = 1;

        public ViewerState()
        {
            this.fileLoaded = false;
            this.isHorizontalView = false;
            this.version = CURRENT_VIEWER_STATE_VERSION;
        }

        public ViewerState(string token)
        {
            this.pdfToken = token;
            this.fileLoaded = true;
            this.isHorizontalView = false;
            this.version = CURRENT_VIEWER_STATE_VERSION;
        }

        public void ResetView()
        {
            isHorizontalView = false;
            hOffset = 0;
            vOffset = 0;
            panelHeight = 0;
            panelWidth = 0;
            zFactor = 1;
            lastViewed = DateTime.Now;
        }
        
        public static ViewerState SaveViewerState(string futureAccessToken, ScrollViewer scrollViewer, StackPanel imagePanel, PageRange visibleRange)
        {
            ViewerState viewerState = new ViewerState(futureAccessToken)
            {
                hOffset = scrollViewer.HorizontalOffset,
                vOffset = scrollViewer.VerticalOffset,
                zFactor = scrollViewer.ZoomFactor,

                panelWidth = imagePanel.ActualWidth,
                panelHeight = imagePanel.ActualHeight,

                visibleRange = visibleRange,
                lastViewed = DateTime.Now
            };

            if (imagePanel.Orientation == Orientation.Horizontal)
                viewerState.isHorizontalView = true;
            
            return viewerState;
        }
    }
}
