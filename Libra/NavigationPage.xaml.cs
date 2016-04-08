using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml.Automation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Navigation;
using NavigationMenu.Controls;
using Libra;
using System.Collections.ObjectModel;
using System;
using Libra.Class;

namespace NavigationMenu
{
    /// <summary>
    /// The "chrome" layer of the app that provides top-level navigation with
    /// proper keyboarding navigation.
    /// </summary>
    public sealed partial class NavigationPage : Page
    {
        private const string ARG_ADD_NEW_VIEW = "AddNewView";
        private const int NAV_LIST_STATIC_BTN_COUNT = 2;
        private const int HIGHLIGHT_BTN_TIMER_TICKS = 750 * 10000;

        private DispatcherTimer highlightBtnTimer;

        // Declare the top level nav items
        private ObservableCollection<NavMenuItem> navlist = new ObservableCollection<NavMenuItem>(
            new[]
            {
                new NavMenuItem()
                {
                    Symbol = Symbol.Home,
                    Label = "Home",
                    DestPage = typeof(MainPage)
                    //Arguments = this;
                },
                new NavMenuItem()
                {
                    Symbol = Symbol.Setting,
                    Label = "Settings",
                    DestPage = typeof(SettingsPage)
                },
            });

        public void InitializeViewBtn()
        {
            // Clean up the buttons
            while(this.navlist.Count > NAV_LIST_STATIC_BTN_COUNT)
            {
                this.navlist.RemoveAt(NAV_LIST_STATIC_BTN_COUNT);
            }

            // Go through the viewer states
            if (SuspensionManager.viewerStateDictionary == null) return;
            foreach (KeyValuePair<Guid, ViewerState> entry in SuspensionManager.viewerStateDictionary)
            {
                if (entry.Value == null)
                {
                    // Viewer state is null
                    this.navlist.Add(new NavMenuItem()
                    {
                        Symbol = Symbol.Page2,
                        Label = "Page 1",
                        DestPage = typeof(ViewerPage),
                        Arguments = entry.Key
                    });
                }
                else
                {
                    // Viewer state is not null
                    string btnLabel = "View";
                    if (entry.Value.visibleRange != null)
                        btnLabel = entry.Value.visibleRange.ToString();
                    this.navlist.Add(new NavMenuItem()
                    {
                        Symbol = entry.Value.isHorizontalView ? Symbol.TwoPage : Symbol.Page2,
                        Label = btnLabel,
                        DestPage = typeof(ViewerPage),
                        Arguments = entry.Key
                    });
                }
            }
            
            // Add an ADD VIEW button if a file is opened
            if (this.navlist.Count > NAV_LIST_STATIC_BTN_COUNT)
            {
                this.navlist.Add(new NavMenuItem()
                {
                    Symbol = Symbol.Add,
                    Label = "Add a New View",
                    DestPage = null,
                    Arguments = ARG_ADD_NEW_VIEW
                });
            }
        }

        private void AddNewView()
        {
            // Generate a new GUID
            Guid viewKey = Guid.NewGuid();
            // Insert a button in the navigation menu
            this.navlist.Insert(this.navlist.Count - 1, new NavMenuItem()
            {
                Symbol = Symbol.Page2,
                Label = "Page 1",
                DestPage = typeof(ViewerPage),
                Arguments = viewKey
            });
            // Add a empty viewer state
            SuspensionManager.viewerStateDictionary.Add(viewKey, null);
            // Navigate to the new view
            this.AppFrame.Navigate(typeof(ViewerPage), viewKey);
        }

        /// <summary>
        /// Remove a view based on the GUID key
        /// </summary>
        /// <param name="viewKey"></param>
        public void RemoveView(Guid viewKey)
        {
            // Remove the button in the navigation menu
            int i = FindNavListIndexByKey(viewKey);
            if (i > 0) this.navlist.RemoveAt(i);
            // Remove the viewer state in the suspension manager
            SuspensionManager.viewerStateDictionary.Remove(viewKey);
            // Navigate to last view, if there is still any
            if (SuspensionManager.viewerStateDictionary.Count > 0)
                this.AppFrame.Navigate(typeof(ViewerPage));
            // Otherwise navigate to main page
            else ViewerPage.Current.CloseAllViews();
        }

