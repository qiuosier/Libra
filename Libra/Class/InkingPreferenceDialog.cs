using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Libra
{
    class InkingPreferenceDialog : ContentDialog
    {
        /// <summary>
        /// Create a new Content Dialog for inking preference.
        /// </summary>
        /// <param name="pageCount">Total number of pages in the pdf file.</param>
        public InkingPreferenceDialog(InkingPreference inkingPref)
        {
            // Initialize a content dialog
            this.Title = "Inking Preference";
            this.MaxWidth = Window.Current.Bounds.Width;
            this.PrimaryButtonText = "OK";
            this.SecondaryButtonText = "Cancel";

            // Use a stack panel to hold the elements in the content dialog
            StackPanel panel = new StackPanel();

            // Put the panel into the dialog
            this.Content = panel;
        }
    }
}
