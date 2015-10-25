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
            this.IsRestoring = false;
        }

        public ViewerState(string token)
        {
            this.pdfToken = token;
            this.fileLoaded = true;
            this.IsCurrentView = true;
            this.IsRestoring = false;
        }

        enum ViewerType : int
        {
            PageView = 1,
            HorizontalView = 2,
            GridView = 3
        }

        public string pdfToken{ get; set; }
        public bool fileLoaded{ get; set; }
        public double hOffset { get; set; }
        public double vOffset { get; set; }
        public double hScrollableOffset { get; set; }
        public double vScrollableOffset { get; set; }
        public float zFactor { get; set; }
        public bool IsCurrentView { get; set; }
        public bool IsRestoring { get; set; }
    }
}
