using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Libra.Class;

// The Content Dialog item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Libra.Dialog
{
    public sealed partial class InkingPrefContentDialog : ContentDialog
    {
        public InkingPreference InkingPreference { get; private set; }

        private List<Brush> penColors = new List<Brush>()
        {
            new SolidColorBrush(Colors.Red),
            new SolidColorBrush(Colors.Black),
            new SolidColorBrush(Colors.Blue),
            new SolidColorBrush(Colors.Yellow),
            new SolidColorBrush(Colors.Green),
            new SolidColorBrush(Colors.Magenta),
            new SolidColorBrush(Colors.OrangeRed),
        };

        private List<Brush> highlighterColors;

        public InkingPrefContentDialog(InkingPreference inkingPref)
        {
            this.InitializeComponent();
            this.MaxWidth = Window.Current.Bounds.Width;
            this.InkingPreference = inkingPref;

            // Assign values from inkingPref
            //this.penSizeLabel.Text = "Pen size: " + this.InkingPreference.penSize.ToString();
            this.penSizeSlider.Value = this.InkingPreference.penSize;
            this.penSizeEllipse.Fill = new SolidColorBrush(this.InkingPreference.penColor);
            this.penSizeEllipse.Height = this.InkingPreference.penSize;
            this.penSizeEllipse.Width = this.InkingPreference.penSize;

            this.highlighterSizeSlider.Value = this.InkingPreference.highlighterSize;
            this.highlighterSizeRectangle.Fill = new SolidColorBrush(this.InkingPreference.highlighterColor);
            this.highlighterSizeRectangle.Height = this.InkingPreference.highlighterSize;
            this.highlighterSizeRectangle.Width = this.InkingPreference.highlighterSize;

            // Binding colors to ListBox
            this.highlighterColors = this.penColors;
            this.penColorListBox.DataContext = this.penColors;
            this.highlighterColorListBox.DataContext = this.highlighterColors;
        }

        /// <summary>
        /// Highlight the current pen and highlighter color when the dialog is fully loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ContentDialog_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (SolidColorBrush item in penColorListBox.Items)
            {
                if (this.InkingPreference.penColor == item.Color)
                    ((ListBoxItem)penColorListBox.ContainerFromItem(item)).IsSelected = true;
            }
            foreach (SolidColorBrush item in highlighterColorListBox.Items)
            {
                if (this.InkingPreference.highlighterColor == item.Color)
                    ((ListBoxItem)highlighterColorListBox.ContainerFromItem(item)).IsSelected = true;
            }
        }

        /// <summary>
        /// Event handler for OK button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Save size values
            this.InkingPreference.penSize = (int)this.penSizeSlider.Value;
            this.InkingPreference.highlighterSize = (int)this.highlighterSizeSlider.Value;
            // Save color values
            this.InkingPreference.penColor = ((SolidColorBrush)penColorListBox.SelectedItem).Color;
            this.InkingPreference.highlighterColor = ((SolidColorBrush)highlighterColorListBox.SelectedItem).Color;
        }

        /// <summary>
        /// Event handler for Cancel button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            // Do nothing
        }

        /// <summary>
        /// Show pen size to user.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void penSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (this.penSizeEllipse != null)
            {
                this.penSizeEllipse.Height = (int)e.NewValue;
                this.penSizeEllipse.Width = (int)e.NewValue;
            }
        }

        /// <summary>
        /// Show highlighter size to user
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void highlighterSizeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (this.highlighterSizeRectangle != null)
            {
                this.highlighterSizeRectangle.Height = (int)e.NewValue;
                this.highlighterSizeRectangle.Width = (int)e.NewValue;
            }
        }

        private void penColorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.penSizeEllipse.Fill = (SolidColorBrush)penColorListBox.SelectedItem;
        }

        private void highlighterColorListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            this.highlighterSizeRectangle.Fill = (SolidColorBrush)highlighterColorListBox.SelectedItem;
        }
    }
}