        public void UpdateViewBtn(Guid viewKey, string newLabel = null, Symbol newSymbol = 0)
        {
            if (newLabel != null)
            {
                // Find the button corresponding to the key
                int i = FindNavListIndexByKey(viewKey);
                // Do nothing if key not found.
                if (i > 0)
                {
                    // Use the same symbol is a new symbol is not specified
                    if (newSymbol == 0)
                        newSymbol = this.navlist[i].Symbol;
                    // Remove the button from the list
                    this.navlist.RemoveAt(i);
                    // Add a new button with the new label
                    this.navlist.Insert(i, new NavMenuItem()
                    {
                        Symbol = newSymbol,
                        Label = newLabel,
                        DestPage = typeof(ViewerPage),
                        Arguments = viewKey
                    });
                }
            }
            // Highlight the navigation button for the selected view.
            // Since the buttons may not be initialized at this time, a timer is started to check the button when it ticks.
            this.highlightBtnTimer.Start();
        }

        /// <summary>
        /// Find the index of a button in the navigation menu (navlist) by the GUID of the view
        /// </summary>
        /// <param name="viewKey">The GUID of a view</param>
        /// <returns>Index of the button associated with the view</returns>
        private int FindNavListIndexByKey(Guid viewKey)
        {
            for (int i = NAV_LIST_STATIC_BTN_COUNT; i < this.navlist.Count - 1; i++)
            {
                if ((Guid)(this.navlist[i].Arguments) == viewKey)
                {
                    return i;
                }
            }
            AppEventSource.Log.Warn("NavigationPage: Button to not found, GUID = " + viewKey.ToString());
            return -1;
        }

        public static NavigationPage Current = null;
        
        /// <summary>
        /// Initializes a new instance of the AppShell, sets the static 'Current' reference,
        /// adds callbacks for Back requests and changes in the SplitView's DisplayMode, and
        /// provide the nav menu list with the data to display.
        /// </summary>
        public NavigationPage()
        {
            this.InitializeComponent();

            this.Loaded += (sender, args) =>
            {
                Current = this;

                this.TogglePaneButton.Focus(FocusState.Programmatic);
            };

            this.RootSplitView.RegisterPropertyChangedCallback(
                SplitView.DisplayModeProperty,
                (s, a) =>
                {
                    // Ensure that we update the reported size of the TogglePaneButton when the SplitView's
                    // DisplayMode changes.
                    this.CheckTogglePaneButtonSizeChanged();
                });

            SystemNavigationManager.GetForCurrentView().BackRequested += SystemNavigationManager_BackRequested;

            // If on a phone device that has hardware buttons then we hide the app's back button.
            //if (ApiInformation.IsTypePresent("Windows.Phone.UI.Input.HardwareButtons"))
            //{
                this.BackButton.Visibility = Visibility.Collapsed;
            //}

            NavMenuList.ItemsSource = navlist;

            this.highlightBtnTimer = new DispatcherTimer();
            this.highlightBtnTimer.Tick += HighlightBtnTimer_Tick;
            this.highlightBtnTimer.Interval = new TimeSpan(HIGHLIGHT_BTN_TIMER_TICKS);
        }

        // Use this counter to prevent the timer running forever in case something is wrong.
        private int HighlightBtnTickCounter = 0;
        private void HighlightBtnTimer_Tick(object sender, object e)
        {
            if (this.AppFrame.CurrentSourcePageType != typeof(ViewerPage) 
                || HighlightViewBtn(ViewerPage.Current.ViewerKey)
                || HighlightBtnTickCounter > 10)
            {
                // Stop the timeer if the button is successfully highlighted.
                this.highlightBtnTimer.Stop();
                HighlightBtnTickCounter = 0;
            }
            else HighlightBtnTickCounter++;
        }

        public Frame AppFrame { get { return this.frame; } }

