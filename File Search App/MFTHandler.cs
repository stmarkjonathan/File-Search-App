using Microsoft.Win32.SafeHandles;

using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Printing.IndexedProperties;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using static File_Search_App.MFTHandler;

namespace File_Search_App
{
    public static class MFTHandler
    {

        private static Dictionary<ulong, FileData> filesDict = new Dictionary<ulong, FileData>();
        private static Dictionary<ulong, ulong> extensionRecordNums = new Dictionary<ulong, ulong>();

        private static SafeFileHandle handle = new SafeFileHandle();
        private static string volumeName = "";

        const int MFT_FILE_SIZE = 1024;
        const int MFT_FILES_PER_BUFFER = 65536;

        #region Enums

        enum GenericAccessRights : uint
        {
            GENERIC_ALL = 0x10000000,
            GENERIC_EXECUTE = 0x20000000,
            GENERIC_WRITE = 0x40000000,
            GENERIC_READ = 0x80000000,
        }

        [Flags]
        enum FileRecordFlags
        {
            InUse = 0x01,
            IsDirectory = 0x02,
            IsExtension = 0x04,
            SpecialIndex = 0x08
        }

        [Flags]
        enum FileProperties
        {
            ReadOnly = 0x01,
            Hidden = 0x02,
            System = 0x04,
            Archive = 0x020,
            Device = 0x040,
            Normal = 0x080,
            Temporary = 0x0100,
            Sparse = 0x0200,
            ReparsePoint = 0x0400,
            Compressed = 0x0800,
            Offline = 0x1000,
            NotIndexed = 0x2000,
            Encrypted = 0x4000,

        }

        enum AttributeTypes : uint
        {
            AttributeList = 0x20,
            FileName = 0x30,
            Data = 0x80,
            BitMap = 0xB0,
            EndMarker = 0xFFFFFFFF,
        }

        #endregion

