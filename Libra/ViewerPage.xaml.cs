using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Libra
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ViewerPage : Page
    {
        private static int SCROLLBAR_WIDTH = 10;
        private static int PAGE_IMAGE_MARGIN = 15;
        private static int PAGE_BUFFER = 3;

        private PdfDocument pdfDocument;
        private DispatcherTimer myTimer;

        private int currentPageNumber;
        private int pageCount;
        private double pageHeight;
        private double pageWidth;
        private bool fileLoaded;
        
        public ViewerPage()
        {
            this.InitializeComponent();
            this.currentPageNumber = 0;
            this.pageHeight = 0;
            this.pageWidth = 0;
            this.fileLoaded = false;
            this.myTimer = new DispatcherTimer();
            myTimer.Tick += MyTimer_Tick;
            myTimer.Interval = new TimeSpan(Convert.ToInt32(5e5));
            
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            StorageFile pdfFile = e.Parameter as StorageFile;
            this.LoadFile(pdfFile);
        }

        private async void LoadFile(StorageFile pdfFile)
        {
            IAsyncOperation<PdfDocument> getPdfTask = PdfDocument.LoadFromFileAsync(pdfFile);

            // Display loading
            this.statusOutput.Text = "Loading...";
            // Wait until the file is loaded
            this.pdfDocument = await getPdfTask;
            this.pageCount = (int)pdfDocument.PageCount;
            // Add pages to scroll viewer
            pageWidth = Window.Current.Bounds.Width - 2 * PAGE_IMAGE_MARGIN - SCROLLBAR_WIDTH;
            Thickness pageMargin = new Thickness(PAGE_IMAGE_MARGIN);
            System.Diagnostics.Stopwatch myWatch = new System.Diagnostics.Stopwatch();
            myWatch.Start();
            // Add the first 5 pages with high quiality
            for (int i = 1; i <= Math.Min(5, pageCount); i++)
            {
                Image image = new Image();
                image.Name = "page" + i.ToString();
                image.Margin = pageMargin;
                image.Width = pageWidth;
                this.imagePanel.Children.Add(image);
                await LoadPage(i, 1500);
                image = (Image)this.FindName("page1");
                pageHeight = image.ActualHeight;
            }
            // Add blank pages for the rest of the file
            for (int i = 6; i <= pageCount; i++)
            {
                Image image = new Image();
                image.Name = "page" + i.ToString();
                image.Margin = pageMargin;
                image.Width = pageWidth;
                image.Height = pageHeight;
                this.imagePanel.Children.Add(image);
                //LoadPage(i, 10);
            }
            myWatch.Stop();
            this.statusOutput.Text = "Finished Loading in " + myWatch.Elapsed.TotalSeconds.ToString();
            // Show first page
            //PreparePages(1);
            SetZoomFactor();
            currentPageNumber = 1;
            this.fileLoaded = true;
        }

        private async void PreparePages(int newPageNumber)
        {
            int diff = newPageNumber - currentPageNumber;
            int oldPageNumber = currentPageNumber;
            this.currentPageNumber = newPageNumber;
            if (oldPageNumber == 0 || Math.Abs(diff) > 2 * PAGE_BUFFER + 1)
            {
                for (int i = newPageNumber - PAGE_BUFFER; i <= newPageNumber + PAGE_BUFFER; i++)
                {
                    await LoadPage(i);
                }
            }
            else if (diff > 0)
            {
                for (int i = 0; i < diff; i++)
                {
                    // Remove pages
                    // RemovePage(oldPageNumber - PAGE_BUFFER + i);
                    // Add Pages
                    await LoadPage(oldPageNumber + PAGE_BUFFER + i + 1);
                }
            }
            else // diff < 0
            {
                for (int i = 0; i > diff; i--)
                {
                    // Remove pages
                    // RemovePage(oldPageNumber - PAGE_BUFFER + i);
                    // Add Pages
                    await LoadPage(oldPageNumber - PAGE_BUFFER - i - 1);
                }
            }
            // Store current page number
            this.currentPageNumber = newPageNumber;
            return;
        }

        private double RemovePage(int pageNumber)
        {
            Image image = (Image)this.FindName("page" + pageNumber.ToString());
            if (image != null)
            {
                //image.Source = null;
                return image.ActualHeight + 2 * PAGE_IMAGE_MARGIN;
            }
            else return 0;
        }

        private async Task LoadPage(int pageNumber, uint renderWidth = 1500)
        {
            if (pageNumber < 1 || pageNumber > pageCount)
                return;
            Image image = (Image)this.imagePanel.FindName("page" + pageNumber.ToString());
            if (image != null)
            {
                if (image.Source != null) return;
                InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
                PdfPage page = pdfDocument.GetPage(Convert.ToUInt32(pageNumber - 1));
                PdfPageRenderOptions options = new PdfPageRenderOptions();
                options.DestinationWidth = renderWidth;
                IAsyncAction action = page.RenderToStreamAsync(stream, options);
                await action;
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.SetSource(stream);
                image.Source = bitmapImage;
            }
        }

        private Boolean IsUserVisible(FrameworkElement element, FrameworkElement container)
        {
            if (element == null)
                return false;
            Rect elementBounds = element.TransformToVisual(container)
                .TransformBounds(new Rect(0.0, 0.0, element.ActualWidth, element.ActualHeight));
            Rect containerBounds = new Rect(0.0, 0.0, container.ActualWidth, container.ActualHeight);
            return (elementBounds.Top < containerBounds.Bottom && elementBounds.Bottom > containerBounds.Top)
                && (elementBounds.Left < containerBounds.Right && elementBounds.Right > containerBounds.Left);
        }

        private Boolean IsPageVisible(int pageNumber)
        {
            return IsUserVisible((Image)this.FindName("page" + pageNumber.ToString()), this.scrollViewer);
        }

        private void RefreshViewer()
        {
            // Find the page that is currently visible, only one page is visible at a time
            // Check current page
            if (IsPageVisible(currentPageNumber))
                return;
            // Find out which page is visible
            int p;
            if (imagePanel.Orientation == Orientation.Vertical)
                p = (int)Math.Ceiling(scrollViewer.VerticalOffset / scrollViewer.ScrollableHeight * pageCount);
            else
                p = (int)Math.Ceiling(scrollViewer.HorizontalOffset / scrollViewer.ScrollableWidth * pageCount);
            if (p < 0) p = 1;
            for (int i = 0; i <= pageCount; i++)
                if (IsPageVisible(p + i))
                {
                    PreparePages(p + i);
                    return;
                }
                else if (IsPageVisible(p - i))
                {
                    PreparePages(p - i);
                    return;
                }
        }

        private void SetZoomFactor()
        {
            double hZoomFactor = (this.scrollViewer.ActualHeight 
                - 2 * (PAGE_IMAGE_MARGIN + imagePanel.Margin.Top)) / pageHeight;
            double wZoomFactor = (this.scrollViewer.ActualWidth 
                - 2 * (PAGE_IMAGE_MARGIN + imagePanel.Margin.Left)) / pageWidth;
            this.scrollViewer.MinZoomFactor = (float)Math.Min(hZoomFactor, wZoomFactor);
        }

        private void MyTimer_Tick(object sender, object e)
        {
            myTimer.Stop();
            this.RefreshViewer();
        }

        private void scrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {

        }

        private void scrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            //this.RefreshViewer();
            myTimer.Stop();
            myTimer.Start();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.statusOutput.Text = "Zoom Factor: " + this.scrollViewer.ZoomFactor.ToString();
            if (fileLoaded)
            {
                // Store current zoom factor
                double factor = scrollViewer.ZoomFactor;
                // Recalculate min and max zoom factor
                SetZoomFactor();
                // Recalculate offsets
                factor = scrollViewer.ZoomFactor / factor;
                scrollViewer.ChangeView(factor * scrollViewer.HorizontalOffset, 
                    factor * scrollViewer.VerticalOffset, scrollViewer.ZoomFactor,true);
            }
        }
    }
}
