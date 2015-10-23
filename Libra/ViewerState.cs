using System.Collections.Generic;
using System.IO;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Input.Inking;

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
        }

        public ViewerState(string token)
        {
            this.pdfToken = token;
            this.fileLoaded = true;
        }

        public string pdfToken{ get; set; }
        public bool fileLoaded{ get; set; }
        public double hOffset { get; set; }
        public double vOffset { get; set; }
        public double hScrollableOffset { get; set; }
        public double vScrollableOffset { get; set; }
        public float zFactor { get; set; }
    }
}
