using OculusLibrary.OS;
using System.Collections.Generic;
using Xunit;

namespace OculusLibrary.Tests
{
    public class PathNormaliserTests
    {
        [Fact]
        public void Get_Drive_Letter_From_DeviceId()
        {
            var path = @"\\?\Volume{a0bf4e34-c90f-4005-815c-9a5a485e40ad}\Oculus\Software";

            var fakeWMOProvider = new FakeWMOProvider();

            using (var subject = new PathNormaliser(fakeWMOProvider))
            {
                var normalisedPath = subject.Normalise(path);
                Assert.Equal(@"D:\Oculus\Software", normalisedPath);
            }
        }

        private class FakeWMOProvider : IWMODriveQueryProvider
        {
            public List<WMODrive> GetDriveData()
            {
                return new List<WMODrive>
                {
                    new WMODrive {
                        DeviceId = @"\\?\Volume{DCBDB210-C414-409E-B108-C2BFA7395E1F}\",
                        DriveLetter = "X:"
                    },
                    new WMODrive {
                        DeviceId = @"\\?\Volume{DCBDB210-C414-409E-B108-C2BFA7395E1F}\",
                        DriveLetter = ""
                    },
                    new WMODrive {
                        DeviceId = @"\\?\Volume{a0bf4e34-c90f-4005-815c-9a5a485e40ad}\",
                        DriveLetter = "D:"
                    },
                    new WMODrive {
                        DeviceId = @"\\?\Volume{ECBDB210-D414-509E-B108-C2BFA7395E1F}\",
                        DriveLetter = "C:"
                    },
                };
            }
        }
    }
}