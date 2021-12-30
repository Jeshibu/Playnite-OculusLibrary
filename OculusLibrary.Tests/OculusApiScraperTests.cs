using NSubstitute;
using NUnit.Framework;
using OculusLibrary.DataExtraction;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OculusLibrary.Tests
{
    public class OculusApiScraperTests
    {
        [Test]
        public void GetJsonData()
        {
            var subject = new OculusApiScraper(Substitute.For<ILogger>(), null);
            var data = subject.GetJsonData("1180401875303371");
            Assert.NotNull(data);
        }
    }
}
