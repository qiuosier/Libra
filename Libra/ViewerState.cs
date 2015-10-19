using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Libra
{
    /// <summary>
    /// Data to represent the state of a viewer page.
    /// </summary>
    public class ViewerState
    {
        public bool fileLoaded
        {
            get; set;
        }

        public ViewerState (StorageFile pdf)
        {
            this.pdfFile = pdf;
            this.fileLoaded = false;
        }

        public StorageFile pdfFile
        {
            get; set;
        }

        public double hOffset { get; set; }
        public double vOffset { get; set; }
        public double hScrollableOffset { get; set; }
        public double vScrollableOffset { get; set; }
        public float zFactor { get; set; }
        public int currentPageNumber { get; set; }
    }
}
