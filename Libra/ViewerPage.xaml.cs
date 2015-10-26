using System;
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
using Windows.UI.Input.Inking;
using System.IO;
using System.IO.Compression;
using Windows.UI.Popups;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Libra
{
    /// <summary>
    /// 
    /// </summary>
    public sealed partial class ViewerPage : Page
    {
        private const int SCROLLBAR_WIDTH = 10;
        private const int PAGE_IMAGE_MARGIN = 10;
        private const int PAGE_BUFFER = 5;
        private const int RECYCLE_QUEUE_SIZE = 10;
        private const int BLANK_PAGE_BATCH = 100;
        private const int FIRST_LOAD_PAGES = 2;
        private const int REFRESH_TIMER_TICKS = 50 * 10000;
        private const int INITIALIZATION_TIMER_TICKS = 10 * 10000;
        private const int RECYCLE_TIMER_SECOND = 1;
        private const int SIZE_PENTIP = 1;
        private const int SIZE_HIGHLIGHTER = 10;

        private const string PREFIX_PAGE = "page";
        private const string PREFIX_GRID = "grid";
        private const string PREFIX_CANVAS = "canvas";
        private const string EXT_INKING = ".gif";
        private const string EXT_ARCHIVE = ".zip";

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

        private System.Diagnostics.Stopwatch fileLoadingWatch;
        private System.Diagnostics.Stopwatch fileSavingWatch;

        private int pageCount;
        private double pageHeight;
        private double pageWidth;
        private bool fileLoaded;
        private int penSize;
        private int highlighterSize;
        private string futureAccessToken;

        public ViewerPage()
        {
            this.InitializeComponent();
            this.pageMargin = new Thickness(PAGE_IMAGE_MARGIN);
            this.fileLoaded = false;

            this.refreshTimer = new DispatcherTimer();
            this.refreshTimer.Tick += RefreshTimer_Tick;
            this.refreshTimer.Interval = new TimeSpan(REFRESH_TIMER_TICKS);

            this.initializationTimer = new DispatcherTimer();
            this.initializationTimer.Tick += InitializationTimer_Tick;
            this.initializationTimer.Interval = new TimeSpan(INITIALIZATION_TIMER_TICKS);

            this.recycleTimer = new DispatcherTimer();
            this.recycleTimer.Tick += RecycleTimer_Tick;
            this.recycleTimer.Interval = new TimeSpan(0, 0, RECYCLE_TIMER_SECOND);

            // Inking preference
            penSize = SIZE_PENTIP;
            highlighterSize = SIZE_HIGHLIGHTER;
            drawingAttributes = new InkDrawingAttributes();
            drawingAttributes.Color = Windows.UI.Colors.Red;
            drawingAttributes.Size = new Size(penSize, penSize);
            // Fixed inking preference
            drawingAttributes.IgnorePressure = false;
            drawingAttributes.FitToCurve = true;
            drawingDevice = Windows.UI.Core.CoreInputDeviceTypes.Mouse | Windows.UI.Core.CoreInputDeviceTypes.Pen;

            AppEventSource.Log.Debug("ViewerPage: Page initialized.");
        }

        private void InitializeViewer()
        {
            this.imagePanel.Children.Clear();
            this.currentRange = new PageRange();
            this.pageHeight = 0;
            this.pageWidth = 0;
            this.pageCount = 0;
            this.inkStrokeDictionary = new Dictionary<int, InkStrokeContainer>();
            this.inkCanvasList = new List<int>();
            this.recyclePagesQueue = new Queue<int>();

            this.recycleTimer.Stop();

            AppEventSource.Log.Debug("ViewerPage: Viewer panel and settings initialized.");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            AppEventSource.Log.Debug("ViewerPage: Navigated to ViewerPage.");
            RestoreDrawingPreference();
            if (e.Parameter != null)
            {
                StorageFile argumentFile = e.Parameter as StorageFile;
                if (this.pdfFile != null && this.pdfFile != argumentFile)
                {
                    // Another file already opened
                    AppEventSource.Log.Debug("ViewerPage: Another file is already opened: " + this.pdfFile.Name);
                    this.fileLoaded = false;
                }
                if (!this.fileLoaded)
                {
                    InitializeViewer();
                    this.pdfFile = argumentFile;
                    LoadFile(this.pdfFile);
                }
            }
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            this.fileSavingWatch = new System.Diagnostics.Stopwatch();
            this.fileSavingWatch.Start();
            SaveDrawingPreference();
            if (this.fileLoaded)
            {
                // Save the viewer state to suspension manager
                SuspensionManager.LastViewerState = SaveViewerState();
                AppEventSource.Log.Info("ViewerPage: Saved viewer state to suspension manager. " + this.pdfFile.Name);
                // Save inking to file
                await SaveInking();
            }
            this.fileSavingWatch.Stop();
            AppEventSource.Log.Info("ViewerPage: Finished saving process in " + fileSavingWatch.Elapsed.TotalSeconds.ToString() + " seconds.");
        }

        private ViewerState SaveViewerState()
        {
            AppEventSource.Log.Debug("ViewerPage: Saving viewer state.");
            ViewerState viewerState = new ViewerState(this.futureAccessToken);
            viewerState.hOffset = this.scrollViewer.HorizontalOffset;
            viewerState.vOffset = this.scrollViewer.VerticalOffset;
            viewerState.hScrollableOffset = this.scrollViewer.ScrollableHeight;
            viewerState.vScrollableOffset = this.scrollViewer.ScrollableWidth;
            viewerState.zFactor = this.scrollViewer.ZoomFactor;
            return viewerState;
        }

        private void RestoreViewerState(ViewerState viewerState)
        {
            AppEventSource.Log.Debug("ViewerPage: Checking previously saved viewer state.");
            // Check if the viewer state exist
            if (viewerState == null)
            {
                AppEventSource.Log.Debug("ViewerPage: No viewer state saved.");
                return;
            }
            // Check if the viewer state is for this file
            if (viewerState.pdfToken != this.futureAccessToken)
            {
                AppEventSource.Log.Debug("ViewerPage: Viewer state is not for this file. Restoration cancelled.");
                return;
            }
            if (viewerState.IsRestoring)
            {
                AppEventSource.Log.Debug("ViewerPage: Restoring previously saved viewer state.");
                // TODO: RestoreViewer
                //
                //
                AppEventSource.Log.Info("ViewerPage: Viewer state restored. " + this.pdfFile.Name);
                viewerState.IsRestoring = false;
                SuspensionManager.LastViewerState = null;
            }
           
        }

        private async Task SaveInking()
        {
            // Save ink canvas
            foreach (int pageNumber in this.inkCanvasList)
            {
                SaveInkCanvas(pageNumber);
            }
            AppEventSource.Log.Debug("ViewerPage: Saving inking. " + this.pdfFile.Name);
            // Save ink strokes
            if (this.inkStrokeDictionary.Count == 0)
            {
                AppEventSource.Log.Debug("ViewerPage: No inking recorded.");
                return;
            }
            MemoryStream inkData = new MemoryStream();
            using (ZipArchive archive = new ZipArchive(inkData, ZipArchiveMode.Create, true))
            {
                foreach (KeyValuePair<int, InkStrokeContainer> entry in inkStrokeDictionary)
                {
                    ZipArchiveEntry inkFile = archive.CreateEntry(entry.Key.ToString() + EXT_INKING);
                    using (var entryStream = inkFile.Open().AsOutputStream())
                        await entry.Value.SaveAsync(entryStream);
                    AppEventSource.Log.Debug("ViewerPage: Inking for page " + entry.Key.ToString() + " saved.");
                }
            }
            AppEventSource.Log.Debug("ViewerPage: Inking saved to memory.");
            string inkStrokeFilename = this.futureAccessToken + EXT_ARCHIVE;
            StorageFile inkArchiveFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(inkStrokeFilename, CreationCollisionOption.ReplaceExisting);
            using (Stream inkArchiveStream = await inkArchiveFile.OpenStreamForWriteAsync())
            {
                inkData.Seek(0, SeekOrigin.Begin);
                await inkData.CopyToAsync(inkArchiveStream);
            }
            AppEventSource.Log.Info("ViewerPage: Inking for " + this.pdfFile.Name + " saved to " + this.futureAccessToken.Substring(0,8));
        }

        private async Task RestoreInking()
        {
            // Restore ink strokes
            AppEventSource.Log.Debug("ViewerPage: Checking inking for " + this.pdfFile.Name);
            string inkStrokeFilename = this.futureAccessToken + EXT_ARCHIVE;
            try
            {
                StorageFile inkArchiveFile = await ApplicationData.Current.LocalFolder.GetFileAsync(inkStrokeFilename);
                AppEventSource.Log.Info("ViewerPage: Restoring inking from file for " + this.pdfFile.Name);
                this.inkStrokeDictionary = new Dictionary<int, InkStrokeContainer>();
                MemoryStream inkData;
                using (Stream inkArchiveStream = await inkArchiveFile.OpenStreamForReadAsync())
                {
                    inkData = new MemoryStream((int)inkArchiveStream.Length);
                    inkArchiveStream.Seek(0, SeekOrigin.Begin);
                    await inkArchiveStream.CopyToAsync(inkData);
                }
                AppEventSource.Log.Debug("ViewerPage: Inking loaded to memory.");
                ZipArchive archive = new ZipArchive(inkData, ZipArchiveMode.Read);
                foreach (ZipArchiveEntry inkFile in archive.Entries)
                {
                    using (var entryStream = inkFile.Open().AsInputStream())
                    {
                        InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
                        await inkStrokeContainer.LoadAsync(entryStream);
                        int pageNumber = Convert.ToInt32(inkFile.Name.Substring(0, inkFile.Name.Length - 4));
                        this.inkStrokeDictionary.Add(pageNumber, inkStrokeContainer);
                        AppEventSource.Log.Debug("ViewerPage: Inking for page " + pageNumber.ToString() + " restored.");
                    }
                }
                AppEventSource.Log.Info("ViewerPage: Inking restored.");
            }
            catch (Exception e)
            {
                if (e is FileNotFoundException)
                {
                    AppEventSource.Log.Info("ViewerPage: No inking file found.");
                    return;
                }
                else
                {
                    AppEventSource.Log.Error("ViewerPage: Error when loading inking for " + this.pdfFile.Name + " Exception: " + e.ToString());
                    // Notify user
                    MessageDialog messageDialog = new MessageDialog("Error when loading inking: \n" + e.ToString());
                    var handler = new UICommandInvokedHandler(RestoreInkingCommandHandler);
                    messageDialog.Commands.Add(new UICommand("Delete inking", handler, 0));
                    messageDialog.Commands.Add(new UICommand("Ignore", handler, 1));
                    await messageDialog.ShowAsync();
                }
            }
        }

        private async void RestoreInkingCommandHandler(IUICommand command)
        {
            switch ((int)command.Id)
            {
                case 0:
                    // Delete file
                    string inkStrokeFilename = this.futureAccessToken + EXT_ARCHIVE;
                    StorageFile inkArchiveFile = await ApplicationData.Current.LocalFolder.GetFileAsync(inkStrokeFilename);
                    await inkArchiveFile.DeleteAsync();
                    AppEventSource.Log.Info("ViewerPage: Inking file for " + this.pdfFile.Name + " deleted.");
                    break;
                default:
                    break;
            }
        }

        private void SaveDrawingPreference()
        {
            // TODO
            //AppEventSource.Log.Debug("ViewerPage: Inking preference saved.");
        }

        private void RestoreDrawingPreference()
        {
            Pencil_Click(null, null);
            //AppEventSource.Log.Debug("ViewerPage: Inking preference restored.");
        }

        private async void LoadFile(StorageFile pdfFile)
        {
            if (this.pageCount > 0 && this.imagePanel.Children.Count >= this.pageCount)
            {
                AppEventSource.Log.Warn("ViewerPage: Viewer not initialized correctly.");
                return;
            }
            AppEventSource.Log.Info("ViewerPage: Loading the new file: " + this.pdfFile.Name);
            // Add file the future access list
            this.futureAccessToken = Windows.Storage.AccessCache.StorageApplicationPermissions.FutureAccessList.Add(pdfFile);
            // Load Pdf file
            IAsyncOperation<PdfDocument> getPdfTask = PdfDocument.LoadFromFileAsync(pdfFile);
            // Display loading
            this.fullScreenCover.Visibility = Visibility.Visible;
            this.fullScreenMessage.Text = "Loading...";
            // Wait until the file is loaded
            this.pdfDocument = await getPdfTask;

            this.pageCount = (int)pdfDocument.PageCount;
            AppEventSource.Log.Debug("ViewerPage: Total pages: " + this.pageCount.ToString());
            this.pageWidth = scrollViewer.ActualWidth - 2 * PAGE_IMAGE_MARGIN - SCROLLBAR_WIDTH;
            AppEventSource.Log.Debug("ViewerPage: Page width set to " + this.pageWidth.ToString());

            this.fileLoadingWatch = new System.Diagnostics.Stopwatch();
            this.fileLoadingWatch.Start();

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
            AppEventSource.Log.Debug("ViewerPage: First " + Math.Min(FIRST_LOAD_PAGES, pageCount).ToString() + " pages rendered.");
            // Update layout to force the calculation of actual height and width of the image
            // otherwise, the following code may be executed BEFORE the stack panel update layout.
            this.imagePanel.UpdateLayout();
            // Use the height of the second page to initialize the rest of the document
            // This may cause some problem... will fix this later
            if (pageCount > 1)
            {
                image = (Image)this.FindName(PREFIX_PAGE + "2");
                this.pageHeight = image.ActualHeight;
                AppEventSource.Log.Debug("ViewerPage: Page height set to " + this.pageHeight.ToString());
            }
            // Add blank pages for the rest of the file using the initialization timer
            this.initializationTimer.Start(); 
        }

        private async void FinishInitialization()
        {
            this.imagePanel.UpdateLayout();
            // Calculate the minimum zoom factor
            SetZoomFactor();
            this.fullScreenCover.Visibility = Visibility.Collapsed;
            this.fileLoaded = true;
            // Restore view
            RestoreViewerState(SuspensionManager.LastViewerState);
            // Retore inking
            await RestoreInking();
            RefreshViewer();
            this.fileLoadingWatch.Stop();
            AppEventSource.Log.Info("ViewerPage: Finished Preparing the file in " + fileLoadingWatch.Elapsed.TotalSeconds.ToString());
            this.recycleTimer.Start();
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
                    AppEventSource.Log.Debug("ViewerPage: Image in page " + pageNumber.ToString() + " removed.");
                }
                else AppEventSource.Log.Warn("ViewerPage: Image in page " + pageNumber.ToString() + " is empty.");
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
                    AppEventSource.Log.Debug("ViewerPage: Ink strokes for page " + pageNumber.ToString() + " saved to dictionary.");
                }
                // Remove ink canvas
                if (removeAfterSave)
                {
                    grid.Children.RemoveAt(1);
                    this.inkCanvasList.Remove(pageNumber);
                }
            }
            else // Something is wrong
            {
                AppEventSource.Log.Warn("ViewerPage: Page " + pageNumber.ToString() + " does not have an ink canvas.");
            }
        }

        private async Task LoadPage(int pageNumber, uint renderWidth = 1500)
        {
            // Render pdf image
            Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
            if (image == null)
            {
                AppEventSource.Log.Warn("ViewerPage: Image container for page " + pageNumber.ToString() + " not found.");
                return;
            }
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
                AppEventSource.Log.Debug("ViewerPage: Page " + pageNumber.ToString() + " loaded with render width " + renderWidth.ToString());
            }
        }

        private void LoadInkCanvas(int pageNumber)
        {
            Grid grid = (Grid)this.imagePanel.Children[pageNumber - 1];
            if (grid == null)
            {
                AppEventSource.Log.Warn("ViewerPage: Grid container for page " + pageNumber.ToString() + " not found.");
                return;
            }
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
                {
                    inkCanvas.InkPresenter.StrokeContainer = inkStrokeContainer;
                    AppEventSource.Log.Debug("ViewerPage: Ink strokes for page " + pageNumber.ToString() + " loaded from dictionary");
                }
            }
        }

        private void UpdateDrawingAttributes()
        {
            if (this.inkCanvasList == null)
            {
                AppEventSource.Log.Debug("ViewerPage: Updating drawing attributes: No ink canvas found.");
                return;
            }
            foreach (int pageNumber in inkCanvasList)
            {
                InkCanvas inkCanvas = (InkCanvas)this.imagePanel.FindName(PREFIX_CANVAS + pageNumber.ToString());
                inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
            }
            AppEventSource.Log.Debug("ViewerPage: Drawing attributes updated.");
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
            else AppEventSource.Log.Warn("ViewerPage: Visible page is incorrect. p = " + p.ToString());
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
            if (hZoomFactor < 0.1)
            {
                hZoomFactor = 0.1;
                AppEventSource.Log.Debug("ViewerPage: Minimum vertical zoom factor is too small");
            }
            if (wZoomFactor < 0.1)
            {
                wZoomFactor = 0.1;
                AppEventSource.Log.Debug("ViewerPage: Minimum horizontal zoom factor is too small");
            }
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
                AppEventSource.Log.Debug("ViewerPage: Blank images add for all pages. Count: " + imagePanel.Children.Count.ToString());
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
            if (fileLoaded && !e.IsIntermediate)
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
                AppEventSource.Log.Debug("ViewerPage: Window size changed, offsets recalculated.");
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

        private void ClearInputTypeToggleBtn()
        {
            this.Pencil.IsChecked = false;
            this.Highlighter.IsChecked = false;
            this.Eraser.IsChecked = false;
        }

        private void Pencil_Click(object sender, RoutedEventArgs e)
        {
            ClearInputTypeToggleBtn();
            AppEventSource.Log.Debug("ViewerPage: Pencil selected");
            this.Pencil.IsChecked = true;
            this.drawingAttributes.Size = new Size(penSize, penSize);
            this.drawingAttributes.PenTip = PenTipShape.Circle;
            this.drawingAttributes.DrawAsHighlighter = false;
            this.drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.Identity;
            UpdateDrawingAttributes();
        }

        private void Highlighter_Click(object sender, RoutedEventArgs e)
        {
            ClearInputTypeToggleBtn();
            AppEventSource.Log.Debug("ViewerPage: Highlighter selected");
            this.Highlighter.IsChecked = true;
            this.drawingAttributes.Size = new Size(penSize, highlighterSize);
            this.drawingAttributes.PenTip = PenTipShape.Rectangle;
            this.drawingAttributes.DrawAsHighlighter = true;
            this.drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.Identity;
            UpdateDrawingAttributes();
        }

        private void Eraser_Click(object sender, RoutedEventArgs e)
        {
            ClearInputTypeToggleBtn();
            AppEventSource.Log.Debug("ViewerPage: Eraser selected");
            this.Eraser.IsChecked = true;
        }
    }
}
