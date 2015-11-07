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
using Windows.UI.Popups;
using Windows.Storage.AccessCache;
using System.IO;
using NavigationMenu;
using Windows.Storage.Pickers;
using Microsoft.Graphics.Canvas;
using Windows.UI;
using Windows.UI.Xaml.Media;
using Libra.Class;

namespace Libra
{
    /// <summary>
    /// Implement the pdf viewer using a scroll viewer.
    /// </summary>
    public sealed partial class ViewerPage : Page
    {
        private const int SCROLLBAR_WIDTH = 10;
        private const int PAGE_IMAGE_MARGIN = 10;
        private const int NAVIGATION_WIDTH = 48;
        private const int SIZE_PAGE_BUFFER = 5;
        private const int SIZE_RECYCLE_QUEUE = 10;
        private const int SIZE_PAGE_BATCH = 100;
        private const int FIRST_LOAD_PAGES = 2;
        private const int REFRESH_TIMER_TICKS = 50 * 10000;
        private const int INITIALIZATION_TIMER_TICKS = 10 * 10000;
        private const int RECYCLE_TIMER_SECOND = 1;
        private const int PAGE_NUMBER_TIMER_SECOND = 2;
        private const int DEFAULT_RENDER_WIDTH = 1500;
        private const double PAGE_NUMBER_OPACITY = 0.75;

        private const string PREFIX_PAGE = "page";
        private const string PREFIX_GRID = "grid";
        private const string PREFIX_CANVAS = "canvas";
        private const string PREFIX_VIEWBOX = "viewbox";
        private const string EXT_INKING = ".gif";
        private const string EXT_VIEW = ".xml";
        private const string INKING_FOLDER = "Inking";
        private const string INKING_PREFERENCE_FILENAME = "_inkingPreference.xml";
        private const string INKING_PROFILE_FILENAME = "_inkingProfile.xml";
        private const string PAGE_SIZE_FILENAME = "_pageSize.xml";
        private const string DEFAULT_FULL_SCREEN_MSG = "No File is Opened.";

        private StorageFile pdfFile;
        private StorageFolder dataFolder;
        private StorageFolder inkingFolder;
        private PdfDocument pdfDocument;
        private Thickness pageMargin;
        private PageRange inkingPageRange;
        private PageRange _visiblePageRange;
        public PageRange VisiblePageRange { get { return this._visiblePageRange; } }
        private DispatcherTimer refreshTimer;
        private DispatcherTimer initializationTimer;
        private DispatcherTimer recycleTimer;
        private DispatcherTimer pageNumberTextTimer;
        private Queue<int> recyclePagesQueue;
        private Queue<int> renderPagesQueue;
        private Dictionary<int, InkStrokeContainer> inkingDictionary;
        private Dictionary<int, double> pageSizeDictionary;
        private List<int> inkCanvasList;
        private InkDrawingAttributes drawingAttributes;
        private InkingProfile inkingProfile;
        private InkingPreference inkingPreference;
        //private Windows.UI.Core.CoreInputDeviceTypes drawingDevice;
        private InkInputProcessingMode inkProcessMode;
        private System.Diagnostics.Stopwatch fileLoadingWatch;

        private Guid _viewerKey;
        public Guid ViewerKey { get { return this._viewerKey; } }

        private int pageCount;
        private double defaultPageHeight;
        private double defaultPageWidth;
        private bool fileLoaded;
        private bool isSavingInking;
        private bool inkingChanged;
        private bool isRenderingPage;
        private string futureAccessToken;

        private uint[] renderWidthArray;

        public static ViewerPage Current = null;

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

            this.pageNumberTextTimer = new DispatcherTimer();
            this.pageNumberTextTimer.Tick += pageNumberTextTimer_Tick;
            this.pageNumberTextTimer.Interval = new TimeSpan(0, 0, PAGE_NUMBER_TIMER_SECOND);

            this.Loaded += (sender, args) =>
            {
                Current = this;
            };

            InitializeViewer();
            AppEventSource.Log.Debug("ViewerPage: Page initialized.");
        }

