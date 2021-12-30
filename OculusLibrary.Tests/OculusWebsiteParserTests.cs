using NSubstitute;
using NUnit.Framework;
using OculusLibrary.DataExtraction;
using Playnite.SDK;
using System.IO;
using System.Threading.Tasks;

namespace OculusLibrary.Tests
{
    public class OculusWebsiteParserTests
    {
        private IWebView fakeWebView;
        private OculusWebsiteScraper subject;

        [SetUp]
        public void Setup()
        {
            var testHtml = File.ReadAllText($"{TestContext.CurrentContext.TestDirectory}\\demo1.html");

            fakeWebView = Substitute.For<IWebView>();

            fakeWebView.GetPageSource()
                .Returns(testHtml);

            var fakeLogger = Substitute.For<ILogger>();

            subject = new OculusWebsiteScraper(fakeLogger);
        }

        [TearDown]
        public void TearDown() {
            fakeWebView = null;
            subject = null;
        }

        [Test]
        public void Game_Name_Correctly_Extracted()
        {
            var result = subject.ScrapeDataForApplicationId(fakeWebView, "123");

            Assert.AreEqual("Test Game", result.Name);
        }

        [Test]
        public void Game_Description_Correctly_Extracted()
        {
            var result = subject.ScrapeDataForApplicationId(fakeWebView, "123");

            Assert.AreEqual("This is a test description", result.Description);
        }

        [Test]
        public void Game_Scraping_Outputs_All_Values()
        {
            var testHtml = File.ReadAllText($"{TestContext.CurrentContext.TestDirectory}\\Sprint Vector.html");
            var fakeWebView = Substitute.For<IWebView>();
            fakeWebView.GetPageSource().Returns(testHtml);


            var result = subject.GetGameData(fakeWebView, "1425858557493354");
            Assert.NotNull(result);
            Assert.AreEqual("Sprint Vector", result.Name);
            Assert.AreEqual("0.0.0.111496", result.Version);
        }
    }
}