        #region Structs 

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct BootSector
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] jump;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 8)]
            public string name;
            public ushort bytesPerSector;
            public byte sectorsPerCluster;
            public ushort reservedSectors;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] unused0;
            public ushort unused1;
            public byte media;
            public ushort unused2;
            public ushort sectorsPerTrack;
            public ushort headsPerCylinder;
            public uint hiddenSectors;
            public uint unused3;
            public uint unused4;
            public ulong totalSectors;
            public ulong mftStart;
            public ulong mftMirrorStart;
            public uint clustersPerFileRecord;
            public uint clustersPerIndexBlock;
            public ulong serialNumber;
            public uint checksum;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 426)]
            public byte[] bootLoader;
            public ushort bootSignature;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileRecordHeader
        {
            public uint magicNum;
            public ushort updateSequenceOffset;
            public ushort updateSequenceSize;
            public ulong logSequenceNum;
            public ushort sequenceNum;
            public ushort hardLinkCount;
            public ushort firstAttributeOffset;
            public ushort flags;
            public uint realSize;
            public uint allocatedSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            byte[] baseRecordNumber;
            public ushort baseSequenceNum;
            public ushort nextAttributeID;
            public ushort unused;
            public uint recordNum;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AttributeHeader
        {
            public uint attributeType;
            public uint length;
            public byte nonResident;
            public byte nameLength;
            public ushort nameOffset;
            public ushort flags;
            public ushort attributeID;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct ResidentAttributeHeader
        {
            public uint attributeType;
            public uint length;
            public byte nonResident;
            public byte nameLength;
            public ushort nameOffset;
            public ushort flags;
            public ushort attributeID;
            public uint attributeLength;
            public ushort attributeOffset;
            public byte indexed;
            public byte unused;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct NonResidentAttributeHeader
        {
            public uint attributeType;
            public uint length;
            public byte nonResident;
            public byte nameLength;
            public ushort nameOffset;
            public ushort flags;
            public ushort attributeID;
            public ulong firstCluster;
            public ulong lastCluster;
            public ushort dataRunsOffset;
            public ushort compressionUnit;
            public uint unused;
            public ulong attributeAllocated;
            public ulong attributeSize;
            public ulong streamDataSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct RunHeader
        {
            /*
             * Data Run Headers contain two 4-bit variables 
             * a length variable - length of data run in bytes
             * an offset variable - how much the data run is offset from the previous run
             */
            public byte contents;
            public int GetRunOffset() { return (contents & 0xF0) >> 4; }

            public int GetRunLength() { return contents & 0x0F; }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct FileNameAttributeHeader
        {
            public uint attributeType;
            public uint length;
            public byte nonResident;
            public byte nameLength;
            public ushort nameOffset;
            public ushort flags;
            public ushort attributeID;
            public uint attributeLength;
            public ushort attributeOffset;
            public byte indexed;
            public byte unused;
            // parentRecordNumber is supposed to be a 6 byte long value,
            // but since C# doesn't have bitfields, we opt for a 6 byte array
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            byte[] parentRecordNumber;
            public ushort sequenceNumber;
            public ulong creationTime;
            public ulong modificationTime;
            public ulong metadataModificationTime;
            public ulong readTime;
            public ulong allocatedSize;
            public ulong realSize;
            public uint fileProperties;
            public uint repase;
            public byte fileNameLength;
            public byte namespaceType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public char[] namePlaceholder;

            public ulong GetParentRecordNumber()
            {
                byte[] array = new byte[8];

                Array.Copy(parentRecordNumber, array, 6);

                return BitConverter.ToUInt64(array, 0);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct AttributeListEntry
        {
            public uint attributeType;
            public ushort attributeSize;
            public byte attributeNameLength;
            public byte attributeNameOffset;
            public ulong vcn;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            byte[] baseRecordNumber;
            public ushort sequenceNumber;
            public ushort attributeId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
            public char[] namePlaceholder;

            public ulong GetBaseRecordNumber()
            {
                byte[] array = new byte[8];

                Array.Copy(baseRecordNumber, array, 6);

                return BitConverter.ToUInt64(array, 0);
            }
        }


        public class FileData
        {
            public ulong ParentIndex { get; set; }
            public ulong FileIndex { get; set; }
            public string FileName { get; set; }
            public string FilePath { get; set; }
            public ImageSource FileIcon { get; set; }

            public FileData()
            {
                FileName = "";
                FilePath = "";
            }

            public override string ToString()
            {
                return FilePath;
            }
        }

        #endregion

        public static Dictionary<ulong, FileData> GetDriveFiles(string driveName, int dataRunCount = 0) // dataRunCount is the amount of dataruns to read. 0 means all
        {

            //we know a constant to test by: files will always contain:
            /*
             * $MFTMirr
             * $LogFile
             * $Volume
             * $AttrDef
             * .
             * $Bitmap
             * $Boot
             * $BadClus
             * $Secure
             * $UpCase
             * $Extend
             * */

            volumeName = driveName;
            handle = GetDriveHandle(driveName);

            byte[] mftFile = GetMFTFileRecord();

            BootSector bootSector = GetBootSector(handle);

            ulong bytesPerCluster = (ulong)(bootSector.bytesPerSector * bootSector.sectorsPerCluster);

            FileRecordHeader mftRecord = BytesToStruct<FileRecordHeader>(mftFile);

            Debug.Assert(mftRecord.magicNum == 0x454C4946);

            NonResidentAttributeHeader dataAttribute =
                FindDataAttribute(mftFile,
                mftRecord.firstAttributeOffset,
                out int dataAttributePosition);

            int dataRunPosition = dataAttributePosition + dataAttribute.dataRunsOffset;

            RunHeader dataRun = BytesToStruct<RunHeader>
                (mftFile[dataRunPosition..^0]);

            EnumerateDataRuns(dataRun, dataAttribute, bytesPerCluster, mftFile, dataRunPosition, dataAttributePosition, dataRunCount);

            filesDict[5].FileName = driveName.Substring(0, driveName.Length - 1);

            foreach (var file in filesDict.Values)
            {
                if (file != null)
                {
                    file.FilePath = GetFilePath(file, extensionRecordNums);
                }

            }

            handle.Close();

            return filesDict;
        }

        private static byte[] GetMFTFileRecord()
        {
            byte[] mftFile = new byte[MFT_FILE_SIZE];

            Dictionary<ulong, ulong> extensionRecordNums = new Dictionary<ulong, ulong>();

            BootSector bootSector = GetBootSector(handle);

            ulong bytesPerCluster = (ulong)(bootSector.bytesPerSector * bootSector.sectorsPerCluster);

            ReadHandle(handle, mftFile, (long)(bootSector.mftStart * bytesPerCluster), MFT_FILE_SIZE); // grab the first 1kb of the MFT

            return mftFile;
        }

        private static void EnumerateDataRuns(RunHeader dataRun, NonResidentAttributeHeader dataAttribute, ulong bytesPerCluster, byte[] mftFile, int dataRunPosition, int dataAttributePosition, int dataRunCount)
        {
            int count = 0;
            ulong clusterNum = 0;
            byte[] mftBuffer = new byte[MFT_FILES_PER_BUFFER * MFT_FILE_SIZE];

            while ((dataRunPosition - dataAttributePosition) < dataAttribute.length
                && dataRun.GetRunLength() > 0)
            {

                if (count >= dataRunCount && dataRunCount != 0)
                {
                    break;
                }

                CalculateLengthAndOffset(dataRun, dataRunPosition, mftFile, out ulong length, out ulong offset);

                clusterNum += offset;
                ulong filesRemaining = length * bytesPerCluster / MFT_FILE_SIZE;
                ulong bufferPosition = 0;

                while (filesRemaining > 0)
                {

                    ulong filesToLoad = MFT_FILES_PER_BUFFER;

                    if (filesRemaining < MFT_FILES_PER_BUFFER)
                        filesToLoad = filesRemaining;

                    ReadHandle(handle,
                        mftBuffer,
                        (long)(clusterNum * bytesPerCluster + bufferPosition),
                        (uint)(filesToLoad * MFT_FILE_SIZE));

                    bufferPosition += filesToLoad * MFT_FILE_SIZE;
                    filesRemaining -= filesToLoad;

                    for (ulong i = 0; i < filesToLoad; i++)
                    {
                        int fileRecordPosition = MFT_FILE_SIZE * (int)i;
                        ulong bufferStart = clusterNum * bytesPerCluster + bufferPosition;

                        FileRecordHeader fileRecord =
                            BytesToStruct<FileRecordHeader>(mftBuffer[fileRecordPosition..(fileRecordPosition + Marshal.SizeOf(typeof(FileRecordHeader)))]);

                        if ((fileRecord.flags & (ushort)FileRecordFlags.InUse) != 1)
                        {
                            continue;
                        }

                        Debug.Assert(fileRecord.magicNum == 0x454C4946);

                        FileData file = GetFileNameAttribute(mftBuffer, extensionRecordNums, fileRecord, fileRecordPosition);

                        if (!file.Equals(default(FileData)))
                        {
                            filesDict[file.FileIndex] = file;
                        }


                    }
                }

                dataRunPosition += dataRun.GetRunLength() + dataRun.GetRunOffset() + 1;
                dataRun = BytesToStruct<RunHeader>(mftFile[dataRunPosition..^0]);
                count++;

            }
        }

        private static FileData GetFileData(FileNameAttributeHeader fileNameAttribute, FileRecordHeader fileRecord, int bufferPosition, byte[] mftBuffer)
        {
            FileData file = new FileData();

            int attributeSize = bufferPosition + Marshal.SizeOf(typeof(FileNameAttributeHeader));

            int fileNameStart = attributeSize - 1;
            int fileNameEnd = fileNameStart + fileNameAttribute.fileNameLength * 2;

            char[] chars = Encoding.Unicode.GetString(mftBuffer[fileNameStart..fileNameEnd]).ToCharArray();

            file.ParentIndex = fileNameAttribute.GetParentRecordNumber();
            file.FileIndex = fileRecord.recordNum;
            file.FileName = new string(chars);

            return file;
        }

        private static string GetFilePath(FileData file, Dictionary<ulong, ulong> extensionRecordNums)
        {

            string filePath = "";
            ulong fallbackParent;
            FileData? parentFile;

            extensionRecordNums.TryGetValue(file.ParentIndex, out fallbackParent);

            bool isParentFound =
                filesDict.TryGetValue(file.ParentIndex, out parentFile) ||
                filesDict.TryGetValue(fallbackParent, out parentFile);

            if (isParentFound && parentFile != null)
            {
                if (file.FileIndex == file.ParentIndex)
                {
                    filePath = file.FileName;
                    return filePath;
                }

                if (!String.IsNullOrEmpty(parentFile.FilePath))
                {
                    filePath = parentFile.FilePath + "\\" + file.FileName;
                    return filePath;
                }

                filePath += GetFilePath(parentFile, extensionRecordNums);




                filePath += "\\" + file.FileName;

                return filePath;
            }
            else
            {
                return file.FileName;
            }

        }


        /// <summary>
        /// Extracts and returns the length and offset values of a data run
        /// </summary>
        /// <param name="dataRun"></param>
        /// <param name="dataRunPosition"></param>
        /// <param name="mftFile"></param>
        /// <param name="length"></param>
        /// <param name="offset"></param>
        private static void CalculateLengthAndOffset(RunHeader dataRun, int dataRunPosition, byte[] mftFile, out ulong length, out ulong offset)
        {
            length = 0;
            offset = 0;

            for (int i = 0; i < dataRun.GetRunLength(); i++)
            {
                length |= (ulong)(mftFile[dataRunPosition + 1 + i]) << (i * 8);
            }

            for (int i = 0; i < dataRun.GetRunOffset(); i++)
            {
                offset |= (ulong)(mftFile[dataRunPosition + 1 + dataRun.GetRunLength() + i]) << (i * 8);
            }

            if ((offset & ((ulong)1 << (dataRun.GetRunOffset() * 8 - 1))) > 0)
            {
                for (int i = dataRun.GetRunOffset(); i < 8; i++)
                {
                    offset |= (ulong)0xFF << (i * 8);
                }
            }
        }
        private static BootSector GetBootSector(SafeFileHandle handle)
        {

            BootSector bootSector = new BootSector();

            byte[] bootSecBuf = StructToBytes<BootSector>(bootSector);

            ReadHandle(handle, bootSecBuf, 0, 512);

            bootSector = BytesToStruct<BootSector>(bootSecBuf);

            return bootSector;
        }

        /// <summary>
        /// Searches for the data attribute within a NTFS file record.
        /// </summary>
        /// <param name="mftFile">A buffer containing NTFS volume data</param>
        /// <param name="filePosition">The current position within the buffer </param>
        /// <param name="dataAttributePosition">Contains the position of the data attribute</param>
        /// <returns></returns>
        private static NonResidentAttributeHeader FindDataAttribute(byte[] mftFile, int filePosition, out int dataAttributePosition)
        {

            AttributeHeader attribute = BytesToStruct<AttributeHeader>(mftFile[filePosition..^0]);

            NonResidentAttributeHeader dataAttribute = new NonResidentAttributeHeader();

            dataAttributePosition = 0;

            while (attribute.attributeType != (uint)AttributeTypes.EndMarker)
            {
                if (attribute.attributeType == (uint)AttributeTypes.Data)
                {
                    dataAttribute = BytesToStruct<NonResidentAttributeHeader>(mftFile[filePosition..^0]);
                    dataAttributePosition = filePosition;
                }

                filePosition += (int)attribute.length;

                attribute = BytesToStruct<AttributeHeader>(mftFile[filePosition..^0]);
            }

            Debug.Assert(!dataAttribute.Equals(default(NonResidentAttributeHeader)));

            return dataAttribute;
        }
        //review this, review decision tree
        private static FileData GetFileNameAttribute(byte[] mftBuffer, Dictionary<ulong, ulong> extensionRecordNums, FileRecordHeader fileRecord, int fileRecordPosition) 
        {
            int attributePosition = fileRecordPosition + fileRecord.firstAttributeOffset;
            int attributeListPosition = 0;
            bool hasAttributeList = false;
            bool fileNameFound = false;

            AttributeHeader attribute = BytesToStruct<AttributeHeader>(mftBuffer[attributePosition..(attributePosition + Marshal.SizeOf(typeof(AttributeHeader)))]);
            FileNameAttributeHeader fileNameAttribute = new FileNameAttributeHeader();
            ResidentAttributeHeader attributeListAttribute = new ResidentAttributeHeader();
            FileData file = new FileData();

            while (attributePosition - fileRecordPosition < MFT_FILE_SIZE && !fileNameFound)
            {

                if (attribute.attributeType == (uint)AttributeTypes.FileName)
                {
                    int fileNameAttributeSize = attributePosition + Marshal.SizeOf(typeof(FileNameAttributeHeader));
                    fileNameAttribute = BytesToStruct<FileNameAttributeHeader>(mftBuffer[attributePosition..fileNameAttributeSize]);

                    if (fileNameAttribute.namespaceType != 2)
                    {
                        fileNameFound = true;
                    }

                    if (!fileNameAttribute.Equals(default(FileNameAttributeHeader)) && fileNameAttribute.nonResident == 0)
                    {
                        file = GetFileData(fileNameAttribute, fileRecord, attributePosition, mftBuffer);
                    }

                    return file;
                }
                else if (attribute.attributeType == (uint)AttributeTypes.AttributeList)
                {
                    hasAttributeList = true;
                    attributeListPosition = attributePosition;
                    int attributeListSize = attributePosition + Marshal.SizeOf(typeof(ResidentAttributeHeader));
                    attributeListAttribute = BytesToStruct<ResidentAttributeHeader>(mftBuffer[attributePosition..attributeListSize]);
                }
                else if (attribute.attributeType == (uint)AttributeTypes.EndMarker)
                {
                    break;
                }

                attributePosition += (int)attribute.length;
                int attributeEnd = attributePosition + Marshal.SizeOf(typeof(AttributeHeader));

                if ((uint)attributePosition < mftBuffer.Length - 1) //attributePos can get so large it overflows int, so we convert to uint
                {
                    attribute = BytesToStruct<AttributeHeader>(mftBuffer[attributePosition..attributeEnd]);
                }
                else
                {
                    attributePosition = mftBuffer.Length - 1 - Marshal.SizeOf(typeof(AttributeHeader));
                    attributeEnd = attributePosition + Marshal.SizeOf(typeof(AttributeHeader));
                    attribute = BytesToStruct<AttributeHeader>(mftBuffer[attributePosition..attributeEnd]);
                    break;
                }
            }

            if (hasAttributeList && !fileNameFound)
            {
                FindAttributeListFileName(mftBuffer, attributeListAttribute, extensionRecordNums, fileRecord.recordNum, attributeListPosition);
            }

            return file;

        }

        private static void FindAttributeListFileName(byte[] mftBuffer, ResidentAttributeHeader attributeListAttribute, Dictionary<ulong, ulong> extensionRecordNums, ulong fileRecordNumber, int attributePosition)
        {
            int attributeListStart = attributePosition;
            int attributeListEnd = 0;


            attributeListStart += attributeListAttribute.attributeOffset;
            attributeListEnd = attributeListStart + Marshal.SizeOf(typeof(AttributeListEntry));

            if (attributeListAttribute.nonResident == 0)
            {
                uint listSize = attributeListAttribute.attributeLength;

                while (listSize > 0)
                {
                    AttributeListEntry entry = BytesToStruct<AttributeListEntry>(mftBuffer[attributeListStart..attributeListEnd]);

                    if (entry.attributeType == (uint)AttributeTypes.FileName)
                    {
                        if (entry.GetBaseRecordNumber() != fileRecordNumber)
                        {
                            extensionRecordNums[fileRecordNumber] = entry.GetBaseRecordNumber();
                            break;
                        }
                    }
                    attributeListStart += entry.attributeSize;
                    attributeListEnd = attributeListStart + Marshal.SizeOf(typeof(AttributeListEntry));
                    listSize -= entry.attributeSize;
                }
            }
        }



        /// <summary>
        /// Converts a chosen struct into an array of bytes
        /// </summary>
        /// <param name="str">The struct to be converted into an array of bytes</param>
        /// <returns></returns>
        private static byte[] StructToBytes<T>(T str)
        {
            int size = Marshal.SizeOf(str);
            byte[] arr = new byte[size];

            IntPtr ptr = IntPtr.Zero;

            ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(str, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);

            Marshal.FreeHGlobal(ptr);

            return arr;
        }

        /// <summary>
        /// Converts an array of bytes to a chosen struct.
        /// </summary>
        /// <param name="bytes">The array of bytes to be converted into a struct</param>
        /// <returns></returns>
        private static T BytesToStruct<T>(byte[] bytes) where T : new()
        {

            T str = new T();

            int size = Marshal.SizeOf(str);
            IntPtr ptr = IntPtr.Zero;

            ptr = Marshal.AllocHGlobal(size);

            Marshal.Copy(bytes, 0, ptr, size);

            str = (T)Marshal.PtrToStructure(ptr, str.GetType());

            Marshal.FreeHGlobal(ptr);

            return str;
        }




        /// <summary>
        /// Read bytes from <paramref name="handle"/> into <paramref name="buffer"/>, starting at <paramref name="from"/>,
        /// to <paramref name="count"/>.
        /// </summary>
        /// <param name="handle">A handle to the device that will be read.</param>
        /// <param name="buffer">The buffer that receives the read bytes from the device</param>
        /// <param name="from">The starting position of the file pointer for the device</param>
        /// <param name="count">The amount of bytes to be read from the device</param>
        private static unsafe void ReadHandle(SafeFileHandle handle, byte[] buffer,
            long from, uint count)
        {
            fixed (byte* bufferPtr = buffer)
            {
                uint bytesAccessed;
                PInvoke.SetFilePointerEx(handle, from,
                    null, SET_FILE_POINTER_MOVE_METHOD.FILE_BEGIN);

                PInvoke.ReadFile((Windows.Win32.Foundation.HANDLE)handle.DangerousGetHandle(),
                    bufferPtr, count, &bytesAccessed, null);
            }
        }

        private static FileRecordHeader GetFileRecord(SafeFileHandle driveHandle, long startPos, uint recordPos)
        {
            byte[] buffer = new byte[MFT_FILE_SIZE];

            ReadHandle(driveHandle, buffer, startPos + recordPos, MFT_FILE_SIZE);

            FileRecordHeader fileRecord = BytesToStruct<FileRecordHeader>(buffer);

            return fileRecord;
        }

        private static string ConvertDriveName(string driveName) //ensure format for name is "\\.\X:" for CreateFile
        {
            //driveName format will be "X:\"
            return @"\\.\" + driveName.Remove(driveName.Length - 1);
        }

        private static SafeFileHandle GetDriveHandle(string driveName)
        {
            SafeFileHandle handle = PInvoke.CreateFile(ConvertDriveName(driveName), //grabbing a handle to selected drive volume
           (uint)GenericAccessRights.GENERIC_READ,
           FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_READ,
           null,
           FILE_CREATION_DISPOSITION.OPEN_EXISTING,
           0,
           null);

            return handle;
        }
    }
}
