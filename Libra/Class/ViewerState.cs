using System;

namespace Libra
{
    /// <summary>
    /// Data to represent the state of a viewer page.
    /// </summary>
    public class ViewerState
    {
        public ViewerState()
        {
            this.fileLoaded = false;
            this.horizontalView = false;
            this.version = CURRENT_VIEWER_STATE_VERSION;
        }

        public ViewerState(string token)
        {
            this.pdfToken = token;
            this.fileLoaded = true;
            this.horizontalView = false;
            this.version = CURRENT_VIEWER_STATE_VERSION;
        }

        public string pdfToken{ get; set; }
        public bool fileLoaded{ get; set; }
        public bool horizontalView { get; set; }
        public double hOffset { get; set; }
        public double vOffset { get; set; }
        public double panelHeight { get; set; }
        public double panelWidth { get; set; }
        public float zFactor { get; set; }
        public int version { get; set; }
        public DateTime lastViewed { get; set; }

        public const int CURRENT_VIEWER_STATE_VERSION = 1;
    }
}