        private void InitializeViewer()
        {
            this.imagePanel.Children.Clear();
            this.imagePanel.UpdateLayout();
            this.inkingPageRange = new PageRange();
            this._visiblePageRange = new PageRange();
            this.defaultPageHeight = 0;
            this.defaultPageWidth = 0;
            this.pageCount = 0;
            this.inkingDictionary = new Dictionary<int, InkStrokeContainer>();
            this.inkCanvasList = new List<int>();
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
            AppEventSource.Log.Debug("ViewerPage: Viewer panel and settings initialized.");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // Set viewer mode
            if (SuspensionManager.sessionState != null)
                SuspensionManager.sessionState.ViewerMode = 1;
            // Check if a new file is opened
            if (this.fileLoaded && !this.pdfFile.IsEqual(SuspensionManager.pdfFile))
            {
                // Another file already opened
                AppEventSource.Log.Debug("ViewerPage: Another file is already opened: " + this.pdfFile.Name);
                this.fileLoaded = false;
            }
            if (!this.fileLoaded)
            {
                // Load a new file
                InitializeViewer();
                this.pdfFile = SuspensionManager.pdfFile;
                LoadFile(this.pdfFile);
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
                if (SuspensionManager.viewerStateDictionary.ContainsKey(this.ViewerKey))
                {
                    SuspensionManager.viewerStateDictionary[this.ViewerKey] = SaveViewerState();
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

        private ViewerState SaveViewerState()
        {
            ViewerState viewerState = new ViewerState(this.futureAccessToken);
            viewerState.hOffset = this.scrollViewer.HorizontalOffset;
            viewerState.vOffset = this.scrollViewer.VerticalOffset;
            viewerState.panelWidth = this.imagePanel.ActualWidth;
            viewerState.panelHeight = this.imagePanel.ActualHeight;
            viewerState.zFactor = this.scrollViewer.ZoomFactor;
            viewerState.lastViewed = DateTime.Now;
            viewerState.visibleRange = this.VisiblePageRange;
            if (this.imagePanel.Orientation == Orientation.Horizontal)
                viewerState.isHorizontalView = true;
            return viewerState;
        }

        /// <summary>
        /// Restore a saved view of a file. 
        /// </summary>
        /// <param name="key">The key to identify which view to be restored, if there are more than one save views.
        /// the latest view will be restored if key is not specified. </param>
        private void RestoreViewerState(Guid key = new Guid())
        {
            if (key == Guid.Empty)
            {
                // Check which one is the latest view
                foreach (KeyValuePair<Guid, ViewerState> entry in SuspensionManager.viewerStateDictionary)
                {
                    //ViewerState entry = SuspensionManager.viewerStateList[i];
                    if (key == Guid.Empty || entry.Value.lastViewed > SuspensionManager.viewerStateDictionary[key].lastViewed)
                        key = entry.Key;
                }
            }
            this._viewerKey = key;

            NavigationPage.Current.UpdateViewBtn(this.ViewerKey);
            
            ViewerState viewerState = SuspensionManager.viewerStateDictionary[this.ViewerKey];
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
                    // The following method may not work correctly if the file has pages of different sizes.
                    // Scale the offsets if the App window has a different size
                    // Unit zoom factor of scroll viewer depends on the intial window size when the App is opened.
                    // Zoom factor for scroll viewer will always be 1 when the App opens.
                    // The file may be displayed at different zoom level even if the zoom factor is the same.
                    // Therefore zoom factor is not reliable for restoring the view of the file.
                    // The goal for restoring the view is to make the file zoomed at the same level.
                    double hScale = imagePanel.ActualWidth / (viewerState.panelWidth * viewerState.zFactor);
                    double vScale = imagePanel.ActualHeight / (viewerState.panelHeight * viewerState.zFactor);
                    float zoomFactor = Math.Min((float)(1 / Math.Max(hScale, vScale)), this.scrollViewer.MaxZoomFactor);
                    // Restore viewer offsets
                    this.scrollViewer.ChangeView(viewerState.hOffset, viewerState.vOffset, zoomFactor);
                    AppEventSource.Log.Info("ViewerPage: Viewer state restored. " + this.pdfFile.Name);
                    return;
                }
            }
            // Reset the scroll viewer if failed to restore viewer state.
            this.imagePanel.Orientation = Orientation.Vertical;
            this.scrollViewer.ChangeView(0, 0, 1);
        }

        private async Task LoadViewerState()
        {
            await SuspensionManager.LoadViewerAsync();
            // Create a new viewer state dictionary if none is loaded
            if (SuspensionManager.viewerStateDictionary == null || SuspensionManager.viewerStateDictionary.Count == 0)
            {
                SuspensionManager.viewerStateDictionary = new Dictionary<Guid, ViewerState>();
                SuspensionManager.viewerStateDictionary.Add(Guid.NewGuid(), SaveViewerState());
            }
            // Create navigation buttons for the views
            NavigationPage.Current.InitializeViewBtn();
        }

        private async Task SaveInking()
        {
            System.Diagnostics.Stopwatch inkingSavingWatch = new System.Diagnostics.Stopwatch();
            inkingSavingWatch.Start();
            // Save ink canvas
            this.isSavingInking = true;
            foreach (int pageNumber in this.inkCanvasList)
            {
                SaveInkCanvas(pageNumber);
            }
            AppEventSource.Log.Debug("ViewerPage: Saving inking of " + this.pdfFile.Name);
            // Save ink strokes
            if (this.inkingDictionary.Count == 0)
            {
                AppEventSource.Log.Debug("ViewerPage: No inking recorded.");
                return;
            }

            // Save inking to file
            try
            {
                foreach (KeyValuePair<int, InkStrokeContainer> entry in inkingDictionary)
                {
                    StorageFile inkFile = await this.inkingFolder.CreateFileAsync(
                        entry.Key.ToString() + EXT_INKING, CreationCollisionOption.ReplaceExisting);
                    using (IRandomAccessStream inkStream = await inkFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        await entry.Value.SaveAsync(inkStream);
                    }
                    AppEventSource.Log.Debug("ViewerPage: Inking for page " + entry.Key.ToString() + " saved.");
                }
            }
            catch (Exception ex)
            {
                NotifyUser("An error occurred when saving inking. \n" + ex.Message, true);
            }

            AppEventSource.Log.Info("ViewerPage: Inking for " + this.pdfFile.Name + " saved to " + this.dataFolder.Name);
            inkingSavingWatch.Stop();
            AppEventSource.Log.Info("ViewerPage: Finished saving process in " + inkingSavingWatch.Elapsed.TotalSeconds.ToString() + " seconds.");
            this.isSavingInking = false;
        }

        private async Task LoadInking()
        {
            System.Diagnostics.Stopwatch inkingLoadingWatch = new System.Diagnostics.Stopwatch();
            inkingLoadingWatch.Start();
            AppEventSource.Log.Debug("ViewerPage: Checking inking for " + this.pdfFile.Name);
            // TODO: Need to check if the inking is suitable for the file/page.
            //
            //
            int loadingPageNumber = 0;
            try
            {
                this.inkingDictionary = new Dictionary<int, InkStrokeContainer>();
                foreach (StorageFile inkFile in await inkingFolder.GetFilesAsync())
                {
                    int pageNumber = Convert.ToInt32(inkFile.Name.Substring(0, inkFile.Name.Length - 4));
                    InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
                    using (var inkStream = await inkFile.OpenSequentialReadAsync())
                    {
                        await inkStrokeContainer.LoadAsync(inkStream);
                    }
                    this.inkingDictionary.Add(pageNumber, inkStrokeContainer);
                    AppEventSource.Log.Debug("ViewerPage: Inking for page " + pageNumber.ToString() + " loaded.");
                }
                inkingLoadingWatch.Stop();
                AppEventSource.Log.Info("ViewerPage: Inking loaded in " + inkingLoadingWatch.Elapsed.TotalSeconds.ToString() + " seconds.");
            }
            catch (Exception e)
            {
                NotifyUser("Error when loading inking for page " + loadingPageNumber.ToString() + "\n Exception: " + e.Message, true);
            }
        }

        private async Task SaveDrawingPreference()
        {
            AppEventSource.Log.Debug("ViewerPage: Saving drawing preference...");
            StorageFile file = await 
                ApplicationData.Current.LocalFolder.CreateFileAsync(INKING_PREFERENCE_FILENAME, CreationCollisionOption.ReplaceExisting);
            await SuspensionManager.SerializeToFileAsync(this.inkingPreference, typeof(InkingPreference), file);
        }

        private async Task LoadDrawingPreference()
        {
            // Check drawing preference file
            AppEventSource.Log.Debug("ViewerPage: Checking previously saved drawing preference...");
            StorageFile file = await SuspensionManager.GetSavedFileAsync(INKING_PREFERENCE_FILENAME);
            this.inkingPreference = await
                SuspensionManager.DeserializeFromFileAsync(typeof(InkingPreference), file) as InkingPreference;
            // Discard the inking setting if it has a 0 page width
            if (this.inkingPreference == null)
            {
                // Create drawing preference file if one does not exist
                AppEventSource.Log.Debug("ViewerPage: No saved drawing preference found. Creating a new one...");
                this.inkingPreference = new InkingPreference();
                await SaveDrawingPreference();
            }
            // Drawing preference
            this.drawingAttributes = new InkDrawingAttributes();
            this.drawingAttributes.IgnorePressure = false;
            this.drawingAttributes.FitToCurve = true;
            
            Pencil_Click(null, null);
        }

        private async Task LoadInkingProfile()
        {
            // Check inking profile
            AppEventSource.Log.Debug("ViewerPage: Checking inking profile...");
            StorageFile file = await SuspensionManager.GetSavedFileAsync(INKING_PROFILE_FILENAME, this.dataFolder);
            this.inkingProfile = await
                SuspensionManager.DeserializeFromFileAsync(typeof(InkingProfile), file) as InkingProfile;
            // Discard the inking setting if it has a 0 page width
            if (inkingProfile != null && inkingProfile.PageWidth == 0)
                inkingProfile = null;
            if (inkingProfile == null)
            {
                // Create an inking profile if one does not exist.
                // Inking profile will only be created and saved once (Read-only thereafter).
                AppEventSource.Log.Debug("ViewerPage: No saved inking profile found. Creating a new one in " + dataFolder.Name);
                inkingProfile = new InkingProfile(this.defaultPageWidth);
                file = await dataFolder.CreateFileAsync(INKING_PROFILE_FILENAME, CreationCollisionOption.ReplaceExisting);
                await SuspensionManager.SerializeToFileAsync(this.inkingProfile, typeof(InkingProfile), file);
            }
        }

        private async Task SavePageSizeFile()
        {
            AppEventSource.Log.Debug("ViewerPage: Saving page size to file...");
            StorageFile file = await
                dataFolder.CreateFileAsync(PAGE_SIZE_FILENAME, CreationCollisionOption.ReplaceExisting);
            await SuspensionManager.SerializeToFileAsync(this.pageSizeDictionary, typeof(Dictionary<int, double>), file);
        }

        private async Task LoadPageSizeFile()
        {
            AppEventSource.Log.Debug("ViewerPage: Checking previously saved page size file...");
            StorageFile file = await SuspensionManager.GetSavedFileAsync(PAGE_SIZE_FILENAME, this.dataFolder);
            this.pageSizeDictionary = await
                SuspensionManager.DeserializeFromFileAsync(typeof(Dictionary<int,double>), file) as Dictionary<int, double>;
            // Create a new one if not loaded
            if (this.pageSizeDictionary == null) this.pageSizeDictionary = new Dictionary<int, double>();
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
            AppEventSource.Log.Info("ViewerPage: Loading file: " + this.pdfFile.Name);
            // Update UI and Display loading
            this.fullScreenCover.Visibility = Visibility.Visible;
            this.fullScreenMessage.Text = "Loading...";
            this.filenameTextBlock.Text = this.pdfFile.Name;
            // Add file the future access list
            this.futureAccessToken = StorageApplicationPermissions.FutureAccessList.Add(pdfFile);
            // Save session state in suspension manager
            SuspensionManager.sessionState = new SessionState(this.futureAccessToken);
            // Load Pdf file
            IAsyncOperation<PdfDocument> getPdfTask = PdfDocument.LoadFromFileAsync(pdfFile);
            // Check future access list
            await CheckFutureAccessList();
            // Create local data folder, if not exist
            this.dataFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync(this.futureAccessToken, CreationCollisionOption.OpenIfExists);
            // Create inking folder
            this.inkingFolder = await dataFolder.CreateFolderAsync(INKING_FOLDER, CreationCollisionOption.OpenIfExists);
            // Wait until the file is loaded
            this.pdfDocument = await getPdfTask;

            this.pageCount = (int)pdfDocument.PageCount;
            AppEventSource.Log.Debug("ViewerPage: Total pages: " + this.pageCount.ToString());
            this.renderWidthArray = new uint[this.pageCount];
            this.defaultPageWidth = Window.Current.Bounds.Width - NAVIGATION_WIDTH - 2 * PAGE_IMAGE_MARGIN - SCROLLBAR_WIDTH;
            AppEventSource.Log.Debug("ViewerPage: Page width set to " + this.defaultPageWidth.ToString());
            // Load inking profile
            await LoadInkingProfile();
            // Load drawing preference
            await LoadDrawingPreference();
            // Add and load the first two pages
            Image image;
            int i = 1;
            //for (int i = 1; i <= Math.Min(FIRST_LOAD_PAGES, pageCount); i++)
            //{
                Grid grid = new Grid();
                grid.Name = PREFIX_GRID + i.ToString();
                grid.Margin = pageMargin;
                image = new Image();
                image.Name = PREFIX_PAGE + i.ToString();
                image.Width = defaultPageWidth;
                grid.Children.Add(image);
                this.imagePanel.Children.Add(grid);
                await LoadPage(i);
            //}
            AppEventSource.Log.Debug("ViewerPage: First " + Math.Min(FIRST_LOAD_PAGES, pageCount).ToString() + " pages rendered.");
            // Update layout to force the calculation of actual height and width of the image.
            // Otherwise, the following code may be executed BEFORE the stack panel update layout.
            // The height and width of the first page will be used to initialize the rest of the document.
            this.imagePanel.UpdateLayout();
            image = (Image)this.FindName(PREFIX_PAGE + "1");
            this.defaultPageHeight = image.ActualHeight;
            AppEventSource.Log.Debug("ViewerPage: Default Page height set to " + this.defaultPageHeight.ToString());
            // Load page size file
            await LoadPageSizeFile();
            // Add blank pages for the rest of the file using the initialization timer
            this.initializationTimer.Start(); 
        }

        private async void FinishInitialization()
        {
            this.imagePanel.UpdateLayout();
            // Calculate the minimum zoom factor
            // SetMinZoomFactor();
            this.fullScreenCover.Visibility = Visibility.Collapsed;
            // Load viewer state
            await LoadViewerState();
            RestoreViewerState();
            // Retore inking
            await LoadInking();
            // Make sure about the visible page range
            this._visiblePageRange = FindVisibleRange();
            RefreshViewer();
            this.fileLoaded = true;
            this.fileLoadingWatch.Stop();
            AppEventSource.Log.Info("ViewerPage: Finished Preparing the file in " + fileLoadingWatch.Elapsed.TotalSeconds.ToString());

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
                    StorageFile pdfFileInList = await StorageApplicationPermissions.FutureAccessList.GetFileAsync(entry.Token);
                    if (this.pdfFile.IsEqual(pdfFileInList))
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
                        this.inkingFolder = await dataFolder.GetFolderAsync(INKING_FOLDER);
                        await LoadInking();
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
            this.isRenderingPage = true;
            while (renderPagesQueue.Count > 0)
            {
                int i = renderPagesQueue.Dequeue();
                // Load page at a higer resolution if it is zoomed in. Otherwise, load page at default resolution
                if (this.defaultPageWidth * this.scrollViewer.ZoomFactor > DEFAULT_RENDER_WIDTH && 
                    i >= VisiblePageRange.first && i <= VisiblePageRange.last)
                    await LoadPage(i, 2 * DEFAULT_RENDER_WIDTH);
                else
                    await LoadPage(i);
                // Add ink canvas
                LoadInkCanvas(i);
            }
            this.isRenderingPage = false;
        }

        private void RemovePage(int pageNumber)
        {
            if (pageNumber < inkingPageRange.first - SIZE_PAGE_BUFFER || pageNumber > inkingPageRange.last + SIZE_PAGE_BUFFER)
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
                InkCanvas inkCanvas = (InkCanvas)grid.FindName(PREFIX_CANVAS + pageNumber.ToString());
                if (inkCanvas.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
                {
                    // Remove old item in dictionary
                    this.inkingDictionary.Remove(pageNumber);
                    // Add to dictionary
                    this.inkingDictionary.Add(pageNumber, inkCanvas.InkPresenter.StrokeContainer);
                    AppEventSource.Log.Debug("ViewerPage: Ink strokes for page " + pageNumber.ToString() + " saved to dictionary.");
                }
                // Remove ink canvas
                if (removeAfterSave)
                {
                    grid.Children.RemoveAt(1);
                    this.inkCanvasList.Remove(pageNumber);
                }
            }
        }

        private async Task LoadPage(int pageNumber, uint renderWidth = DEFAULT_RENDER_WIDTH)
        {
            if (pageNumber <= 0 || pageNumber > this.pageCount) return;
            // Render pdf image
            Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
            if (image == null)
            {
                AppEventSource.Log.Warn("ViewerPage: Image container for page " + pageNumber.ToString() + " not found.");
                return;
            }
            // Check if page is already loaded, or re-render the page if a different resolution is specified.
            if (image.Source == null || renderWidth != this.renderWidthArray[pageNumber - 1])
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
                this.renderWidthArray[pageNumber - 1] = renderWidth;
                AppEventSource.Log.Debug("ViewerPage: Page " + pageNumber.ToString() + " loaded with render width " + renderWidth.ToString());
            }

            // Ajust image container height/width
            // The following code may be re-used later.
            //if (this.imagePanel.Orientation == Orientation.Vertical)
            //{
            //    image.Width = this.defaultPageWidth;
            //    image.Height = ((BitmapImage)(image.Source)).PixelHeight * this.defaultPageWidth / ((BitmapImage)(image.Source)).PixelWidth;
            //}
            //else
            //{
            //    if (defaultPageHeight > 0)
            //    {
            //        image.Height = this.defaultPageHeight;
            //        image.Width = ((BitmapImage)(image.Source)).PixelWidth * this.defaultPageHeight / ((BitmapImage)(image.Source)).PixelHeight;
            //    }
            //}

            // Adjust image container height (width will be the same for all pages)
            double newHeight = ((BitmapImage)(image.Source)).PixelHeight * this.defaultPageWidth / ((BitmapImage)(image.Source)).PixelWidth;
            if (Math.Abs(newHeight - image.Height) > 1)
            {
                image.Height = newHeight;
                if (this.pageSizeDictionary.ContainsKey(pageNumber))
                    this.pageSizeDictionary[pageNumber] = newHeight;
                else this.pageSizeDictionary.Add(pageNumber, newHeight);
                // Save the adjusted height/width to file
                await SavePageSizeFile();
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
            // Update the grid size to match the image size
            Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
            grid.Width = image.ActualWidth;
            grid.Height = image.ActualHeight;
            // Find existing ink canvas
            InkCanvas inkCanvas = (InkCanvas)grid.FindName(PREFIX_CANVAS + pageNumber.ToString());
            // If an ink canvas does not exist, add a new one
            if (inkCanvas == null)
            {
                // Add ink canvas
                inkCanvas = new InkCanvas();
                inkCanvas.Name = PREFIX_CANVAS + pageNumber.ToString();
                inkCanvas.InkPresenter.InputDeviceTypes = inkingPreference.drawingDevice;
                inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
                inkCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
                inkCanvas.InkPresenter.InputProcessingConfiguration.Mode = this.inkProcessMode;

                // Calculate ink canvas scale factor
                double inkCanvasScaleFactor = this.defaultPageWidth / inkingProfile.PageWidth;
                AppEventSource.Log.Debug("ViewerPage: Ink canvas scale factor for page " + pageNumber.ToString()
                    + " set to " + inkCanvasScaleFactor.ToString());
                // 
                inkCanvas.Height = image.ActualHeight / inkCanvasScaleFactor;
                inkCanvas.Width = image.ActualWidth / inkCanvasScaleFactor;

                // Load inking if exist
                InkStrokeContainer inkStrokeContainer;
                if (inkingDictionary.TryGetValue(pageNumber, out inkStrokeContainer))
                {
                    inkCanvas.InkPresenter.StrokeContainer = inkStrokeContainer;
                    AppEventSource.Log.Debug("ViewerPage: Ink strokes for page " + pageNumber.ToString() + " loaded from dictionary");
                }

                // Use viewbox to scale the ink canvas
                Viewbox viewbox = new Viewbox();
                viewbox.Name = PREFIX_VIEWBOX + pageNumber.ToString();
                viewbox.Child = inkCanvas;
                viewbox.Height = image.ActualHeight;
                viewbox.Width = image.ActualWidth;

                grid.Children.Add(viewbox);
                this.inkCanvasList.Add(pageNumber);
            }
            else
            {
                // Adjust viewbox size
                Viewbox viewbox = (Viewbox)grid.FindName(PREFIX_VIEWBOX + pageNumber.ToString());
                viewbox.Height = image.ActualHeight;
                viewbox.Width = image.ActualWidth;
            }
        }


        private async void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            this.inkingChanged = true;
            // Pause recycling when saving inking.
            if (!this.isSavingInking)
            {
                this.recycleTimer.Stop();
                while (this.inkingChanged)
                {
                    this.inkingChanged = false;
                    await SaveInking();
                }
                this.recycleTimer.Start();
            }
        }

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
            PreparePages(this.VisiblePageRange);
        }

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

        private PageRange FindVisibleRange()
        {
            // Find a visible page,
            int visiblePage = FindVisiblePage();
            if (visiblePage <= 0)
            {
                AppEventSource.Log.Warn("ViewerPage: Visible page is incorrect. page = " + visiblePage.ToString());
                return this.VisiblePageRange;
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

        private void SetMinZoomFactor()
        {
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
                Grid grid = new Grid();
                grid.Name = PREFIX_GRID + i.ToString();
                grid.Margin = pageMargin;
                Image image = new Image();
                image.Name = PREFIX_PAGE + i.ToString();
                image.Width = defaultPageWidth;
                if (this.pageSizeDictionary.ContainsKey(i))
                    image.Height = pageSizeDictionary[i];
                else image.Height = defaultPageHeight;
                grid.Children.Add(image);
                this.imagePanel.Children.Add(grid);
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
            while (this.recyclePagesQueue.Count > SIZE_RECYCLE_QUEUE)
            {
                RemovePage(this.recyclePagesQueue.Dequeue());
            }
            this.recycleTimer.Start();
        }

        private void pageNumberTextTimer_Tick(object sender, object e)
        {
            pageNumberTextTimer.Stop();
            this.pageNumberFadeOut.Begin();
        }

        private void scrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // Determine visible page range
            this._visiblePageRange = FindVisibleRange();
            // Show page number
            this.pageNumberTextBlock.Text = this.VisiblePageRange.last.ToString() + " / " + this.pageCount.ToString();
            this.pageNumberGrid.Opacity = PAGE_NUMBER_OPACITY;
            // The following code acts like a filter to prevent the timer ticking too frequently
            if (fileLoaded && !e.IsIntermediate)
            {
                refreshTimer.Stop();
                refreshTimer.Start();
                pageNumberTextTimer.Stop();
                pageNumberTextTimer.Start();
            }
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (fileLoaded)
            {
                // Store current zoom factor
                double factor = scrollViewer.ZoomFactor;
                // Recalculate min and max zoom factor
                //SetMinZoomFactor();
                // Recalculate offsets
                factor = scrollViewer.ZoomFactor / factor;
                scrollViewer.ChangeView(factor * scrollViewer.HorizontalOffset, 
                    factor * scrollViewer.VerticalOffset, scrollViewer.ZoomFactor,true);
                AppEventSource.Log.Debug("ViewerPage: Window size changed, offsets recalculated.");
            }
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
            this.drawingAttributes.Size = new Size(inkingPreference.penSize, inkingPreference.penSize);
            this.drawingAttributes.Color = inkingPreference.penColor;
            this.drawingAttributes.PenTip = PenTipShape.Circle;
            this.drawingAttributes.DrawAsHighlighter = false;
            this.drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.Identity;
            this.inkProcessMode = InkInputProcessingMode.Inking;
            UpdateInkPresenter();
        }

        private void Highlighter_Click(object sender, RoutedEventArgs e)
        {
            ClearInputTypeToggleBtn();
            AppEventSource.Log.Debug("ViewerPage: Highlighter selected");
            this.Highlighter.IsChecked = true;
            this.drawingAttributes.Size = new Size(inkingPreference.highlighterSize, inkingPreference.highlighterSize);
            this.drawingAttributes.Color = inkingPreference.highlighterColor;
            this.drawingAttributes.PenTip = PenTipShape.Rectangle;
            this.drawingAttributes.DrawAsHighlighter = true;
            this.drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.Identity;
            this.inkProcessMode = InkInputProcessingMode.Inking;
            UpdateInkPresenter();
        }

        private void Eraser_Click(object sender, RoutedEventArgs e)
        {
            ClearInputTypeToggleBtn();
            AppEventSource.Log.Debug("ViewerPage: Eraser selected");
            this.Eraser.IsChecked = true;
            this.drawingAttributes.Size = new Size(inkingPreference.penSize, inkingPreference.penSize);
            this.drawingAttributes.PenTip = PenTipShape.Circle;
            this.drawingAttributes.DrawAsHighlighter = false;
            this.drawingAttributes.PenTipTransform = System.Numerics.Matrix3x2.Identity;
            this.inkProcessMode = InkInputProcessingMode.Erasing;
            UpdateInkPresenter();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            NavigationPage.Current.RemoveView(this.ViewerKey);
        }

        private async void CloseAll_Click(object sender, RoutedEventArgs e)
        {
            SuspensionManager.viewerStateDictionary = new Dictionary<Guid, ViewerState>();
            SuspensionManager.viewerStateDictionary.Add(Guid.NewGuid(), null);
            await SuspensionManager.SaveViewerAsync();
            SuspensionManager.sessionState.FileToken = null;
            this.fileLoaded = false;
            InitializeViewer();
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
            ClearViewModeToggleBtn();
            this.VerticalViewBtn.IsChecked = true;
            if (imagePanel.Orientation != Orientation.Vertical)
            {
                this.imagePanel.Orientation = Orientation.Vertical;
                NavigationPage.Current.UpdateViewBtn(this.ViewerKey, this.VisiblePageRange.ToString(), Symbol.Page2);
                AppEventSource.Log.Debug("ViewerPage: View " + ViewerKey.ToString() + "Changed to Vertical View.");
                RefreshViewer();
            }
        }

        private void HorizontalView_Click(object sender, RoutedEventArgs e)
        {
            ClearViewModeToggleBtn();
            this.HorizontalViewBtn.IsChecked = true;
            if (imagePanel.Orientation != Orientation.Horizontal)
            {
                this.imagePanel.Orientation = Orientation.Horizontal;
                NavigationPage.Current.UpdateViewBtn(this.ViewerKey, this.VisiblePageRange.ToString(), Symbol.TwoPage);
                AppEventSource.Log.Debug("ViewerPage: View " + ViewerKey.ToString() + "Changed to Horizontal View.");
                RefreshViewer();
            }
        }

        private void GridView_Click(object sender, RoutedEventArgs e)
        {
            ClearViewModeToggleBtn();
            this.GridViewBtn.IsChecked = true;
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
                    try
                    {
                        foreach (int pageNumber in dialog.PagesToExport)
                        {
                            exportingPageNumber = pageNumber;
                            string filename = dialog.ImageFilename;
                            string fileExtension = ".PNG";
                            StorageFile file = await folder.CreateFileAsync(filename + pageNumber.ToString() + fileExtension,
                                CreationCollisionOption.GenerateUniqueName);
                            await Export_Page(pageNumber, file);
                        }
                    }
                    catch (Exception ex)
                    {
                        NotifyUser("An error occurred when exporting page " + exportingPageNumber.ToString() + ".\n" + ex.Message, true);
                    }
                }
            }
        }

        /// <summary>
        /// Save a rendered pdf page with inking to png file.
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="saveFile"></param>
        /// <returns></returns>
        private async Task Export_Page(int pageNumber, StorageFile saveFile)
        {
            Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
            InkCanvas inkCanvas = (InkCanvas)this.imagePanel.FindName(PREFIX_CANVAS + pageNumber.ToString());
            CanvasDevice device = CanvasDevice.GetSharedDevice();
            CanvasRenderTarget renderTarget = new CanvasRenderTarget(device, (int)inkCanvas.ActualWidth, (int)inkCanvas.ActualHeight, 96 * 2);

            // Render pdf page
            InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
            PdfPage page = pdfDocument.GetPage(Convert.ToUInt32(pageNumber - 1));
            PdfPageRenderOptions options = new PdfPageRenderOptions();
            options.DestinationWidth = (uint)inkCanvas.ActualWidth * 2;
            await page.RenderToStreamAsync(stream, options);
            CanvasBitmap bitmap = await CanvasBitmap.LoadAsync(device, stream, 96 * 2);
            // Draw image with ink
            using (var ds = renderTarget.CreateDrawingSession())
            {
                ds.Clear(Colors.White);
                ds.DrawImage(bitmap);
                ds.DrawInk(inkCanvas.InkPresenter.StrokeContainer.GetStrokes());
            }

            // Encode the image to the selected file on disk
            using (var fileStream = await saveFile.OpenAsync(FileAccessMode.ReadWrite))
                await renderTarget.SaveAsync(fileStream, CanvasBitmapFileFormat.Png, 1f);
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
            }
        }

        /// <summary>
        /// Show a dialog with notification message.
        /// </summary>
        /// <param name="message">The message to be displayed to the user.</param>
        private async void NotifyUser(string message, bool logMessage = false)
        {
            MessageDialog messageDialog = new MessageDialog(message);
            messageDialog.Commands.Add(new UICommand("OK", null, 0));
            await messageDialog.ShowAsync();
            if (logMessage) AppEventSource.Log.Error("ViewerPage: " + message);
        }
    }
}
