
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Input.Inking;

namespace LibraTests
{
    [TestClass]
    public class TestInkManager
    {
        [TestMethod]
        public async Task TestFileAccess()
        {
            StorageFolder assetsFolder = await Package.Current.InstalledLocation.GetFolderAsync("TestAssets");
            StorageFile testFile = await assetsFolder.GetFileAsync("SinglePageTestFile.pdf");
            Assert.IsNotNull(testFile);
        }
    }
}
