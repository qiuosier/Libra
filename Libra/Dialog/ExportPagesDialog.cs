using System;
using System.Collections.Generic;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Libra.Dialog
{
    class ExportPagesDialog : ContentDialog
    {
        private TextBox enterPagesTextBox;
        private TextBox filenameTextBox;
        private int pageCount;

        /// <summary>
        /// Create a new Content Dialog to export pages to images.
        /// </summary>
        /// <param name="pageCount">Total number of pages in the pdf file.</param>
        public ExportPagesDialog(int pageCount)
        {
            // Initialize a content dialog
            this.Title = "Export page as image";
            this.MaxWidth = Window.Current.Bounds.Width;
            this.PrimaryButtonText = "Export...";
            this.SecondaryButtonText = "Cancel";
            this.pageCount = pageCount;

            // Use a stack panel to hold the elements in the content dialog
            StackPanel panel = new StackPanel();

            // Display a message to user
            panel.Children.Add(new TextBlock()
            {
                Text = "Export the following page(s) as images (e.g. 1-3, 8, 13-14): ",
            });

            // User will input page numbers in this TextBox
            enterPagesTextBox = new TextBox()
            {
                Text = ViewerPage.Current.VisiblePageRange.ToString().Split(' ')[1],
            };
            panel.Children.Add(enterPagesTextBox);

            // Show error messages to user
            TextBlock errorMsgTextBlock = new TextBlock()
            {
                Text = " ",
                Name = "errorMsgTextBlock",
                Foreground = new SolidColorBrush(Colors.Red),
            };
            panel.Children.Add(errorMsgTextBlock);

            // More info about exporting 
            panel.Children.Add(new TextBlock()
            {
                Text = "Each page will be saved as an individual PNG image file in the folder you selected in the next step.\n\n" +
                    "Preferred filename:",
            });

            // User will input filename in this TextBox
            filenameTextBox = new TextBox()
            {
                Text = "PageSnapshot",
            };
            panel.Children.Add(filenameTextBox);

            // Show filename examples to user
            TextBlock fileExampleTextBlock = new TextBlock()
            {
                Text = "The exported images will be named as: PageSnapshot1.PNG, PageSnapshot2.PNG, ...",
            };
            panel.Children.Add(fileExampleTextBlock);

            // Check page numbers
            enterPagesTextBox.KeyUp += (sPages, ePages) =>
            {
                try
                {
                    pagesFromString(enterPagesTextBox.Text, pageCount);
                    errorMsgTextBlock.Text = " ";
                    this.IsPrimaryButtonEnabled = true;
                }
                catch (Exception ex)
                {
                    errorMsgTextBlock.Text = ex.Message;
                    this.IsPrimaryButtonEnabled = false;
                }
            };

            // Check filename
            filenameTextBox.KeyUp += (sFilename, eFilename) =>
            {
                fileExampleTextBlock.Text = "The exported images will be named as: " +
                    filenameTextBox.Text + "1.PNG" + ", " + filenameTextBox.Text + "2.PNG, ...";
            };
            
            // Put the panel into the dialog
            this.Content = panel;
        }

        public List<int> PagesToExport
        {
            get { return pagesFromString(this.enterPagesTextBox.Text, this.pageCount); }
        }

        public string PagesToExportString
        {
            get { return this.enterPagesTextBox.Text; }
        }

        public string ImageFilename
        {
            get { return this.filenameTextBox.Text; }
        }

        /// <summary>
        /// Convert a string to a list of page numbers (integers)
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private List<int> pagesFromString(string s, int pageCount)
        {
            string[] pageStrings = s.Split(',');
            List<int> pageList = new List<int>();
            foreach (string p in pageStrings)
            {
                string[] pages = p.Trim().Split('-');
                if (pages.Length == 1)
                {
                    int i = Convert.ToInt32(pages[0]);
                    if (i < 1 || i > pageCount)
                        throw new Exception("Page number is out of range.");
                    else pageList.Add(Convert.ToInt32(pages[0]));
                }
                else if (pages.Length == 2)
                {
                    for (int i = Convert.ToInt32(pages[0]); i <= Convert.ToUInt32(pages[1]); i++)
                        if (i < 1 || i > pageCount)
                            throw new Exception("Page number is out of range.");
                        else pageList.Add(i);
                }
                else throw new Exception("The entered page numbers are not valid.");
            }
            return pageList;
        }
    }
}