        /// <summary>
        /// Default keyboard focus movement for any unhandled keyboarding
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AppShell_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            FocusNavigationDirection direction = FocusNavigationDirection.None;
            switch (e.Key)
            {
                case Windows.System.VirtualKey.Left:
                case Windows.System.VirtualKey.GamepadDPadLeft:
                case Windows.System.VirtualKey.GamepadLeftThumbstickLeft:
                case Windows.System.VirtualKey.NavigationLeft:
                    direction = FocusNavigationDirection.Left;
                    break;
                case Windows.System.VirtualKey.Right:
                case Windows.System.VirtualKey.GamepadDPadRight:
                case Windows.System.VirtualKey.GamepadLeftThumbstickRight:
                case Windows.System.VirtualKey.NavigationRight:
                    direction = FocusNavigationDirection.Right;
                    break;

                case Windows.System.VirtualKey.Up:
                case Windows.System.VirtualKey.GamepadDPadUp:
                case Windows.System.VirtualKey.GamepadLeftThumbstickUp:
                case Windows.System.VirtualKey.NavigationUp:
                    direction = FocusNavigationDirection.Up;
                    break;

                case Windows.System.VirtualKey.Down:
                case Windows.System.VirtualKey.GamepadDPadDown:
                case Windows.System.VirtualKey.GamepadLeftThumbstickDown:
                case Windows.System.VirtualKey.NavigationDown:
                    direction = FocusNavigationDirection.Down;
                    break;
            }

            if (direction != FocusNavigationDirection.None)
            {
                var control = FocusManager.FindNextFocusableElement(direction) as Control;
                if (control != null)
                {
                    control.Focus(FocusState.Programmatic);
                    e.Handled = true;
                }
            }
        }

        #region BackRequested Handlers

        private void SystemNavigationManager_BackRequested(object sender, BackRequestedEventArgs e)
        {
            bool handled = e.Handled;
            this.BackRequested(ref handled);
            e.Handled = handled;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            bool ignored = false;
            this.BackRequested(ref ignored);
        }

