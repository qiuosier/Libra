using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace Libra.Class
{
    public sealed partial class ZoomInGrid : Grid, ISemanticZoomInformation
    {
        public ZoomInGrid()
        {
            this.InitializeComponent();
        }
        public void CompleteViewChange()
        {
        }

        public void CompleteViewChangeFrom(SemanticZoomLocation source, SemanticZoomLocation destination)
        {
        }

        public void CompleteViewChangeTo(SemanticZoomLocation source, SemanticZoomLocation destination)
        {
        }

        public void InitializeViewChange()
        {
        }

        public bool IsActiveView
        {
            get;
            set;
        }

        public bool IsZoomedInView
        {
            get;
            set;
        }

        public void MakeVisible(SemanticZoomLocation item)
        {
        }

        public SemanticZoom SemanticZoomOwner
        {
            get;
            set;
        }

        public void StartViewChangeFrom(SemanticZoomLocation source, SemanticZoomLocation destination)
        {
        }

        public void StartViewChangeTo(SemanticZoomLocation source, SemanticZoomLocation destination)
        {
        }
    }
}
