using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            penSize = 2;
            highlighterSize = 12;
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

        public const int CURRENT_INKING_PREF_VERSION = 1;
    }
}
