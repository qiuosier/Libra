
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.UI.Input.Inking;
using Libra.Class;

namespace LibraTests
{
    [TestClass]
    public class TestFileModel
    {
        [TestMethod]
        public async Task TestFileAccess()
        {
            StorageFolder assetsFolder = await Package.Current.InstalledLocation.GetFolderAsync("TestAssets");
            StorageFile testFile = await assetsFolder.GetFileAsync("SinglePageTestFile.pdf");
            Assert.IsNotNull(testFile);
        }

        [TestMethod]
        public async Task TestLoadFile()
        {
            StorageFile testFile = await Package.Current.InstalledLocation.GetFileAsync("TestAssets\\SinglePageTestFile.pdf");
            StorageFolder dataFolder = await ApplicationData.Current.LocalFolder.CreateFolderAsync("data_folder");
            PdfModel pdfModel = await PdfModel.LoadFromFile(testFile, dataFolder);
        }
    }
}
