﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using NavigationMenu;
using Windows.UI.Input.Inking;
using System.IO;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Libra
{
    /// <summary>
    /// 
    /// </summary>
    public sealed partial class ViewerPage : Page
    {
        private const int SCROLLBAR_WIDTH = 10;
        private const int PAGE_IMAGE_MARGIN = 15;
        private const int PAGE_BUFFER = 5;
        private const int RECYCLE_QUEUE_SIZE = 10;
        private const int BLANK_PAGE_BATCH = 100;
        private const int FIRST_LOAD_PAGES = 2;
        private const int REFRESH_TIMER_TICKS = 50 * 10000;
        private const int INITIALIZATION_TIMER_TICKS = 10 * 10000;
        private const int RECYCLE_TIMER_SECOND = 1;
        

        private const string PREFIX_PAGE = "page";
        private const string PREFIX_GRID = "grid";
        private const string PREFIX_CANVAS = "canvas";

        private StorageFile pdfFile;
        private PdfDocument pdfDocument;
        private Thickness pageMargin;
        private PageRange currentRange;
        private DispatcherTimer refreshTimer;
        private DispatcherTimer initializationTimer;
        private DispatcherTimer recycleTimer;
        private Queue<int> recyclePagesQueue;
        private Dictionary<int, InkStrokeContainer> inkStrokeDictionary;
        private List<int> inkCanvasList;
        private InkDrawingAttributes drawingAttributes;
        private Windows.UI.Core.CoreInputDeviceTypes drawingDevice;

        private System.Diagnostics.Stopwatch myWatch;

        private int pageCount;
        private double pageHeight;
        private double pageWidth;
        private bool fileLoaded;
        private bool viewInitialized;
        private int penSize;

        public ViewerPage()
        {
            this.InitializeComponent();
            this.currentRange = new PageRange();
            this.pageMargin = new Thickness(PAGE_IMAGE_MARGIN);
            this.pageHeight = 0;
            this.pageWidth = 0;
            this.fileLoaded = false;
            this.viewInitialized = false;

            this.recyclePagesQueue = new Queue<int>();

            this.refreshTimer = new DispatcherTimer();
            this.refreshTimer.Tick += RefreshTimer_Tick;
            this.refreshTimer.Interval = new TimeSpan(REFRESH_TIMER_TICKS);

            this.initializationTimer = new DispatcherTimer();
            this.initializationTimer.Tick += InitializationTimer_Tick;
            this.initializationTimer.Interval = new TimeSpan(INITIALIZATION_TIMER_TICKS);

            this.recycleTimer = new DispatcherTimer();
            this.recycleTimer.Tick += RecycleTimer_Tick;
            this.recycleTimer.Interval = new TimeSpan(0, 0, RECYCLE_TIMER_SECOND);
            this.recycleTimer.Start();

            penSize = 1;
            drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.Color = Windows.UI.Colors.Red;
            drawingAttributes.Size = new Size(penSize, penSize);
            drawingAttributes.IgnorePressure = false;
            drawingAttributes.FitToCurve = true;

            this.drawingDevice = Windows.UI.Core.CoreInputDeviceTypes.Mouse | Windows.UI.Core.CoreInputDeviceTypes.Pen;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter != null)
            {
                StorageFile argumentFile = e.Parameter as StorageFile;
                if (this.pdfFile != null && this.pdfFile != argumentFile)
                {
                    // Another file already opened
                    // TODO: Save the existing file

                }
                this.pdfFile = argumentFile;
                LoadFile(this.pdfFile);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            // Save the viewer state to suspension manager
            if (this.fileLoaded)
            {
                SuspensionManager.PageViewerState = SaveViewerState();
            }
        }

        private ViewerState SaveViewerState()
        {
            string token = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Add(pdfFile);
            ViewerState viewerState = new ViewerState(token);
            viewerState.hOffset = this.scrollViewer.HorizontalOffset;
            viewerState.vOffset = this.scrollViewer.VerticalOffset;
            viewerState.hScrollableOffset = this.scrollViewer.ScrollableHeight;
            viewerState.vScrollableOffset = this.scrollViewer.ScrollableWidth;
            viewerState.zFactor = this.scrollViewer.ZoomFactor;
            //viewerState.drawingAttributes = this.drawingAttributes;
            viewerState.drawingDevice = this.drawingDevice;
            SaveInkStrokes();
            return viewerState;
        }

        private void RestoreViewerState(ViewerState viewerState)
        {
            // TODO: Restore the view

            // Restore the inking
            //this.drawingAttributes = viewerState.drawingAttributes;
            this.drawingDevice = viewerState.drawingDevice;
            this.inkStrokeDictionary = SuspensionManager.inkStrokeDictionary;
            //await RestoreInkStrokes(viewerState.inkStreamDictionary);
        }

        private void SaveInkStrokes()
        {
            foreach (int pageNumber in this.inkCanvasList)
            {
                SaveInkCanvas(pageNumber);
            }
            SuspensionManager.inkStrokeDictionary = this.inkStrokeDictionary;
            //Dictionary<int, MemoryStream> inkStreamDictionary = new Dictionary<int, MemoryStream>();
            //foreach (KeyValuePair<int, InkStrokeContainer> entry in inkStrokeDictionary)
            //{
            //    InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
            //    await entry.Value.SaveAsync(stream);
            //    MemoryStream s = new MemoryStream();
            //    stream.AsStreamForRead().CopyTo(s);
            //    inkStreamDictionary.Add(entry.Key, s);
            //};
            //return inkStreamDictionary;
        }

        //private async Task RestoreInkStrokes(Dictionary<int, InMemoryRandomAccessStream>  inkStreamDictionary)
        //{
        //    foreach (KeyValuePair<int, InMemoryRandomAccessStream> entry in inkStreamDictionary)
        //    {
        //        InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
        //        await inkStrokeContainer.LoadAsync(entry.Value);
        //        this.inkStrokeDictionary.Remove(entry.Key);
        //        this.inkStrokeDictionary.Add(entry.Key, inkStrokeContainer);
        //    };
        //}

        private async void LoadFile(StorageFile pdfFile)
        {
            if (this.pageCount > 0 && this.imagePanel.Children.Count >= this.pageCount) return;
            IAsyncOperation<PdfDocument> getPdfTask = PdfDocument.LoadFromFileAsync(pdfFile);

            // Display loading
            this.statusOutput.Text = "Loading...";
            // Wait until the file is loaded
            this.pdfDocument = await getPdfTask;
            this.pageCount = (int)pdfDocument.PageCount;
            // Add pages to scroll viewer
            pageWidth = scrollViewer.ActualWidth - 2 * PAGE_IMAGE_MARGIN - SCROLLBAR_WIDTH;
            
            myWatch = new System.Diagnostics.Stopwatch();
            myWatch.Start();
            // Add and load the first two pages
            Image image;
            for (int i = 1; i <= Math.Min(FIRST_LOAD_PAGES, pageCount); i++)
            {
                Grid grid = new Grid();
                grid.Name = PREFIX_GRID + i.ToString();
                grid.Margin = pageMargin;
                image = new Image();
                image.Name = PREFIX_PAGE + i.ToString();
                image.Width = pageWidth;
                grid.Children.Add(image);
                this.imagePanel.Children.Add(grid);
                await LoadPage(i);
            }
            // Update layout to force the calculation of actual height and width of the image
            // otherwise, the following code may be executed BEFORE the stack panel update layout.
            this.imagePanel.UpdateLayout();
            // Use the height of the second page to initialize the rest of the document
            // This may cause some problem... will fix this later
            if (pageCount > 1)
            {
                image = (Image)this.FindName(PREFIX_PAGE + "2");
                this.pageHeight = image.ActualHeight;
            }
            // TODO: Load inking if exist
            this.inkStrokeDictionary = new Dictionary<int, InkStrokeContainer>();
            this.inkCanvasList = new List<int>();
            // Add blank pages for the rest of the file using the initialization timer
            this.initializationTimer.Start(); 
        }

        private void FinishInitialization()
        {
            this.imagePanel.UpdateLayout();
            SetZoomFactor();
            this.fullScreenCover.Visibility = Visibility.Collapsed;
            this.fileLoaded = true;
            this.viewInitialized = true;
            if (SuspensionManager.Restoring)
            {
                RestoreViewerState(SuspensionManager.PageViewerState);
                SuspensionManager.Restoring = false;
            }
            RefreshViewer();
            // Load the first a few pages
            myWatch.Stop();
            this.statusOutput.Text = "Finished Preparing the file in " + myWatch.Elapsed.TotalSeconds.ToString();
        }

        private async void PreparePages(PageRange range)
        {
            // Add invisible pages to recycle list
            for (int i = currentRange.first - PAGE_BUFFER; i <= currentRange.last + PAGE_BUFFER; i++)
            {
                if ((i < range.first - PAGE_BUFFER || i > range.last + PAGE_BUFFER)
                    && i > 0 && i <= pageCount)
                    this.recyclePagesQueue.Enqueue(i);
            }
            // Update visible range
            this.currentRange = range;
            // Load visible pages
            for (int i = range.first; i <= range.last; i++)
            {
                await LoadPage(i);
                LoadInkCanvas(i);
            }
            // Load buffer pages
            for (int i = range.first - PAGE_BUFFER; i <= range.last + PAGE_BUFFER; i++)
            {
                await LoadPage(i);
            }
        }

        private void RemovePage(int pageNumber)
        {
            if (pageNumber < currentRange.first - PAGE_BUFFER || pageNumber > currentRange.last + PAGE_BUFFER)
            {
                // Remove Image
                Image image = (Image)this.FindName(PREFIX_PAGE + pageNumber.ToString());
                if (image != null)
                {
                    double x = image.ActualHeight;
                    image.Source = null;
                    image.Height = x;
                }
                // Remove Ink Canvas
                SaveInkCanvas(pageNumber, true);
            }
        }

        private void SaveInkCanvas(int pageNumber, bool removeAfterSave = false)
        {
            Grid grid = (Grid)this.imagePanel.Children[pageNumber - 1];
            if (grid.Children.Count > 1)
            {
                // Save ink strokes, if there is any
                InkCanvas inkCanvas = (InkCanvas)grid.Children[1];
                if (inkCanvas.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
                {
                    // Remove old item in dictionary
                    this.inkStrokeDictionary.Remove(pageNumber);
                    // Add to dictionary
                    this.inkStrokeDictionary.Add(pageNumber, inkCanvas.InkPresenter.StrokeContainer);
                }
                // Remove ink canvas
                if (removeAfterSave)
                {
                    grid.Children.RemoveAt(1);
                    this.inkCanvasList.Remove(pageNumber);
                }
            }
            else // Something is wrong
            { }
        }

        private async Task LoadPage(int pageNumber, uint renderWidth = 1500)
        {
            // Render pdf image
            Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
            if (image == null) return;
            // Check if page is already loaded
            if (image.Source == null)
            {
                // Render page
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

        private void LoadInkCanvas(int pageNumber)
        {
            Grid grid = (Grid)this.imagePanel.Children[pageNumber - 1];
            if (grid == null) return;
            // Load ink canvas
            InkCanvas inkCanvas = (InkCanvas)grid.FindName(PREFIX_CANVAS + pageNumber.ToString());
            // Check if an ink canvas exist
            if (inkCanvas == null)
            {
                // Add ink canvas
                inkCanvas = new InkCanvas();
                inkCanvas.Name = PREFIX_CANVAS + pageNumber.ToString();
                inkCanvas.InkPresenter.InputDeviceTypes = drawingDevice;
                inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
                grid.Children.Add(inkCanvas);
                this.inkCanvasList.Add(pageNumber);
                // Load inking if exist
                InkStrokeContainer inkStrokeContainer;
                if (inkStrokeDictionary.TryGetValue(pageNumber, out inkStrokeContainer))
                    inkCanvas.InkPresenter.StrokeContainer = inkStrokeContainer;
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
            return IsUserVisible((Image)imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString()), this.scrollViewer);
        }

        private void RefreshViewer()
        {
            int p = FindVisiblePage();
            if (p > 0)
                PreparePages(FindVisibleRange(p));
        }

        private int FindVisiblePage()
        {
            // Find a page that is currently visible
            // Check current page range
            for (int i = currentRange.first; i <= currentRange.last; i++)
            {
                if (IsPageVisible(i)) return i;
            }
            // Find out which page is visible
            int p;
            if (imagePanel.Orientation == Orientation.Vertical)
                p = (int)Math.Ceiling(scrollViewer.VerticalOffset / scrollViewer.ScrollableHeight * pageCount);
            else
                p = (int)Math.Ceiling(scrollViewer.HorizontalOffset / scrollViewer.ScrollableWidth * pageCount);
            if (p < 0) p = 1;
            for (int i = 0; i <= pageCount; i++)
            {
                if (IsPageVisible(p + i))
                    return p + i;
                else if (IsPageVisible(p - i))
                    return p - i;
            }
            return 0;
        }

        private PageRange FindVisibleRange(int visiblePage)
        {
            // Given a visible page,
            // Find the pages that are currently visible
            PageRange range = new PageRange(visiblePage, visiblePage);
            // Find the first visible page
            for (int i = 1; i <= pageCount; i++)
            {
                if (!IsPageVisible(visiblePage - i))
                {
                    range.first = visiblePage - i + 1;
                    break;
                }
            }
            // Find the last visible page
            for (int i = 1; i <= pageCount; i++)
            {
                if (!IsPageVisible(visiblePage + i))
                {
                    range.last = visiblePage + i - 1;
                    break;
                }
            }
            return range;
        }

        private void SetZoomFactor()
        {
            double hZoomFactor = (this.scrollViewer.ActualHeight 
                - 2 * (PAGE_IMAGE_MARGIN + imagePanel.Margin.Top)) / pageHeight;
            double wZoomFactor = (this.scrollViewer.ActualWidth 
                - 2 * (PAGE_IMAGE_MARGIN + imagePanel.Margin.Left)) / pageWidth;
            if (hZoomFactor < 0.1) hZoomFactor = 0.1;
            if (wZoomFactor < 0.1) wZoomFactor = 0.1;
            this.scrollViewer.MinZoomFactor = (float)Math.Min(hZoomFactor, wZoomFactor);
        }

        private void RefreshTimer_Tick(object sender, object e)
        {
            refreshTimer.Stop();
            this.RefreshViewer();
        }

        private void InitializationTimer_Tick(object sender, object e)
        {
            int count = imagePanel.Children.Count;
            for (int i = count + 1; i <= Math.Min(count + BLANK_PAGE_BATCH, pageCount); i++)
            {
                Grid grid = new Grid();
                grid.Name = PREFIX_GRID + i.ToString();
                grid.Margin = pageMargin;
                Image image = new Image();
                image.Name = PREFIX_PAGE + i.ToString();
                image.Width = pageWidth;
                image.Height = pageHeight;
                grid.Children.Add(image);
                this.imagePanel.Children.Add(grid);
            }
            this.fullScreenMessage.Text = "Loading... " + (this.imagePanel.Children.Count * 100 / this.pageCount).ToString() + "%";
            if (imagePanel.Children.Count >= pageCount)
            {
                this.initializationTimer.Stop();
                FinishInitialization();
            }
        }

        private void RecycleTimer_Tick(object sender, object e)
        {
            while (this.recyclePagesQueue.Count > RECYCLE_QUEUE_SIZE)
            {
                RemovePage(this.recyclePagesQueue.Dequeue());
            }
        }

        private void scrollViewer_ViewChanging(object sender, ScrollViewerViewChangingEventArgs e)
        {

        }

        private void scrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (viewInitialized && !e.IsIntermediate)
            {
                refreshTimer.Stop();
                refreshTimer.Start();
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
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

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            //
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //
        }
    }
}