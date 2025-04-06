using File_Search_App;

namespace FileSearchAppTests
{
    [TestClass]
    public class MFTHandlerTests
    {
        [TestMethod]
        public void MFTHandler_GrabMFTMetadata_Success()
        {
            string[] expectedFileNames =
            [
               "$MFT",
               "$MFTMirr",
               "$LogFile",
               "$Volume",
               "$AttrDef",
               "C:",
               "$Bitmap",
               "$Boot",
               "$BadClus",
               "$Secure",
               "$UpCase",
               "$Extend"
            ];

            Dictionary<ulong, MFTHandler.FileData> dict = new Dictionary<ulong, MFTHandler.FileData>();       
            string driveName = DriveInfo.GetDrives()[0].Name;

            dict = MFTHandler.GetDriveFiles(driveName, 1);

            for( int i = 0; i < expectedFileNames.Length; i++)
            {
                Assert.AreEqual(expectedFileNames[i], dict[(ulong)i].FileName);
            }
        }
    }
}

