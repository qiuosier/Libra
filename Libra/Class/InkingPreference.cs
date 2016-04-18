using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Core;

namespace Libra.Class
{
    /// <summary>
    /// Contains user preferences for inking.
    /// </summary>
    public class InkingPreference
    {
        public InkingPreference()
        {
            penSize = 1;
            highlighterSize = 10;
            penColor = Colors.Red;
            highlighterColor = Colors.Yellow;
            drawingDevice = CoreInputDeviceTypes.Mouse | CoreInputDeviceTypes.Pen;
            this.version = CURRENT_INKING_PREF_VERSION;
        }

        public int penSize;
        public int highlighterSize;
        public Color penColor;
        public Color highlighterColor;
        public CoreInputDeviceTypes drawingDevice;
        public int version;

        public Size GetPenSize(double scale)
        {
            return new Size(penSize / scale, penSize / scale);
        }

        public Size GetHighlighterSize(double scale)
        {
            return new Size(highlighterSize / scale, highlighterSize / scale);
        }

        public const int CURRENT_INKING_PREF_VERSION = 2;
    }
}
