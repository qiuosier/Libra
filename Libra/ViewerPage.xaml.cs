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
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using Windows.Storage.AccessCache;
using System.IO;
using NavigationMenu;
using Windows.Storage.Pickers;
using Microsoft.Graphics.Canvas;
using Windows.UI;
using Libra.Class;
using Windows.UI.Xaml.Data;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Media;

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
        private const int FIRST_LOAD_PAGES = 2;
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
        private Queue<int> inkingChangedPagesQueue;
        private Dictionary<int, InkStrokeContainer> inkingDictionary;
        private List<int> inkCanvasList;
        private InkDrawingAttributes drawingAttributes;
        private InkingPreference inkingPreference;
        private InkInputProcessingMode inkProcessMode;
        private System.Diagnostics.Stopwatch fileLoadingWatch;

        private Guid _viewerKey;
        public Guid ViewerKey { get { return this._viewerKey; } }

        private int pageCount;
        private bool fileLoaded;
        private bool isSavingInking;
        private bool isRenderingPage;
        private string futureAccessToken;

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
            this.inkingDictionary = new Dictionary<int, InkStrokeContainer>();
            this.inkCanvasList = new List<int>();
            this.recyclePagesQueue = new Queue<int>();
            this.renderPagesQueue = new Queue<int>();
            this.inkingChangedPagesQueue = new Queue<int>();
            this.isRenderingPage = false;
            this.inkProcessMode = InkInputProcessingMode.Inking;
            this.recycleTimer.Stop();
            this.fullScreenCover.Visibility = Visibility.Visible;
            this.fullScreenMessage.Text = DEFAULT_FULL_SCREEN_MSG;
            this.pageNumberTextBlock.Text = "";
            this.filenameTextBlock.Text = "";
            this.imagePanel.Orientation = Orientation.Vertical;
            this.semanticZoom.IsZoomedInViewActive = true;
            this.pageThumbnails = null;
            if (NavigationPage.Current != null) NavigationPage.Current.InitializeViewBtn();
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
                InitializeZoomInView();
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
                    // Restore the previous view
                    this.scrollViewer.ChangeView(viewerState.hOffset, viewerState.vOffset, viewerState.zFactor);
                    AppEventSource.Log.Info("ViewerPage: Viewer state restored. " + this.pdfFile.Name);
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
                / (this.pdfDocument.GetPage(0).Size.Width + 2 * PAGE_IMAGE_MARGIN + SCROLLBAR_WIDTH));
            double hOffset = this.imagePanel.ActualWidth * zoomFactor - this.scrollViewer.ActualWidth;
            hOffset = hOffset > 0 ? hOffset / 2 : 0;
            return this.scrollViewer.ChangeView(hOffset, 0, zoomFactor);
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
            if (SuspensionManager.viewerStateDictionary == null || SuspensionManager.viewerStateDictionary.Count == 0)
            {
                SuspensionManager.viewerStateDictionary = new Dictionary<Guid, ViewerState>();
                ViewerState viewerState = SaveViewerState();
                viewerState.hOffset = this.scrollViewer.ScrollableWidth / 2;
                viewerState.vOffset = 0;
                SuspensionManager.viewerStateDictionary.Add(Guid.NewGuid(), viewerState);
            }
            // Create navigation buttons for the views
            NavigationPage.Current.InitializeViewBtn();
        }

        /// <summary>
        /// Save the inkstrokes from in each ink canvas to inking dictionary, and
        /// save the inking dictionary to files in local app data folder.
        /// Inking for each page will be saved in a individual file.
        /// However, this method will save the inking for all pages,
        /// whether the inking has been modified or not.
        /// </summary>
        /// <returns></returns>
        private async Task SaveInkingDictionary()
        {
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
            this.isSavingInking = false;
        }

        private async Task SaveInkingQueue()
        {
            this.isSavingInking = true;
            while (this.inkingChangedPagesQueue.Count > 0)
            {
                int pageNumber = this.inkingChangedPagesQueue.Dequeue();
                // Save ink strokes to dictionary
                SaveInkCanvas(pageNumber);
                // Save inking to file
                try
                {
                    StorageFile inkFile = await this.inkingFolder.CreateFileAsync(
                        pageNumber.ToString() + EXT_INKING, CreationCollisionOption.ReplaceExisting);
                    using (IRandomAccessStream inkStream = await inkFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        InkStrokeContainer inkStrokeContainer;
                        if (inkingDictionary.TryGetValue(pageNumber, out inkStrokeContainer))
                        {
                            await inkStrokeContainer.SaveAsync(inkStream);
                        }
                    }
                    AppEventSource.Log.Debug("ViewerPage: Inking for page " + pageNumber + " saved.");
                }
                catch (Exception ex)
                {
                    NotifyUser("An error occurred when saving inking. \n" + ex.Message, true);
                }
            }
            this.isSavingInking = false;
        }

        /// <summary>
        /// Load inking from files to inking dictionary.
        /// </summary>
        /// <returns></returns>
        private async Task LoadInking()
        {
            System.Diagnostics.Stopwatch inkingLoadingWatch = new System.Diagnostics.Stopwatch();
            inkingLoadingWatch.Start();
            AppEventSource.Log.Debug("ViewerPage: Checking inking for " + this.pdfFile.Name);
            // TODO: Need to check if the inking is suitable for the file/page.
            //
            //
            this.inkingDictionary = new Dictionary<int, InkStrokeContainer>();
            foreach (StorageFile inkFile in await inkingFolder.GetFilesAsync())
            {
                int pageNumber = 0;
                try
                {
                    pageNumber = Convert.ToInt32(inkFile.Name.Substring(0, inkFile.Name.Length - 4));
                    InkStrokeContainer inkStrokeContainer = new InkStrokeContainer();
                    using (var inkStream = await inkFile.OpenSequentialReadAsync())
                    {
                        await inkStrokeContainer.LoadAsync(inkStream);
                    }
                    this.inkingDictionary.Add(pageNumber, inkStrokeContainer);
                    AppEventSource.Log.Debug("ViewerPage: Inking for page " + pageNumber.ToString() + " loaded.");
                }
                catch (Exception e)
                {
                    string errorMsg = "Error when loading inking for page " + pageNumber.ToString() + "\n Exception: " + e.Message;
                    AppEventSource.Log.Error("ViewerPage: " + errorMsg);
                    int userResponse = await ShowMessageDialog(errorMsg, new string[] { "Remove Inking", "Ignore" });
                    switch (userResponse)
                    {
                        case 0: // Delete inking file
                            await inkFile.DeleteAsync();
                            AppEventSource.Log.Error("ViewerPage: File deleted.");
                            break;
                        default: break;
                    }
                    return;
                }
            }
            inkingLoadingWatch.Stop();
            AppEventSource.Log.Info("ViewerPage: Inking loaded in " + inkingLoadingWatch.Elapsed.TotalSeconds.ToString() + " seconds.");
        }

        /// <summary>
        /// Save inking preference to file.
        /// </summary>
        /// <returns></returns>
        private async Task SaveDrawingPreference()
        {
            AppEventSource.Log.Debug("ViewerPage: Saving drawing preference...");
            StorageFile file = await
                ApplicationData.Current.LocalFolder.CreateFileAsync(INKING_PREFERENCE_FILENAME, CreationCollisionOption.ReplaceExisting);
            await SuspensionManager.SerializeToFileAsync(this.inkingPreference, typeof(InkingPreference), file);
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
            this.drawingAttributes.IgnorePressure = false;
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
            AppEventSource.Log.Info("ViewerPage: Finished loading the file in " + fileLoadingWatch.Elapsed.TotalSeconds.ToString());
            // Total number of pages
            this.pageCount = (int)pdfDocument.PageCount;
            AppEventSource.Log.Debug("ViewerPage: Total pages: " + this.pageCount.ToString());
            // Zoom the first page to fit the viewer window width
            ResetViewer();
            // Load drawing preference
            await LoadDrawingPreference();
            // Initialize thumbnails collection
            this.pageThumbnails = new PageCollection(this.pdfDocument);
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
            image.Width = this.pdfDocument.GetPage((uint)(pageNumber - 1)).Size.Width;
            image.Height = this.pdfDocument.GetPage((uint)(pageNumber - 1)).Size.Height;
            grid.Children.Add(image);
            this.imagePanel.Children.Add(grid);
            // Add blank thumbnail
            this.pageThumbnails.Add(new PageDetail(pageNumber, image.Height, image.Width));
        }

        private async void FinishInitialization()
        {
            this.fullScreenCover.Visibility = Visibility.Collapsed;
            // Load viewer state
            await LoadViewerState();
            RestoreViewerState();
            // Retore inking
            await LoadInking();
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
            // Set isRenderingPage to true to prevent this method to be called multiple times before the rendering is finished.
            this.isRenderingPage = true;
            while (renderPagesQueue.Count > 0)
            {
                int i = renderPagesQueue.Dequeue();
                Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + i.ToString());
                // The actual page width that is diaplaying to the user
                double displayWidth = this.pdfDocument.GetPage((uint)(i-1)).Size.Width * this.scrollViewer.ZoomFactor;
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
                if (i >= VisiblePageRange.first && i <= VisiblePageRange.last)
                    await AddPageImage(i, renderWidth);
                else
                    await AddPageImage(i, Math.Min(renderWidth, (uint)(standardWidth)));
                // Add ink canvas
                LoadInkCanvas(i);
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
        private void RemovePage(int pageNumber)
        {
            if (pageNumber < inkingPageRange.first - SIZE_PAGE_BUFFER || pageNumber > inkingPageRange.last + SIZE_PAGE_BUFFER)
            {
                // Remove Ink Canvas
                SaveInkCanvas(pageNumber, true);
                // Remove Image
                Image image = (Image)this.imagePanel.FindName(PREFIX_PAGE + pageNumber.ToString());
                if (image != null)
                {
                    double x = image.ActualHeight;
                    image.Source = null;
                    image.Height = x;
                    AppEventSource.Log.Debug("ViewerPage: Image in page " + pageNumber.ToString() + " removed.");
                }
                else AppEventSource.Log.Warn("ViewerPage: Image in page " + pageNumber.ToString() + " is empty.");
            }
        }

        /// <summary>
        /// Save ink canvas to inking dictionary.
        /// </summary>
        /// <param name="pageNumber"></param>
        /// <param name="removeAfterSave"></param>
        private void SaveInkCanvas(int pageNumber, bool removeAfterSave = false)
        {
            Grid grid = (Grid)this.imagePanel.Children[pageNumber - 1];
            if (grid.Children.Count > 1)    // No inkcanvas if count < 1
            {
                // Save ink strokes, if there is any
                InkCanvas inkCanvas = (InkCanvas)grid.FindName(PREFIX_CANVAS + pageNumber.ToString());
                if (inkCanvas.InkPresenter.StrokeContainer.GetStrokes().Count > 0)
                {
                    // Remove item in dictionary, it will return false if item not found
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

        private async Task AddPageImage(int pageNumber, uint renderWidth)
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
            if (image.Source == null || 
                renderWidth > ((BitmapImage)(image.Source)).PixelWidth ||
                renderWidth < ((BitmapImage)(image.Source)).PixelWidth / 2)
            {
                image.Source = await RenderPageImage(pageNumber, renderWidth);
                AppEventSource.Log.Debug("ViewerPage: Page " + pageNumber.ToString() + " loaded with render width " + renderWidth.ToString());
            }
        }

        private async Task<BitmapImage> RenderPageImage(int pageNumber, uint renderWidth)
        {
            // Render pdf image
            InMemoryRandomAccessStream stream = new InMemoryRandomAccessStream();
            PdfPage page = pdfDocument.GetPage(Convert.ToUInt32(pageNumber - 1));
            PdfPageRenderOptions options = new PdfPageRenderOptions();
            options.DestinationWidth = renderWidth;
            await page.RenderToStreamAsync(stream, options);
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.SetSource(stream);
            return bitmapImage;
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
                Binding bindingHeight = new Binding();
                bindingHeight.Mode = BindingMode.OneWay;
                bindingHeight.Path = new PropertyPath("ActualHeight");
                bindingHeight.Source = image;
                Binding bindingWidth = new Binding();
                bindingWidth.Mode = BindingMode.OneWay;
                bindingWidth.Path = new PropertyPath("ActualWidth");
                bindingWidth.Source = image;
                // Add ink canvas
                inkCanvas = new InkCanvas();
                inkCanvas.Name = PREFIX_CANVAS + pageNumber.ToString();
                inkCanvas.InkPresenter.InputDeviceTypes = inkingPreference.drawingDevice;
                inkCanvas.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
                inkCanvas.InkPresenter.StrokesCollected += InkPresenter_StrokesCollected;
                inkCanvas.InkPresenter.StrokesErased += InkPresenter_StrokesErased;
                inkCanvas.InkPresenter.InputProcessingConfiguration.Mode = this.inkProcessMode;
                inkCanvas.SetBinding(HeightProperty, bindingHeight);
                inkCanvas.SetBinding(WidthProperty, bindingWidth);
                // Load inking if exist
                InkStrokeContainer inkStrokeContainer;
                if (inkingDictionary.TryGetValue(pageNumber, out inkStrokeContainer))
                {
                    inkCanvas.InkPresenter.StrokeContainer = inkStrokeContainer;
                    AppEventSource.Log.Debug("ViewerPage: Ink strokes for page " + pageNumber.ToString() + " loaded from dictionary");
                }
                // Add ink canvas page
                grid.Children.Add(inkCanvas);
                this.inkCanvasList.Add(pageNumber);
            }
        }

        private void InkPresenter_StrokesErased(InkPresenter sender, InkStrokesErasedEventArgs args)
        {
            InkPresenter_StrokesChanged(sender);
        }

        private async void InkPresenter_StrokesCollected(InkPresenter sender, InkStrokesCollectedEventArgs args)
        {
            // Notify user about the risk when using inking for the first time.
            if ((bool)App.AppSettings[App.INKING_WARNING])
            {
                int userResponse = await ShowMessageDialog("Ink strokes collection is an experimental feature. \n" +
                    "Currently ink strokes are NOT SAVED to the PDF file. They are saved ONLY IN THIS APP. \n" +
                    "You can export the ink strokes along with pdf pages as image file.",
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
            InkPresenter_StrokesChanged(sender);
        }

        /// <summary>
        /// Save inking if strokes are changed.
        /// </summary>
        /// <param name="sender"></param>
        private async void InkPresenter_StrokesChanged(InkPresenter sender)
        {
            // Find the page number
            int pageNumber = 0;
            foreach (int i in inkCanvasList)
            {
                InkCanvas inkCanvas = (InkCanvas)this.imagePanel.FindName(PREFIX_CANVAS + i.ToString());
                if (inkCanvas.InkPresenter == sender) pageNumber = i;
            }
            if (pageNumber == 0)
            {
                AppEventSource.Log.Error("ViewerPage: Strokes Changed but Ink canvas found.");
                return;
            }
            // Enqueue the page if it is not already in the queue
            if (!this.inkingChangedPagesQueue.Contains(pageNumber))
                this.inkingChangedPagesQueue.Enqueue(pageNumber);
            // Invoke save inking only if SaveInking is not running.
            // This will prevent running multiple saving instance at the same time.
            if (!this.isSavingInking)
                await SaveInkingQueue();
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
            // Avoid recycling when saving inking
            while (!this.isSavingInking && this.recyclePagesQueue.Count > SIZE_RECYCLE_QUEUE)
            {
                RemovePage(this.recyclePagesQueue.Dequeue());
            }
            this.recycleTimer.Start();
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

        private async void Eraser_Click(object sender, RoutedEventArgs e)
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
            // Notify user about the risk when using eraser for the first time.
            if ((bool)App.AppSettings[App.INKING_WARNING])
            {
                int userResponse = await ShowMessageDialog("Eraser deletes the entire stroke. \n" +
                    "Eraser operation cannot be undo. \n" +
                    "Please use with care. ",
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
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            NavigationPage.Current.RemoveView(this.ViewerKey);
        }

        public void AllViewClosed()
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
            SuspensionManager.viewerStateDictionary = null;
            await SuspensionManager.SaveViewerAsync();
            SuspensionManager.sessionState.FileToken = null;
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
            for (uint i = 0; i < pageIndex; i++)
            {
                pageOffset += this.imagePanel.Orientation == Orientation.Vertical ?
                    this.pdfDocument.GetPage(i).Size.Height :
                    this.pdfDocument.GetPage(i).Size.Width;
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

        private async Task<int> ShowMessageDialog(string errorMsg, string[] optionsLabel)
        {
            MessageDialog messageDialog = new MessageDialog(errorMsg);
            foreach (string label in optionsLabel)
            {
                messageDialog.Commands.Add(new UICommand(label, null, messageDialog.Commands.Count));
            }
            IUICommand command = await messageDialog.ShowAsync();
            return (int)command.Id;
        }

        private void GoToPage_Click(object sender, RoutedEventArgs e)
        {

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

        private PageCollection pageThumbnails;

        /// <summary>
        /// Record the clicked item
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ThumbnailGrid_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            this.pageThumbnails.SelectedIndex = (int)(((PageDetail)((Grid)sender).DataContext).PageNumber - 1
                    - (this.pdfDocument.PageCount - this.pageThumbnails.Count));
        }
    }
}
