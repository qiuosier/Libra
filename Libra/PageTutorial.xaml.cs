using Windows.Storage;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Libra
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TutorialPage : Page
    {
        private const int TUTORIAL_PAGE_COUNT = 5;

        private int currentPage = 0;
        SolidColorBrush SteelBlueBrush = new SolidColorBrush(Colors.SteelBlue);
        SolidColorBrush WhiteBrush = new SolidColorBrush(Colors.White);

        public TutorialPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            currentPage = 1;
            ((Ellipse)this.FindName("E" + currentPage.ToString())).Fill = SteelBlueBrush;
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            Image image = ((Image)this.FindName("I" + currentPage.ToString()));
            image.Visibility = Visibility.Collapsed;
            ContentGrid.UpdateLayout();
            ((Ellipse)this.FindName("E" + currentPage.ToString())).Fill = WhiteBrush;
            currentPage++;
            if (currentPage > TUTORIAL_PAGE_COUNT)
                // Tutorial finished, go to main page
                SkipButton_Click(sender, e);
            else ((Ellipse)this.FindName("E" + currentPage.ToString())).Fill = SteelBlueBrush;
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.RoamingSettings.Values[App.TUTORIAL] = false;
            this.Frame.Navigate(typeof(MainPage));
        }

        private void Grid_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            NextButton_Click(sender, e);
        }
    }
}