        private void BackRequested(ref bool handled)
        {
            // Get a hold of the current frame so that we can inspect the app back stack.

            if (this.AppFrame == null)
                return;

            // Check to see if this is the top-most page on the app back stack.
            if (this.AppFrame.CanGoBack && !handled)
            {
                // If not, set the event to handled and go back to the previous page in the app.
                handled = true;
                this.AppFrame.GoBack();
            }
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Navigate to the Page for the selected <paramref name="listViewItem"/>.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="listViewItem"></param>
        private void NavMenuList_ItemInvoked(object sender, ListViewItem listViewItem)
        {
            var item = (NavMenuItem)((NavMenuListView)sender).ItemFromContainer(listViewItem);
            
            if (item != null)
            {
                if (item.DestPage == null)
                {
                    // The ADD button is clicked, add a new view
                    if ((string)item.Arguments == ARG_ADD_NEW_VIEW)
                    {
                        // Add a new item to the navigation list
                        AddNewView();
                    }
                }
                else if (item.DestPage == typeof(ViewerPage) && (Guid)item.Arguments != ViewerPage.Current.ViewerKey)
                {
                    this.AppFrame.Navigate(item.DestPage, item.Arguments);
                }
                else if (item.DestPage != this.AppFrame.CurrentSourcePageType)
                {
                    // Reset viewer mode
                    if (SuspensionManager.sessionState != null)
                        SuspensionManager.sessionState.ViewerMode = 0;
                    this.AppFrame.Navigate(item.DestPage, item.Arguments);
                }
            }
        }

        /// <summary>
        /// Ensures the nav menu reflects reality when navigation is triggered outside of
        /// the nav menu buttons.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnNavigatingToPage(object sender, NavigatingCancelEventArgs e)
        {
            //if (e.NavigationMode == NavigationMode.Back)
            if (e.SourcePageType == typeof(ViewerPage)) return;
            var item = (from p in this.navlist where p.DestPage == e.SourcePageType select p).SingleOrDefault();
            //if (item == null && this.AppFrame.BackStackDepth > 0)
            //{
            //    // In cases where a page drills into sub-pages then we'll highlight the most recent
            //    // navigation menu item that appears in the BackStack
            //    foreach (var entry in this.AppFrame.BackStack.Reverse())
            //    {
            //        item = (from p in this.navlist where p.DestPage == entry.SourcePageType select p).SingleOrDefault();
            //        if (item != null)
            //            break;
            //    }
            //}
            HighlightNavigationBtn((ListViewItem)NavMenuList.ContainerFromItem(item));
        }

        /// <summary>
        /// Highlight a navigation button for a view or page.
        /// </summary>
        /// <param name="container"></param>
        /// <returns>True if the highlight is successful.</returns>
        private bool HighlightNavigationBtn(ListViewItem container)
        {
            // While updating the selection state of the item prevent it from taking keyboard focus.  If a
            // user is invoking the back button via the keyboard causing the selected nav menu item to change
            // then focus will remain on the back button.
            if (container != null)
            {
                container.IsTabStop = false;
                NavMenuList.SetSelectedItem(container);
                container.IsTabStop = true;
                return true;
            }
            else return false;
        }

        private bool HighlightViewBtn(Guid viewKey)
        {
            // Find the button corresponding to the key
            int i = FindNavListIndexByKey(viewKey);
            if (i > 0)
                return HighlightNavigationBtn((ListViewItem)NavMenuList.ContainerFromIndex(i));
            else
                return false;
        }

        private void OnNavigatedToPage(object sender, NavigationEventArgs e)
        {
            // After a successful navigation set keyboard focus to the loaded page
            if (e.Content is Page && e.Content != null)
            {
                var control = (Page)e.Content;
                control.Loaded += Page_Loaded;
            }
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ((Page)sender).Focus(FocusState.Programmatic);
            ((Page)sender).Loaded -= Page_Loaded;
            this.CheckTogglePaneButtonSizeChanged();
        }

        #endregion

        public Rect TogglePaneButtonRect
        {
            get;
            private set;
        }

        /// <summary>
        /// An event to notify listeners when the hamburger button may occlude other content in the app.
        /// The custom "PageHeader" user control is using this.
        /// </summary>
        public event TypedEventHandler<NavigationPage, Rect> TogglePaneButtonRectChanged;

        /// <summary>
        /// Callback when the SplitView's Pane is toggled open or close.  When the Pane is not visible
        /// then the floating hamburger may be occluding other content in the app unless it is aware.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TogglePaneButton_Checked(object sender, RoutedEventArgs e)
        {
            this.CheckTogglePaneButtonSizeChanged();
        }

        /// <summary>
        /// Check for the conditions where the navigation pane does not occupy the space under the floating
        /// hamburger button and trigger the event.
        /// </summary>
        private void CheckTogglePaneButtonSizeChanged()
        {
            if (this.RootSplitView.DisplayMode == SplitViewDisplayMode.Inline ||
                this.RootSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
            {
                var transform = this.TogglePaneButton.TransformToVisual(this);
                var rect = transform.TransformBounds(new Rect(0, 0, this.TogglePaneButton.ActualWidth, this.TogglePaneButton.ActualHeight));
                this.TogglePaneButtonRect = rect;
            }
            else
            {
                this.TogglePaneButtonRect = new Rect();
            }

            var handler = this.TogglePaneButtonRectChanged;
            if (handler != null)
            {
                // handler(this, this.TogglePaneButtonRect);
                handler.DynamicInvoke(this, this.TogglePaneButtonRect);
            }
        }

        /// <summary>
        /// Enable accessibility on each nav menu item by setting the AutomationProperties.Name on each container
        /// using the associated Label of each item.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void NavMenuItemContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (!args.InRecycleQueue && args.Item != null && args.Item is NavMenuItem)
            {
                args.ItemContainer.SetValue(AutomationProperties.NameProperty, ((NavMenuItem)args.Item).Label);
            }
            else
            {
                args.ItemContainer.ClearValue(AutomationProperties.NameProperty);
            }
        }

        private void TogglePaneButton_Click(object sender, RoutedEventArgs e)
        {
            // Update button label
            if(this.RootSplitView.IsPaneOpen && this.AppFrame.CurrentSourcePageType == typeof(ViewerPage))
                UpdateViewBtn(ViewerPage.Current.ViewerKey, ViewerPage.Current.VisiblePageRange.ToString());
        }
    }
}
