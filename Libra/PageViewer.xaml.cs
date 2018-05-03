using Libra.Class;
using Libra.Dialog;
using NavigationMenu;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using Windows.UI;
using Windows.UI.Input.Inking;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Libra
{
    /// <summary>
    /// Implement the pdf viewer using a scroll viewer.
    /// </summary>
    public sealed partial class ViewerPage : Page
    {
        private const int SCROLLBAR_WIDTH = 15;
        private const int PAGE_IMAGE_MARGIN = 10;
        private const int NAVIGATION_WIDTH = 48;
        private const int SIZE_PAGE_BUFFER = 3;
        private const int SIZE_RECYCLE_QUEUE = 10;
        private const int SIZE_PAGE_BATCH = 150;
        private const int REFRESH_TIMER_TICKS = 50 * 10000;
        private const int INITIALIZATION_TIMER_TICKS = 10 * 10000;
        private const int RECYCLE_TIMER_SECOND = 1;
        private const int PAGE_NUMBER_TIMER_SECOND = 2;
        private const int MIN_RENDER_WIDTH = 500;
        private const int MIN_WIDTH_TO_SHOW_FILENAME = 850;
        private const int MIN_WIDTH_TO_SHOW_ORIENTATION_BTN = 650;
        private const int MIN_WIDTH_TO_SHOW_CLOSE_BTN = 750;
        private const int MIN_WIDTH_TO_SHOW_PAN_BTN = 700;
        private const double INFO_GRID_OPACITY = 0.75;
        private const double ZOOM_STEP_SIZE = 0.25;

        private const string PREFIX_PAGE = "page";
        private const string PREFIX_GRID = "grid";
        private const string PREFIX_CANVAS = "canvas";
        private const string EXT_VIEW = ".xml";
        private const string INKING_PREFERENCE_FILENAME = "_inkingPreference.xml";
        private const string PAGE_SIZE_FILENAME = "_pageSize.xml";
        private const string DEFAULT_FULL_SCREEN_MSG = "No File is Opened.";

        private PdfModel pdfModel;
        private StorageFile pdfStorageFile;
        private StorageFolder dataFolder;
        private Thickness pageMargin;
        private PageRange inkingPageRange;
        private PageRange _visiblePageRange;
        public PageRange VisiblePageRange { get { return this._visiblePageRange; } }
        private DispatcherTimer refreshTimer;
        private DispatcherTimer initializationTimer;
        private DispatcherTimer recycleTimer;
        private DispatcherTimer pageNumberTextTimer;
        private List<int> renderedPages;
        private Queue<int> recyclePagesQueue;
        private Queue<int> renderPagesQueue;
        private List<int> inkCanvasList;                // Active inkcanvas
        private Stack<InkCanvas> inkCanvasStack;        // Inactive inkcanvas
        private InkDrawingAttributes drawingAttributes;
        private InkingPreference inkingPreference;
        private InkingManager inkManager;
        private InkInputProcessingMode inkProcessMode;
        
        private Guid _viewerKey;
        public Guid ViewerKey { get { return this._viewerKey; } }

        private int pageCount;
        private bool fileLoaded;
        private bool isRenderingPage;
        private string futureAccessToken;

        public static ViewerPage Current = null;

        // For diagnosis only
        private System.Diagnostics.Stopwatch fileLoadingWatch;

        public ViewerPage()
        {

            this.InitializeComponent();

            this.inkCanvasStack = new Stack<InkCanvas>();

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

            this.pageNumberTextTimer = new DispatcherTimer();
            this.pageNumberTextTimer.Tick += pageNumberTextTimer_Tick;
            this.pageNumberTextTimer.Interval = new TimeSpan(0, 0, PAGE_NUMBER_TIMER_SECOND);

            this.Loaded += (sender, args) =>
            {
                Current = this;
            };

            InitializeZoomInView();
            AppEventSource.Log.Debug("ViewerPage: Page initialized.");
        }

        private void InitializeZoomInView()
        {
            this.imagePanel.Children.Clear();
            this.imagePanel.UpdateLayout();

            this.inkingPageRange = new PageRange();
            this._visiblePageRange = new PageRange();
            this._viewerKey = new Guid();
            this.pageCount = 0;
            this.inkCanvasList = new List<int>();
            this.renderedPages = new List<int>();
            this.recyclePagesQueue = new Queue<int>();
            this.renderPagesQueue = new Queue<int>();
            this.isRenderingPage = false;
            this.inkProcessMode = InkInputProcessingMode.Inking;
            this.recycleTimer.Stop();
            this.fullScreenCover.Visibility = Visibility.Visible;
            this.fullScreenMessage.Text = DEFAULT_FULL_SCREEN_MSG;
            this.pageNumberTextBlock.Text = "";
            this.filenameTextBlock.Text = "";
            this.imagePanel.Orientation = Orientation.Vertical;
            this.semanticZoom.IsZoomedInViewActive = true;
            ClearViewModeToggleBtn();
            this.VerticalViewBtn.IsChecked = true;
            this.pageThumbnails = null;
            if (NavigationPage.Current != null) NavigationPage.Current.InitializeViewBtn();
            AppEventSource.Log.Debug("ViewerPage: Viewer panel and settings initialized.");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Set viewer mode
            if (SuspensionManager.AppSessionState != null)
                SuspensionManager.AppSessionState.ViewerMode = 1;
            // Check if a new file is opened
            if (this.fileLoaded && !this.pdfStorageFile.IsEqual(SuspensionManager.pdfFile))
            {
                // Another file already opened.
                AppEventSource.Log.Debug("ViewerPage: Another file is already opened: " + this.pdfStorageFile.Name);
                this.fileLoaded = false;
            }
            if (!this.fileLoaded)
            {
                // Load a new file
                InitializeZoomInView();
                this.pdfStorageFile = SuspensionManager.pdfFile;
                LoadFile(this.pdfStorageFile);
            }
            else
            {
                // File already loaded, restore viewer state
                if (e.Parameter != null) RestoreViewerState((Guid)e.Parameter);
                else RestoreViewerState();
            }
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            if (this.fileLoaded)
            {
                // Save viewer state to suspension manager.
                if (SuspensionManager.ViewerStateDictionary.ContainsKey(this.ViewerKey))
                {
                    SuspensionManager.ViewerStateDictionary[this.ViewerKey] = ViewerState.SaveViewerState(futureAccessToken, scrollViewer, imagePanel, VisiblePageRange);
                    AppEventSource.Log.Debug("ViewerPage: Saved viewer state to suspension manager.");
                }
                // Save the viewer state to file and update navigation button label if the App is not suspending.
                // Viewer state will be saved by the suspension manager if the App is suspending.
                if (!SuspensionManager.IsSuspending)
                {
                    NavigationPage.Current.UpdateViewBtn(this.ViewerKey, this.VisiblePageRange.ToString());
                    await SuspensionManager.SaveViewerAsync();
                }
            }
        }

        /// <summary>
        /// Restore a saved view of a file. 
        /// </summary>
        /// <param name="key">The key to identify which view to be restored, if there are more than one save views.
        /// the latest view will be restored if key is not specified. </param>
        private void RestoreViewerState(Guid key = new Guid())
        {
            // Switch to zoomed in view
            this.semanticZoom.IsZoomedInViewActive = true;
            if (key == Guid.Empty)
            {
                // Check which one is the latest view
                foreach (KeyValuePair<Guid, ViewerState> entry in SuspensionManager.ViewerStateDictionary)
                {
                    if (key == Guid.Empty)
                        key = entry.Key;
                    // Skip comparison if state is null
                    if (entry.Value == null) continue;
                    if (entry.Value.lastViewed > SuspensionManager.ViewerStateDictionary[key].lastViewed)
                        key = entry.Key;
                }
            }
            this._viewerKey = key;

            NavigationPage.Current.UpdateViewBtn(this.ViewerKey);

            ViewerState viewerState;
            // Assign null state if key is not found.
            if (!SuspensionManager.ViewerStateDictionary.TryGetValue(this.ViewerKey, out viewerState))
                viewerState = null;
            if (viewerState != null)
            {
                // Check if the viewer state is for this file
                if (viewerState.version != ViewerState.CURRENT_VIEWER_STATE_VERSION)
                {
                    AppEventSource.Log.Warn("ViewerPage: Saved viewer state is not for the current App version.");
                }
                else if (viewerState.pdfToken != this.futureAccessToken)
                {
                    AppEventSource.Log.Warn("ViewerPage: Token in the saved viewer state does not match the current file token.");
                }
                else
                {
                    AppEventSource.Log.Debug("ViewerPage: Restoring previously saved viewer state.");
                    // Panel orientation
                    ClearViewModeToggleBtn();
                    if (viewerState.isHorizontalView == true)
                    {
                        this.imagePanel.Orientation = Orientation.Horizontal;
                        this.HorizontalViewBtn.IsChecked = true;
                    }
                    else
                    {
                        this.imagePanel.Orientation = Orientation.Vertical;
                        this.VerticalViewBtn.IsChecked = true;
                    }
                    this.imagePanel.UpdateLayout();
                    // Restore the previous view
                    this.scrollViewer.ChangeView(viewerState.hOffset, viewerState.vOffset, viewerState.zFactor);
                    AppEventSource.Log.Info("ViewerPage: Viewer state restored. " + this.pdfStorageFile.Name);
                    return;
                }
            }
            // Reset the scroll viewer if failed to restore viewer state.
            ResetViewer();
        }

        /// <summary>
        /// Reset the scroll viewer to vertical orientation and display the beginning of the file.
        /// The first page will be zoomed to fit the App window width.
        /// </summary>
        /// <returns></returns>
        private bool ResetViewer()
        {
            this.imagePanel.Orientation = Orientation.Vertical;
            this.imagePanel.UpdateLayout();
            // Zoom the first page to fit the viewer window width
            float zoomFactor = (float)(this.scrollViewer.ActualWidth
                / (this.pdfModel.PageSize(1).Width + 2 * PAGE_IMAGE_MARGIN + SCROLLBAR_WIDTH));
            zoomFactor = CheckZoomFactor(zoomFactor);
            double hOffset = this.imagePanel.ActualWidth * zoomFactor - this.scrollViewer.ActualWidth;
            hOffset = hOffset > 0 ? hOffset / 2 : 0;
            AppEventSource.Log.Debug("ViewerPage: Zoom Factor Reset to " + zoomFactor.ToString());
            return this.scrollViewer.ChangeView(hOffset, 0, zoomFactor);
        }

        /// <summary>
        /// Make sure the zoom factor of scroll viewer is in range.
        /// </summary>
        /// <param name="zoomFactor"></param>
        /// <returns></returns>
        private float CheckZoomFactor(float zoomFactor)
        {
            zoomFactor = zoomFactor > this.scrollViewer.MaxZoomFactor ? this.scrollViewer.MaxZoomFactor : zoomFactor;
            zoomFactor = zoomFactor < this.scrollViewer.MinZoomFactor ? this.scrollViewer.MinZoomFactor : zoomFactor;
            return zoomFactor;
        }

        /// <summary>
        /// Call LoadViewerStateAsync() method in suspension manager to load viewer state from file.
        /// This method also create a new viewer state if none is loaded.
        /// </summary>
        /// <returns></returns>
        private async Task LoadViewerState()
        {
            await SuspensionManager.LoadViewerAsync();
            // Create a new viewer state dictionary if none is loaded
            if (SuspensionManager.ViewerStateDictionary == null || SuspensionManager.ViewerStateDictionary.Count == 0)
            {
                SuspensionManager.ViewerStateDictionary = new Dictionary<Guid, ViewerState>();
                SuspensionManager.ViewerStateDictionary.Add(Guid.NewGuid(), null);
            }
            // Create navigation buttons for the views
            NavigationPage.Current.InitializeViewBtn();
        }

        /// <summary>
        /// Save inking preference to file.
        /// </summary>
        /// <returns></returns>
        private async Task SaveDrawingPreference()
        {
            AppEventSource.Log.Debug("ViewerPage: Saving drawing preference...");
            try
            {
                StorageFile file = await
                    ApplicationData.Current.LocalFolder.CreateFileAsync(INKING_PREFERENCE_FILENAME, CreationCollisionOption.ReplaceExisting);
                await SuspensionManager.SerializeToFileAsync(this.inkingPreference, typeof(InkingPreference), file);
            }
            catch (Exception ex)
            {
                App.NotifyUser(typeof(ViewerPage), "An Error occurred when saving inking preference.\n" + ex.Message);
            }
            
        }

        /// <summary>
        /// Load inking preference from file. A new one will be created none is loaded.
        /// This method will also create drawing attributes to be used by ink canvas.
        /// </summary>
        /// <returns></returns>
        private async Task LoadDrawingPreference()
        {
            // Check drawing preference file
            AppEventSource.Log.Debug("ViewerPage: Checking previously saved drawing preference...");
            StorageFile file = await SuspensionManager.GetSavedFileAsync(INKING_PREFERENCE_FILENAME);
            this.inkingPreference = await
                SuspensionManager.DeserializeFromFileAsync(typeof(InkingPreference), file) as InkingPreference;
            // Discard the inking preference if it is not the current version
            if (this.inkingPreference != null && this.inkingPreference.version != InkingPreference.CURRENT_INKING_PREF_VERSION)
                this.inkingPreference = null;
            // Create drawing preference file if one was not loaded.
            if (this.inkingPreference == null)
            {
                AppEventSource.Log.Debug("ViewerPage: No saved drawing preference loaded. Creating a new one...");
                this.inkingPreference = new InkingPreference();
                await SaveDrawingPreference();
            }
            // Drawing preference
            this.drawingAttributes = new InkDrawingAttributes();
            this.drawingAttributes.FitToCurve = true;

            Pencil_Click(null, null);
        }

        private async void LoadFile(StorageFile pdfFile)
        {
            this.fileLoadingWatch = new System.Diagnostics.Stopwatch();
            this.fileLoadingWatch.Start();

            if (this.pageCount > 0 && this.imagePanel.Children.Count >= this.pageCount)
            {
                AppEventSource.Log.Warn("ViewerPage: Viewer not initialized correctly.");
                return;
            }
            AppEventSource.Log.Info("ViewerPage: Loading file: " + this.pdfStorageFile.Name);
            // Update UI and Display loading
            this.fullScreenCover.Visibility = Visibility.Visible;
            this.fullScreenMessage.Text = "Loading...";
            this.filenameTextBlock.Text = this.pdfStorageFile.Name;
            // Add file the future access list
            this.futureAccessToken = StorageApplicationPermissions.FutureAccessList.Add(pdfFile);
            // Save session state in suspension manager
            SuspensionManager.AppSessionState = new SessionState(this.futureAccessToken);
            // Create local data folder, if not exist
            this.dataFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(this.futureAccessToken, CreationCollisionOption.OpenIfExists);
            // Load Pdf file
            this.pdfModel = await PdfModel.LoadFromFile(pdfFile, dataFolder);
            // Notify the user and return to main page if failed to load the file.
            if (this.pdfModel == null)
            {
                // Currently, exceptions and notifications are handled within the 
                //  pdfModel /PdfModelMS/PdfModelSF class
                //App.NotifyUser(typeof(ViewerPage), "Failed to open the file.", true);
                this.CloseAllViews();
                return;
            }
            // Check future access list
            await CheckFutureAccessList();

            AppEventSource.Log.Info("ViewerPage: Finished loading the file in " + fileLoadingWatch.Elapsed.TotalSeconds.ToString());
            // Total number of pages
            this.pageCount = pdfModel.PageCount;
            AppEventSource.Log.Debug("ViewerPage: Total pages: " + this.pageCount.ToString());
            // Zoom the first page to fit the viewer window width
            ResetViewer();
            // Load drawing preference
            await LoadDrawingPreference();
            // Initialize thumbnails collection
            this.pageThumbnails = new PageThumbnailCollection(this.pdfModel);
            // Render the first page
            AddBlankImage(1);
            await AddPageImage(1, (uint)(this.scrollViewer.ActualWidth));
            // Add blank pages for the rest of the file using the initialization timer
            this.initializationTimer.Start();
        }

        private void AddBlankImage(int pageNumber)
        {
            if (pageNumber < 1 || pageNumber > this.pageCount) return;
            // Add blank image
            Grid grid = new Grid();
            grid.Name = PREFIX_GRID + pageNumber.ToString();
            grid.Margin = pageMargin;
            grid.Background = new SolidColorBrush(Colors.White);
            Image image = new Image();
            image.Name = PREFIX_PAGE + pageNumber.ToString();
            image.Width = this.pdfModel.PageSize(pageNumber).Width;
            image.Height = this.pdfModel.PageSize(pageNumber).Height;
            grid.Children.Add(image);
            this.imagePanel.Children.Add(grid);
            // Add blank thumbnail
            this.pageThumbnails.Add(new PageDetail(pageNumber, image.Height, image.Width));
        }

        private async void FinishInitialization()
        {
            this.fullScreenCover.Visibility = Visibility.Collapsed;
            this.imagePanel.UpdateLayout();
            // Load viewer state
            await LoadViewerState();
            RestoreViewerState();
            // Load inking
            inkManager = await InkingManager.InitializeInking(dataFolder, pdfModel);
            // Make sure about the visible page range
            this._visiblePageRange = FindVisibleRange();
            this.fileLoaded = true;
            this.fileLoadingWatch.Stop();
            RefreshViewer();
            AppEventSource.Log.Info("ViewerPage: Finished Preparing the file in " + fileLoadingWatch.Elapsed.TotalSeconds.ToString());
            this.zoomOutGrid.ItemsSource = pageThumbnails;
            this.recycleTimer.Start();
        }

        /// <summary>
        /// This method is not reliable.
        /// This method should only be called either right after getting the future access token, 
        /// or at the end of FinishInitialization() after fileLoaded has been set to true,
        /// depending on the performance and the size of the future access list.
        /// </summary>
        /// <returns></returns>
        private async Task CheckFutureAccessList()
        {
            string oldToken = null;
            AccessListEntryView futureAccessEntries = StorageApplicationPermissions.FutureAccessList.Entries;
            // If no recent file
            if (futureAccessEntries.Count == 0)
                return;
            else
            {
                System.Diagnostics.Stopwatch fileCheckingWatch = new System.Diagnostics.Stopwatch();
                fileCheckingWatch.Start();
                for (int i = 0; i < futureAccessEntries.Count; i++)
                {
                    AccessListEntry entry = futureAccessEntries[i];
                    if (entry.Token == this.futureAccessToken) continue;
                    StorageFile pdfFileInList = null;
                    try
                    {
                        pdfFileInList = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(entry.Token);
                    }
                    catch 
                    {
                        // remove the entry if there is an exception
                        StorageApplicationPermissions.FutureAccessList.Remove(entry.Token);
                    }
                    if (pdfFileInList == null) continue;
                    if (this.pdfStorageFile.IsEqual(pdfFileInList))
                    {
                        oldToken = entry.Token;
                        StorageApplicationPermissions.FutureAccessList.Remove(entry.Token);
                        break;
                    }
                }
                fileCheckingWatch.Stop();
                AppEventSource.Log.Debug("ViewerPage: Went through future access list in " + fileCheckingWatch.Elapsed.TotalSeconds.ToString() + " seconds");
            }
            if (oldToken != null)
            {
                AppEventSource.Log.Info("ViewerPage: File matched existing token in access list. " + oldToken);
                try
                {
                    StorageFolder oldDataFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(oldToken);
                    await oldDataFolder.RenameAsync(this.futureAccessToken, NameCollisionOption.ReplaceExisting);
                    AppEventSource.Log.Info("ViewerPage: Folder " + oldToken + " renamed to " + this.futureAccessToken);
                    if (this.fileLoaded)
                    {
                        this.dataFolder = await ApplicationData.Current.LocalFolder.GetFolderAsync(this.futureAccessToken);
                        this.inkManager = await InkingManager.InitializeInking(dataFolder, pdfModel);
                        RefreshViewer();
                    }
                }
                catch (Exception e)
                {
                    if (e is FileNotFoundException)
                    {
                        AppEventSource.Log.Warn("ViewerPage: Folder " + oldToken + " not found. ");
                    }
                    else throw new Exception(e.Message);
                }
            }
        }

        /// <summary>
        /// Prepare a range of pages to be displayed. Visiable pages and buffer pages (before/after the visible pages)
        /// are added to a queue for rendering. Other pages are added to the recycle queue so that memeory will be release
        /// when the a page is removed later.
        /// </summary>
        /// <param name="range"></param>
        private void PreparePages(PageRange range)
        {
            // Add invisible pages to recycle list
            for (int i = inkingPageRange.first - SIZE_PAGE_BUFFER; i <= inkingPageRange.last + SIZE_PAGE_BUFFER; i++)
            {
                if ((i < range.first - SIZE_PAGE_BUFFER || i > range.last + SIZE_PAGE_BUFFER)
                    && i > 0 && i <= pageCount)
                    this.recyclePagesQueue.Enqueue(i);
            }
            // Update inking range
            this.inkingPageRange = range;
            // Clear render page queue
            this.renderPagesQueue.Clear();
            // Add visible pages to queue
            for (int i = range.first; i <= range.last; i++)
            {
                if (i > 0 && i <= pageCount)
                    renderPagesQueue.Enqueue(i);
            }
            // Add buffer pages to queue
            for (int i = range.first - SIZE_PAGE_BUFFER; i <= range.last + SIZE_PAGE_BUFFER; i++)
            {
                if (i > 0 && i <= pageCount && !renderPagesQueue.Contains(i))
                    renderPagesQueue.Enqueue(i);
            }
            // Start rendering pages
            if (!this.isRenderingPage)
                RenderPages();
        }

        private async void RenderPages()
        {
            await RenderPagesAsync();
        }

        private async Task RenderPagesAsync()
        {
            // Set isRenderingPage to true to prevent this method to be called multiple times before the rendering is finished.
            this.isRenderingPage = true;
            while (renderPagesQueue.Count > 0)
            {
                int pageNumber = renderPagesQueue.Dequeue();
                Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
                // The actual page width that is diaplaying to the user
                double displayWidth = this.pdfModel.PageSize(pageNumber).Width * this.scrollViewer.ZoomFactor;
                // Render width depends on the display width. (higher if a page is zoomed in)
                double standardWidth = this.scrollViewer.ActualWidth;
                uint renderWidth;
                if (displayWidth < 0.3 * standardWidth)
                    renderWidth = (uint)(0.3 * standardWidth);
                else if (displayWidth < 0.6 * standardWidth)
                    renderWidth = (uint)(0.6 * standardWidth);
                else if (displayWidth < 1.1 * standardWidth)
                    renderWidth = (uint)(1.0 * standardWidth);
                else if (displayWidth < 2.1 * standardWidth)
                    renderWidth = (uint)(2.0 * standardWidth);
                else if (displayWidth < 3.1 * standardWidth)
                    renderWidth = (uint)(3.0 * standardWidth);
                else if (displayWidth < 4.1 * standardWidth)
                    renderWidth = (uint)(4.0 * standardWidth);
                else if (displayWidth < 5.1 * standardWidth)
                    renderWidth = (uint)(5.0 * standardWidth);
                else renderWidth = (uint)(6.0 * standardWidth);
                // Load the visible pages with a higher resolution.
                if (pageNumber >= VisiblePageRange.first && pageNumber <= VisiblePageRange.last)
                    await AddPageImage(pageNumber, renderWidth);
                else
                    await AddPageImage(pageNumber, Math.Min(renderWidth, (uint)(standardWidth)));
                // Add ink canvas
                await LoadInkCanvas(pageNumber);
            }
            this.isRenderingPage = false;
        }

        /// <summary>
        /// Calculate the maximum page height and width within the visible pages.
        /// </summary>
        private Size MaxVisiblePageSize()
        {
            double height = 0;
            double width = 0;
            // Calculate FitView height/width based of the actual page height/width of visible pages.
            for (int i = VisiblePageRange.first; i <= VisiblePageRange.last; i++)
            {
                Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + i.ToString());
                if (image == null) continue;
                if (i == VisiblePageRange.first)
                {
                    height = image.ActualHeight;
                    width = image.ActualWidth;
                }
                else
                {
                    if (image.ActualHeight > height) height = image.ActualHeight;
                    if (image.ActualWidth > width) width = image.ActualWidth;
                }
            }
            // Add the page margin and scroll bar width
            height = height + 2 * SCROLLBAR_WIDTH + 2 * PAGE_IMAGE_MARGIN;
            width = width + SCROLLBAR_WIDTH + 2 * PAGE_IMAGE_MARGIN;

            return new Size(width, height);
        }

        /// <summary>
        /// Remove ink canvas and image for a page to release memory. 
        /// Ink strokes will also be saved to inking dictionary before the ink canvas is removed.
        /// </summary>
        /// <param name="pageNumber"></param>
        private void RemovePage(int pageNumber, bool forceRemove = false)
        {
            if (forceRemove ||
                pageNumber < inkingPageRange.first - SIZE_PAGE_BUFFER || 
                pageNumber > inkingPageRange.last + SIZE_PAGE_BUFFER)
            {
                // Remove Ink Canvas
                Grid grid = (Grid)this.imagePanel.Children[pageNumber - 1];
                if (grid.Children.Count > 1)    // No inkcanvas if count < 1
                {
                    InkCanvas inkCanvas = (InkCanvas)grid.FindName(PREFIX_CANVAS + pageNumber.ToString());
                    // Recycle ink canvas
                    inkCanvasStack.Push(inkCanvas);
                    grid.Children.RemoveAt(1);
                    this.inkCanvasList.Remove(pageNumber);
                }
                // Remove Image
                Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
                if (image != null)
                {
                    double x = image.ActualHeight;
                    image.Source = null;
                    image.Height = x;
                    renderedPages.Remove(pageNumber);
                    AppEventSource.Log.Debug("ViewerPage: Image in page " + pageNumber.ToString() + " removed.");
                }
                else AppEventSource.Log.Warn("ViewerPage: Image in page " + pageNumber.ToString() + " is empty.");
            }
        }

        private async Task AddPageImage(int pageNumber, uint renderWidth, bool forceRender = false)
        {
            if (pageNumber <= 0 || pageNumber > this.pageCount) return;
            // Get the XAML image element
            Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
            if (image == null)
            {
                AppEventSource.Log.Warn("ViewerPage: Image container for page " + pageNumber.ToString() + " not found.");
                return;
            }
            // Render pdf page to image, if image is not rendered, or a HIGHER render width is specified,
            if (forceRender || image.Source == null || 
                renderWidth > ((BitmapImage)(image.Source)).PixelWidth ||
                renderWidth < ((BitmapImage)(image.Source)).PixelWidth / 2)
            {
                renderedPages.Add(pageNumber);
                image.Source = await this.pdfModel.RenderPageImage(pageNumber, renderWidth);
                AppEventSource.Log.Debug("ViewerPage: Page " + pageNumber.ToString() + " loaded with render width " + renderWidth.ToString());
            }
        }

        
        private async Task LoadInkCanvas(int pageNumber)
        {
            Grid grid = (Grid)this.imagePanel.Children[pageNumber - 1];
            if (grid == null)
            {
                AppEventSource.Log.Warn("ViewerPage: Grid container for page " + pageNumber.ToString() + " not found.");
                return;
            }
            // Update the grid size to match the image size
            Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
            grid.Width = image.ActualWidth;
            grid.Height = image.ActualHeight;
            // Find existing ink canvas
            InkCanvas inkCanvas = (InkCanvas)grid.FindName(PREFIX_CANVAS + pageNumber.ToString());
            // If an ink canvas does not exist, add a new one
            if (inkCanvas == null)
            {
                Binding bindingHeight = new Binding();
                bindingHeight.Mode = BindingMode.OneWay;
                bindingHeight.Path = new PropertyPath("ActualHeight");
                bindingHeight.Source = image;
                Binding bindingWidth = new Binding();
                bindingWidth.Mode = BindingMode.OneWay;
                bindingWidth.Path = new PropertyPath("ActualWidth");
                bindingWidth.Source = image;
                // Add ink canvas
                if (inkCanvasStack.Count > 0)
                    inkCanvas = inkCanvasStack.Pop();
                else inkCanvas = new InkCanvas();
                inkCanvas.Name = PREFIX_CANVAS + pageNumber.ToString();
                inkCanvas.InkPresenter.InputDeviceTypes = inkingPreference.drawingDevice;
                inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
                inkCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
                inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
                inkCanvas.InkPresenter.InputProcessingConfiguration.Mode = this.inkProcessMode;
                inkCanvas.SetBinding(HeightProperty, bindingHeight);
                inkCanvas.SetBinding(WidthProperty, bindingWidth);

                this.inkCanvasList.Add(pageNumber);
                // Load inking if exist
                InkStrokeContainer inkStrokesContainer = await inkManager.LoadInking(pageNumber);
                if (inkStrokesContainer != null)
                    inkCanvas.InkPresenter.StrokeContainer = inkStrokesContainer;
                // Add ink canvas page
                grid.Children.Add(inkCanvas);
                
            }
        }

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            int p = findPageNumberByInkPresenter(sender);
            inkManager.EraseStrokes(p, sender.StrokeContainer, args.Strokes);
        }

        private async void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            // Notify user about the risk when using inking for the first time.
            if ((bool)App.AppSettings[App.INKING_WARNING])
            {
                int userResponse = await App.NotifyUserWithOptions(Messages.INK_STROKE_WARNING,
                    new string[] { "OK, do not show this again.", "Notify me again next time." });
                switch (userResponse)
                {
                    case 0: // Do not show again
                        ApplicationData.Current.RoamingSettings.Values[App.INKING_WARNING] = false;
                        App.AppSettings[App.INKING_WARNING] = false;
                        break;
                    default:
                        App.AppSettings[App.INKING_WARNING] = false;
                        break;
                }
            }
            int p = findPageNumberByInkPresenter(sender);
            inkManager.AddStrokes(p, sender.StrokeContainer, args.Strokes);
        }

        private int findPageNumberByInkPresenter(InkPresenter sender)
        {
            // Find the page number
            int pageNumber = 0;
            foreach (int i in inkCanvasList)
            {
                InkCanvas inkCanvas = (InkCanvas)this.imagePanel.FindName(PREFIX_CANVAS + i.ToString());
                if (inkCanvas.InkPresenter == sender)
                {
                    pageNumber = i;
                    break;
                }
            }
            if (pageNumber == 0)
            {
                AppEventSource.Log.Error("ViewerPage: Strokes Changed but Ink canvas not found.");
            }
            return pageNumber;
        }

        /// <summary>
        /// Update drawing attributes of ink presenter in each ink canvas
        /// </summary>
        private void UpdateInkPresenter()
        {
            if (this.inkCanvasList == null)
            {
                AppEventSource.Log.Debug("ViewerPage: Updating drawing attributes: No ink canvas found.");
                return;
            }
            foreach (int pageNumber in inkCanvasList)
            {
                InkCanvas inkCanvas = (InkCanvas)this.imagePanel.FindName(PREFIX_CANVAS + pageNumber.ToString());
                inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(this.drawingAttributes);
                inkCanvas.InkPresenter.InputProcessingConfiguration.Mode = this.inkProcessMode;
                inkCanvas.InkPresenter.InputDeviceTypes = this.inkingPreference.drawingDevice;
            }
            AppEventSource.Log.Debug("ViewerPage: Drawing attributes updated.");
        }

        /// <summary>
        /// Determine whether an element is visible, assuming the container is visible.
        /// </summary>
        /// <param name="element"></param>
        /// <param name="container"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Determine whether a page is visible.
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <returns></returns>
        private Boolean IsPageVisible(int pageNumber)
        {
            return IsUserVisible((Image)imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString()), this.scrollViewer);
        }

        /// <summary>
        /// Refresh the visible pages.
        /// </summary>
        private void RefreshViewer()
        {
            if(this.fileLoaded)
                PreparePages(this.VisiblePageRange);
        }

        /// <summary>
        /// Find a visible page.
        /// </summary>
        /// <returns>A page number, which can be any visible page.</returns>
        private int FindVisiblePage()
        {
            // Find a page that is currently visible
            // Check current page range
            for (int i = VisiblePageRange.first; i <= VisiblePageRange.last; i++)
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

        /// <summary>
        /// Determine the visible page range
        /// </summary>
        /// <returns></returns>
        private PageRange FindVisibleRange()
        {
            // Find a visible page,
            int visiblePage = FindVisiblePage();
            if (visiblePage <= 0)
            {
                AppEventSource.Log.Warn("ViewerPage: No visible page found.");
                return new PageRange(1, 1);
            }
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

        /// <summary>
        /// This method is not used. 
        /// </summary>
        private void SetMinZoomFactor()
        {
            if (!this.fileLoaded) return;
            // Store current zoom factor
            double factor = scrollViewer.ZoomFactor;
            // Update visible page range
            this._visiblePageRange = FindVisibleRange();
            // Find the max page height and width in the visible pages
            double maxHeight = 0;
            double maxWidth = 0;
            for (int i = this.VisiblePageRange.first; i <= this.VisiblePageRange.last; i++)
            {
                Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + i.ToString());
                if (image.ActualHeight > maxHeight) maxHeight = image.ActualHeight;
                if (image.ActualWidth > maxWidth) maxWidth = image.MaxWidth;
            }
            // Recalculate min zoom factor
            double hZoomFactor = (this.scrollViewer.ActualHeight - SCROLLBAR_WIDTH
                - 2 * (PAGE_IMAGE_MARGIN + imagePanel.Margin.Top)) / maxHeight;
            double wZoomFactor = (this.scrollViewer.ActualWidth - SCROLLBAR_WIDTH
                - 2 * (PAGE_IMAGE_MARGIN + imagePanel.Margin.Left)) / maxWidth;
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
            // Recalculate offsets if zoom level changed
            if (this.scrollViewer.MinZoomFactor > factor)
            {
                factor = this.scrollViewer.MinZoomFactor / factor;
                scrollViewer.ChangeView(factor * scrollViewer.HorizontalOffset,
                    factor * scrollViewer.VerticalOffset, scrollViewer.ZoomFactor, true);
                AppEventSource.Log.Debug("ViewerPage: zoom factor changed, offsets recalculated.");
            }
        }

        private void RefreshTimer_Tick(object sender, object e)
        {
            refreshTimer.Stop();
            this.RefreshViewer();
        }

        private void InitializationTimer_Tick(object sender, object e)
        {
            this.initializationTimer.Stop();
            int count = imagePanel.Children.Count;
            for (int i = count + 1; i <= Math.Min(count + SIZE_PAGE_BATCH, pageCount); i++)
            {
                AddBlankImage(i);
            }
            this.fullScreenMessage.Text = "Loading... " + (this.imagePanel.Children.Count * 100 / this.pageCount).ToString() + "%";
            if (imagePanel.Children.Count >= pageCount)
            {
                AppEventSource.Log.Debug("ViewerPage: Blank images add for all pages. Count: " + imagePanel.Children.Count.ToString());
                FinishInitialization();
            }
            else this.initializationTimer.Start();
        }

        private void RecycleTimer_Tick(object sender, object e)
        {
            this.recycleTimer.Stop();
            RecyclePages();
            this.recycleTimer.Start();
        }

        private void RecyclePages()
        {
            // Avoid recycling when saving inking?
            while (this.recyclePagesQueue.Count > SIZE_RECYCLE_QUEUE)
            {
                RemovePage(this.recyclePagesQueue.Dequeue());
            }
        }

        private void pageNumberTextTimer_Tick(object sender, object e)
        {
            pageNumberTextTimer.Stop();
            this.infoGridFadeOut.Begin();
        }

        private void scrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // Determine visible page range
            this._visiblePageRange = FindVisibleRange();
            // Show page number
            this.pageNumberTextBlock.Text = this.VisiblePageRange.ToString() + " / " + this.pageCount.ToString();
            // Show zoom level
            this.zoomFactorTextBlock.Text = ((int)(this.scrollViewer.ZoomFactor * 100)).ToString() + "%";
            // Show information grid
            this.infoGrid.Opacity = INFO_GRID_OPACITY;
            // The following code acts like a filter to prevent the timer ticking too frequently
            if (fileLoaded && !e.IsIntermediate)
            {
                refreshTimer.Stop();
                refreshTimer.Start();
                pageNumberTextTimer.Stop();
                pageNumberTextTimer.Start();
            }
        }

        /// <summary>
        /// Adjust the UI based on the window width.
        /// This event handler does the same thing as adaptive trigger.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Filename Textblock
            if (Window.Current.Bounds.Width > MIN_WIDTH_TO_SHOW_FILENAME)
                this.filenameTextBlock.Visibility = Visibility.Visible;
            else
                this.filenameTextBlock.Visibility = Visibility.Collapsed;
            // View orientation buttons
            if (Window.Current.Bounds.Width > MIN_WIDTH_TO_SHOW_ORIENTATION_BTN)
            {
                this.VerticalViewBtn.Visibility = Visibility.Visible;
                this.VerticalViewSecBtn.Visibility = Visibility.Collapsed;
                this.HorizontalViewBtn.Visibility = Visibility.Visible;
                this.HorizontalViewSecBtn.Visibility = Visibility.Collapsed;
                this.ViewSecBtnSeparator.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.VerticalViewBtn.Visibility = Visibility.Collapsed;
                this.VerticalViewSecBtn.Visibility = Visibility.Visible;
                this.HorizontalViewBtn.Visibility = Visibility.Collapsed;
                this.HorizontalViewSecBtn.Visibility = Visibility.Visible;
                this.ViewSecBtnSeparator.Visibility = Visibility.Visible;
            }
            // Close view button
            if (Window.Current.Bounds.Width > MIN_WIDTH_TO_SHOW_CLOSE_BTN)
            {
                this.closeThisView.Visibility = Visibility.Visible;
                this.closeThisViewSec.Visibility = Visibility.Collapsed;
            }
            else
            {
                this.closeThisView.Visibility = Visibility.Collapsed;
                this.closeThisViewSec.Visibility = Visibility.Visible;
            }
        }

        private void ClearInputTypeToggleBtn()
        {
            this.Pencil.IsChecked = false;
            this.Highlighter.IsChecked = false;
            this.Eraser.IsChecked = false;
        }

        private void DisableInputTypeBtn()
        {
            this.Pencil.Visibility = Visibility.Collapsed;
            this.Eraser.Visibility = Visibility.Collapsed;
            this.Highlighter.Visibility = Visibility.Collapsed;
        }

        private void EnableInputTypeBtn()
        {
            this.Pencil.Visibility = Visibility.Visible;
            this.Eraser.Visibility = Visibility.Visible;
            this.Highlighter.Visibility = Visibility.Visible;
        }

        private void Pencil_Click(object sender, RoutedEventArgs e)
        {
            ClearInputTypeToggleBtn();
            AppEventSource.Log.Debug("ViewerPage: Pencil selected");
            this.Pencil.IsChecked = true;
            this.drawingAttributes.Size = inkingPreference.GetPenSize(pdfModel.ScaleRatio);
            this.drawingAttributes.Color = inkingPreference.penColor;
            this.drawingAttributes.PenTip = PenTipShape.Circle;
            this.drawingAttributes.DrawAsHighlighter = false;
            this.drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.Identity;
            this.drawingAttributes.IgnorePressure = false;
            this.drawingAttributes.FitToCurve = true;
            this.inkProcessMode = InkInputProcessingMode.Inking;
            UpdateInkPresenter();
        }

        private void Highlighter_Click(object sender, RoutedEventArgs e)
        {
            ClearInputTypeToggleBtn();
            AppEventSource.Log.Debug("ViewerPage: Highlighter selected");
            this.Highlighter.IsChecked = true;
            this.drawingAttributes.Size = inkingPreference.GetHighlighterSize(pdfModel.ScaleRatio);
            this.drawingAttributes.Color = inkingPreference.highlighterColor;
            this.drawingAttributes.PenTip = PenTipShape.Rectangle;
            this.drawingAttributes.DrawAsHighlighter = true;
            this.drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.Identity;
            this.drawingAttributes.IgnorePressure = true;
            this.drawingAttributes.FitToCurve = false;
            this.inkProcessMode = InkInputProcessingMode.Inking;
            UpdateInkPresenter();
        }

        private async void Eraser_Click(object sender, RoutedEventArgs e)
        {
            ClearInputTypeToggleBtn();
            AppEventSource.Log.Debug("ViewerPage: Eraser selected");
            this.Eraser.IsChecked = true;
            this.drawingAttributes.Size = inkingPreference.GetPenSize(pdfModel.ScaleRatio);
            this.drawingAttributes.PenTip = PenTipShape.Circle;
            this.drawingAttributes.DrawAsHighlighter = false;
            this.drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.Identity;
            this.inkProcessMode = InkInputProcessingMode.Erasing;
            UpdateInkPresenter();
            // Notify user about the risk when using eraser for the first time.
            if ((bool)App.AppSettings[App.ERASER_WARNING])
            {
                int userResponse = await App.NotifyUserWithOptions(Messages.ERASER_WARNING,
                    new string[] { "OK, do not show this again.", "Notify me again next time." });
                switch (userResponse)
                {
                    case 0: // Do not show again
                        ApplicationData.Current.RoamingSettings.Values[App.ERASER_WARNING] = false;
                        App.AppSettings[App.ERASER_WARNING] = false;
                        break;
                    default:
                        App.AppSettings[App.ERASER_WARNING] = false;
                        break;
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            NavigationPage.Current.RemoveView(this.ViewerKey);
        }

        public void CloseAllViews()
        {
            CloseAll_Click(null, null);
        }

        /// <summary>
        /// Close all views.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void CloseAll_Click(object sender, RoutedEventArgs e)
        {
            SuspensionManager.ViewerStateDictionary = null;
            await SuspensionManager.SaveViewerAsync();
            SuspensionManager.AppSessionState.FileToken = null;
            this.fileLoaded = false;
            InitializeZoomInView();
            NavigationPage.Current.InitializeViewBtn();
            this.Frame.Navigate(typeof(MainPage));
        }

        private void ClearViewModeToggleBtn()
        {
            this.VerticalViewBtn.IsChecked = false;
            this.HorizontalViewBtn.IsChecked = false;
            this.GridViewBtn.IsChecked = false;
        }

        private void VerticalView_Click(object sender, RoutedEventArgs e)
        {
            this.semanticZoom.IsZoomedInViewActive = true;
            ClearViewModeToggleBtn();
            this.VerticalViewBtn.IsChecked = true;
            if (imagePanel.Orientation != Orientation.Vertical)
            {
                // Update navigation buttons
                NavigationPage.Current.UpdateViewBtn(this.ViewerKey, this.VisiblePageRange.ToString(), Symbol.Page2);
                // Save offset
                double vOffsetPercent = (this.scrollViewer.ActualWidth / 2 + this.scrollViewer.HorizontalOffset)
                    / (this.imagePanel.ActualWidth * this.scrollViewer.ZoomFactor);
                vOffsetPercent = vOffsetPercent > 1 ? 0 : vOffsetPercent;
                // Change layout
                this.imagePanel.Orientation = Orientation.Vertical;
                this.imagePanel.UpdateLayout();
                // Recalculate offset
                float zoomFactor = (float)(this.scrollViewer.ActualWidth / MaxVisiblePageSize().Width);
                zoomFactor = CheckZoomFactor(zoomFactor);
                double vOffset = 0;
                double panelHeight = this.imagePanel.ActualHeight * zoomFactor;
                vOffset = panelHeight * vOffsetPercent - this.scrollViewer.ActualHeight / 2;
                vOffset = vOffset < 0 ? 0 : vOffset;
                this.scrollViewer.ChangeView(0, vOffset, zoomFactor);
                AppEventSource.Log.Debug("ViewerPage: View " + ViewerKey.ToString() + "Changed to Vertical View.");
            }
        }

        private void HorizontalView_Click(object sender, RoutedEventArgs e)
        {
            this.semanticZoom.IsZoomedInViewActive = true;
            ClearViewModeToggleBtn();
            this.HorizontalViewBtn.IsChecked = true;
            if (imagePanel.Orientation != Orientation.Horizontal)
            {
                // Update navigation buttons
                NavigationPage.Current.UpdateViewBtn(this.ViewerKey, this.VisiblePageRange.ToString(), Symbol.TwoPage);
                // Save offset
                double hOffsetPercent = (this.scrollViewer.ActualHeight / 2 + this.scrollViewer.VerticalOffset)
                    / (this.imagePanel.ActualHeight * this.scrollViewer.ZoomFactor);
                hOffsetPercent = hOffsetPercent > 1 ? 0 : hOffsetPercent;
                // Change layout
                this.imagePanel.Orientation = Orientation.Horizontal;
                this.imagePanel.UpdateLayout();
                // Recalculate offset
                float zoomFactor = (float)(this.scrollViewer.ActualHeight / MaxVisiblePageSize().Height);
                zoomFactor = CheckZoomFactor(zoomFactor);
                double hOffset = 0;
                double panelWidth = this.imagePanel.ActualWidth * zoomFactor;
                hOffset = panelWidth * hOffsetPercent - this.scrollViewer.ActualWidth / 2;
                hOffset = hOffset < 0 ? 0 : hOffset;
                this.scrollViewer.ChangeView(hOffset, 0, zoomFactor);
                AppEventSource.Log.Debug("ViewerPage: View " + ViewerKey.ToString() + "Changed to Horizontal View.");
            }
        }

        private void GridView_Click(object sender, RoutedEventArgs e)
        {
            ClearViewModeToggleBtn();
            this.semanticZoom.IsZoomedInViewActive = !this.semanticZoom.IsZoomedInViewActive;
        }

        // <summary>
        /// Event handler for the "Export Inking to Image.." button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void SaveImage_Click(object sender, RoutedEventArgs e)
        {
            ExportPagesDialog dialog = new ExportPagesDialog(this.pageCount);
            if(await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                // Ask user to pick a folder
                FolderPicker folderPicker = new FolderPicker();
                folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                folderPicker.FileTypeFilter.Add(".PNG");
                StorageFolder folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    AppEventSource.Log.Debug("ViewerPage: Exporting Pages: " + dialog.PagesToExportString);
                    // Export images
                    int exportingPageNumber = 0;
                    int exportedCount = 0;
                    foreach (int pageNumber in dialog.PagesToExport)
                    {
                        try
                        {
                            exportingPageNumber = pageNumber;
                            string filename = dialog.ImageFilename;
                            string fileExtension = ".PNG";
                            StorageFile file = await folder.CreateFileAsync(filename + pageNumber.ToString() + fileExtension,
                                CreationCollisionOption.GenerateUniqueName);
                            InkCanvas inkCanvas = (InkCanvas)this.imagePanel.FindName(PREFIX_CANVAS + pageNumber.ToString());
                            await pdfModel.ExportPageImage(pageNumber, inkCanvas, file);
                            exportedCount++;
                        }
                        catch (Exception ex)
                        {
                            App.NotifyUser(typeof(ViewerPage), "An error occurred when exporting page " + exportingPageNumber.ToString() + ".\n" + ex.Message, true);
                        }
                    }
                    // Notify user
                    string message;
                    if (exportedCount == 0)
                        message = "Nothing Exported.";
                    else if (exportedCount == 1)
                        message = "1 Page Exported.";
                    else message = exportedCount.ToString() + " Pages Exported.";
                    App.NotifyUser(typeof(ViewerPage), message);
                }
            }
        }

        /// <summary>
        /// Event handler for the "Inking Setting" button.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void InkingSetting_Click(object sender, RoutedEventArgs e)
        {
            InkingPrefContentDialog dialog = new InkingPrefContentDialog(this.inkingPreference);
            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                // Update drawing attributes
                this.inkingPreference = dialog.InkingPreference;
                if (this.Pencil.IsChecked == true) Pencil_Click(null, null);
                else if (this.Highlighter.IsChecked == true) Highlighter_Click(null, null);
                // Save inking preference
                await SaveDrawingPreference();
            }
        }

        /// <summary>
        /// Calculate the new vertical offset after zoom factor changed
        /// </summary>
        /// <param name="newZoomFactor"></param>
        /// <returns></returns>
        private double ZoomVerticalOffset(float newZoomFactor)
        {
            double panelHeight = this.imagePanel.ActualHeight * newZoomFactor;
            double vOffsetPercent = (this.scrollViewer.ActualHeight / 2 + this.scrollViewer.VerticalOffset)
                / (this.imagePanel.ActualHeight * this.scrollViewer.ZoomFactor);
            vOffsetPercent = vOffsetPercent > 1 ? 0 : vOffsetPercent;
            double vOffset = panelHeight * vOffsetPercent - this.scrollViewer.ActualHeight / 2;
            return vOffset = vOffset < 0 ? 0 : vOffset;
        }

        /// <summary>
        /// Calculate the new horizontal offset after zoom factor changed
        /// </summary>
        /// <param name="newZoomFactor"></param>
        /// <returns></returns>
        private double ZoomHorizontalOffset(float newZoomFactor)
        {
            double panelWidth = this.imagePanel.ActualWidth * newZoomFactor;
            double hOffsetPercent = (this.scrollViewer.ActualWidth / 2 + this.scrollViewer.HorizontalOffset)
                / (this.imagePanel.ActualWidth * this.scrollViewer.ZoomFactor);
            hOffsetPercent = hOffsetPercent > 1 ? 0 : hOffsetPercent;
            double hOffset = panelWidth * hOffsetPercent - this.scrollViewer.ActualWidth / 2;
            return hOffset = hOffset < 0 ? 0 : hOffset;
        }

        private Size FitOffset(float newZoomFactor)
        {
            double hOffset, vOffset;
            if (this.imagePanel.Orientation == Orientation.Vertical)
            {
                // Calculate the vertical offset
                vOffset = ZoomVerticalOffset(newZoomFactor);
                // Center the page horizontally
                double panelWidth = this.imagePanel.ActualWidth * newZoomFactor;
                hOffset = panelWidth > this.scrollViewer.ActualWidth ? (panelWidth - this.scrollViewer.ActualWidth) / 2 : 0;
            }
            else
            {
                // Center the page vertically
                double panelHeight = this.imagePanel.ActualHeight * newZoomFactor;
                vOffset = panelHeight > this.scrollViewer.ActualHeight ? (panelHeight - this.scrollViewer.ActualHeight) / 2 : 0;
                // Calculate the horizontal offset
                hOffset = ZoomHorizontalOffset(newZoomFactor);
            }
            return new Size(hOffset, vOffset);
        }

        private void FitWidthBtn_Click(object sender, RoutedEventArgs e)
        {
            double pageWidth = MaxVisiblePageSize().Width;
            if (pageWidth > 0)
            {
                // Calculate the zoom factor.
                float zoomFactor = (float)(this.scrollViewer.ActualWidth / pageWidth);
                zoomFactor = CheckZoomFactor(zoomFactor);
                // Calculate new offsets.
                Size offset = FitOffset(zoomFactor);
                // Scroll to a suitable page
                this.scrollViewer.ChangeView(offset.Width, offset.Height, zoomFactor);
                //ScrollToPage(this.VisiblePageRange.first - 1, zoomFactor);
            }
        }

        private void FitHeightBtn_Click(object sender, RoutedEventArgs e)
        {
            double pageHeight = MaxVisiblePageSize().Height;
            if (pageHeight > 0)
            {
                float zoomFactor = (float)(this.scrollViewer.ActualHeight / pageHeight);
                zoomFactor = CheckZoomFactor(zoomFactor);
                // Calculate new offsets.
                Size offset = FitOffset(zoomFactor);
                // Scroll to a suitable page 
                this.scrollViewer.ChangeView(offset.Width, offset.Height, zoomFactor);
                //ScrollToPage(this.VisiblePageRange.first - 1, zoomFactor);
            }
        }

        private void FitPageBtn_Click(object sender, RoutedEventArgs e)
        {
            Size pageSize = MaxVisiblePageSize();
            if (pageSize.Height > pageSize.Width)
                FitHeightBtn_Click(sender, e);
            else
                FitWidthBtn_Click(sender, e);
        }

        /// <summary>
        /// Scroll to a specific page and center the view.
        /// </summary>
        /// <param name="pageIndex"></param>
        /// <param name="zoomFactor"></param>
        private bool ScrollToPage(int pageIndex, float? zoomFactor = null)
        {
            if (pageIndex < 0) return false;
            if (zoomFactor == null) zoomFactor = this.scrollViewer.ZoomFactor;
            double pageOffset = 0;
            // Calculate Offset
            for (int i = 0; i < pageIndex; i++)
            {
                pageOffset += this.imagePanel.Orientation == Orientation.Vertical ?
                    this.pdfModel.GetPage(i + 1).Size.Height :
                    this.pdfModel.GetPage(i + 1).Size.Width;
                pageOffset += 2 * PAGE_IMAGE_MARGIN;
            }
            pageOffset *= (float)zoomFactor;
            double viewOffset;
            if(this.imagePanel.Orientation == Orientation.Vertical)
            {
                double panelWidth = this.imagePanel.ActualWidth * (float)zoomFactor;
                viewOffset = panelWidth > this.scrollViewer.ActualWidth ? (panelWidth - this.scrollViewer.ActualWidth) / 2 : 0;
            }
            else
            {
                double panelHeight = this.imagePanel.ActualHeight * (float)zoomFactor;
                viewOffset = panelHeight > this.scrollViewer.ActualHeight ? (panelHeight - this.scrollViewer.ActualHeight) / 2 : 0;
            }

            // Change View
            if (this.imagePanel.Orientation == Orientation.Vertical)
                return this.scrollViewer.ChangeView(viewOffset, pageOffset, zoomFactor);
            else return this.scrollViewer.ChangeView(pageOffset, viewOffset, zoomFactor);
        }

        private bool ZoomView(float zoomFactor)
        {
            double hOffset = ZoomHorizontalOffset(zoomFactor);
            double vOffset = ZoomVerticalOffset(zoomFactor);
            return this.scrollViewer.ChangeView(hOffset, vOffset, zoomFactor);
        }

        private void ActualSizeBtn_Click(object sender, RoutedEventArgs e)
        {
            ZoomView(1);
        }

        private void ZoomInBtn_Click(object sender, RoutedEventArgs e)
        {
            float newZoomFactor = (float)(this.scrollViewer.ZoomFactor * (1 + ZOOM_STEP_SIZE));
            newZoomFactor = newZoomFactor > this.scrollViewer.MaxZoomFactor ? this.scrollViewer.MaxZoomFactor : newZoomFactor;
            ZoomView(newZoomFactor);
        }

        private void ZoomOutBtn_Click(object sender, RoutedEventArgs e)
        {
            float newZoomFactor = (float)(this.scrollViewer.ZoomFactor * (1 - ZOOM_STEP_SIZE));
            newZoomFactor = newZoomFactor < this.scrollViewer.MinZoomFactor ? this.scrollViewer.MinZoomFactor : newZoomFactor;
            ZoomView(newZoomFactor);
        }

        private void GoToPage_Click(object sender, RoutedEventArgs e)
        {
            // TODO
        }

        // The following are for grid view

        /// <summary>
        /// Event handler for view switching
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void semanticZoom_ViewChangeStarted(object sender, SemanticZoomViewChangedEventArgs e)
        {
            if (e.IsSourceZoomedInView)
            {
                // Switching to zoom out view (Grid View)
                this.GridViewBtn.IsChecked = true;
                DisableInputTypeBtn();
                this.ZoomBtn.IsEnabled = false;
                // Pause zoom in view rendering 
                this.renderPagesQueue.Clear();
                // Resume zoom out view rendering
                this.pageThumbnails.ResumeRendering();
                // Initialize thumbnails
                if (!this.pageThumbnails.IsInitialized)
                    await this.pageThumbnails.InitializeBlankPages();
                // Sync current visible page between views
                int pageIndex = this.VisiblePageRange.first - 1;
                this.zoomOutGrid.ScrollIntoView(this.pageThumbnails[pageIndex]);
                // Clear previous selected index
                this.pageThumbnails.SelectedIndex = -1;
            }
            else
            {
                // Switching to zoom in view
                this.GridViewBtn.IsChecked = false;
                if (this.imagePanel.Orientation == Orientation.Vertical)
                    this.VerticalViewBtn.IsChecked = true;
                else this.HorizontalViewBtn.IsChecked = true;
                EnableInputTypeBtn();
                this.ZoomBtn.IsEnabled = true;
                // Pause zoom out view rendering
                this.pageThumbnails.PauseRendering();
                // Resume zoom in view rendering
                RefreshViewer();
                // Sync current visible page between views
                int pageIndex = this.pageThumbnails.SelectedIndex;
                if (pageIndex >= 0)
                    ScrollToPage(pageIndex);
            }
        }

        /// <summary>
        /// Store the page thumbnails
        /// </summary>
        private PageThumbnailCollection pageThumbnails;

        /// <summary>
        /// Select the page if the page index is the same as the one when the pointer is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThumbnailGrid_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            int releasedIndex = (int)(((PageDetail)((Grid)sender).DataContext).PageNumber - 1
                    - (this.pdfModel.PageCount - this.pageThumbnails.Count));
            if (releasedIndex == this.pageThumbnails.PressedIndex) this.pageThumbnails.SelectedIndex = releasedIndex;
        }

        /// <summary>
        /// Record the page index when pointer is pressed.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThumbnailGrid_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            this.pageThumbnails.PressedIndex = (int)(((PageDetail)((Grid)sender).DataContext).PageNumber - 1
                    - (this.pdfModel.PageCount - this.pageThumbnails.Count));
        }

        private async void SaveInking_Click(object sender, RoutedEventArgs e)
        {
            // Ask user to confirm
            if ((bool)App.AppSettings[App.CONFIRM_SAVING])
            {
                int userResponse = await App.NotifyUserWithOptions(Messages.SAVE_INKING_CLICKED, new string[] { "Yes", "Cancel" });
                switch (userResponse)
                {
                    case 0:     // Yes
                        // Show full screen cover with message
                        this.fullScreenCover.Visibility = Visibility.Visible;
                        this.fullScreenCover.Opacity = 0.6;
                        this.fullScreenMessage.Text = "Saving ink annotations to PDF file...";
                        // Save inking to pdf
                        bool inkSaved = await pdfModel.SaveInkingToPdf(inkManager);
                        if (inkSaved)
                        {
                            // Ink annotations are saved successfully
                            // Remove in app inking
                            await inkManager.RemoveInAppInking();
                            // Reload file
                            pdfModel = await PdfModel.LoadFromFile(pdfStorageFile, dataFolder);
                            // Failed to load the file again?
                            // Re-render pages
                            await reRenderPages();
                        }
                        else App.NotifyUser(typeof(ViewerPage), "Failed to save the annotations.", true);
                        this.fullScreenCover.Opacity = 1.0;
                        this.fullScreenCover.Visibility = Visibility.Collapsed;
                        this.fullScreenMessage.Text = "";
                        break;
                    default:    // Cancel
                        break;
                }
            }
        }

        /// <summary>
        /// Re-render the page images.
        /// </summary>
        private async Task reRenderPages()
        {
            // Clear the recycle queue
            recyclePagesQueue.Clear();
            // Remove the rendered pages
            while (renderedPages.Count > 0)
            {
                RemovePage(renderedPages[0], true);
            }
            // Add visible pages to rendering queue
            for (int i = VisiblePageRange.first; i <= VisiblePageRange.last; i++)
            {
                renderPagesQueue.Enqueue(i);
            }
            // Render visible pages.
            await RenderPagesAsync();
            // Preare pages in the visible range
            PreparePages(this.VisiblePageRange);
        }
    }
}
