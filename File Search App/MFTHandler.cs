using Microsoft.Win32.SafeHandles;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Navigation;
using System.Xml.Linq;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;
using static File_Search_App.MFTHandler;

namespace File_Search_App
{
    internal class MFTHandler
    {

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
            FileName = 0x30,
            Data = 0x80,
            BitMap = 0xB0,
            EndMarker = 0xFFFFFFFF,
        }

        #region Structs 

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BootSector
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
        public struct FileRecordHeader
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
            public ulong fileReference;
            public ushort nextAttributeID;
            public ushort unused;
            public uint recordNum;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct AttributeHeader
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
        struct ResidentAttributeHeader
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
        struct NonResidentAttributeHeader
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
        struct FileNameAttributeHeader
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
            public char[] fileName;

            public ulong GetParentRecordNumber()
            {
                ulong recordNum = 0;

                for (int i = 0; i < parentRecordNumber.Length; i++)
                {
                    recordNum |= (ulong)parentRecordNumber[i] >> (8 * i);
                }

                return recordNum;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct File
        {
            public ulong parent;
            public string fileName;
        }

        #endregion

        const int MFT_FILE_SIZE = 1024;
        const int MFT_FILES_PER_BUFFER = 65536;

        public MFTHandler()
        {
            foo();
            Debug.WriteLine("DONE");
        }

        public void foo()
        {

            byte[] mftFile = new byte[MFT_FILE_SIZE];
            List<File> files = new List<File>();
            int count = 0;
            SafeFileHandle handle = PInvoke.CreateFile(@"\\.\C:", //grabbing a handle to the C: volume
            (uint)GenericAccessRights.GENERIC_READ,
            FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_READ,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            0,
            null);

            BootSector bootSector = GetBootSector(handle);

            int bytesPerCluster = bootSector.bytesPerSector * bootSector.sectorsPerCluster;

            ReadHandle(handle, mftFile, (long)bootSector.mftStart * bytesPerCluster, MFT_FILE_SIZE); // grab the first 1kb of the MFT

            FileRecordHeader fileRecord = FindMFTFileRecord(handle, bootSector, mftFile, bytesPerCluster);

            NonResidentAttributeHeader dataAttribute =
                FindDataAttribute(mftFile,
                fileRecord.firstAttributeOffset,
                out int dataAttributePosition,
                out ulong approximateRecordCount);

            int dataRunPosition = dataAttributePosition + dataAttribute.dataRunsOffset;

            RunHeader dataRun = BytesToStruct<RunHeader>
                (mftFile[dataRunPosition..^0]);

            long clusterNum = 0;
            byte[] mftBuffer = new byte[MFT_FILES_PER_BUFFER * MFT_FILE_SIZE];
            ulong recordsProcessed = 0;

            while ((dataRunPosition - dataAttributePosition) < dataAttribute.length
                && dataRun.GetRunLength() > 0)
            {

                CalculateLengthAndOffset(dataRun, dataRunPosition, mftFile, out uint length, out uint offset);

                clusterNum += offset;

                int filesRemaining = (int)length * bytesPerCluster / MFT_FILE_SIZE;
                long bufferPosition = 0;

                while (filesRemaining > 0)
                {
                    Debug.WriteLine((int)(recordsProcessed * 100 / approximateRecordCount));

                    int filesToLoad = MFT_FILES_PER_BUFFER;

                    if (filesRemaining < MFT_FILES_PER_BUFFER)
                        filesToLoad = filesRemaining;

                    count += filesToLoad;

                    ReadHandle(handle,
                        mftBuffer,
                        clusterNum * bytesPerCluster + bufferPosition,
                        (uint)(filesToLoad * MFT_FILE_SIZE));

                    bufferPosition += filesToLoad * MFT_FILE_SIZE;
                    filesRemaining -= filesToLoad;

                    for (int i = 0; i < filesToLoad; i++)
                    {
                        int fileRecordPosition = MFT_FILE_SIZE * i;

                        FileRecordHeader file =
                            BytesToStruct<FileRecordHeader>(mftBuffer[fileRecordPosition..(fileRecordPosition + Marshal.SizeOf(typeof(FileRecordHeader)))]);
                        recordsProcessed++;

                        Debug.Assert(file.magicNum == 0x454C4946);

                        if ((file.flags & (ushort)FileRecordFlags.InUse) != 1)
                            continue;



                        int attributePosition = fileRecordPosition + file.firstAttributeOffset;

                        AttributeHeader attribute = BytesToStruct<AttributeHeader>(mftBuffer[attributePosition..(attributePosition + Marshal.SizeOf(typeof(AttributeHeader)))]);

                        while (attributePosition - fileRecordPosition < MFT_FILE_SIZE)
                        {

                            if (attribute.attributeType == (uint)AttributeTypes.FileName)
                            {

                                int fileNameAttributeSize = attributePosition + Marshal.SizeOf(typeof(FileNameAttributeHeader));
                                FileNameAttributeHeader fileNameAttribute = BytesToStruct<FileNameAttributeHeader>(mftBuffer[attributePosition..fileNameAttributeSize]);

                                if (fileNameAttribute.namespaceType != 2 && fileNameAttribute.nonResident == 0)
                                {
                                    File fileData = new File();

                                    fileData.parent = fileNameAttribute.GetParentRecordNumber();

                                    int fileNameStart = fileNameAttributeSize - 1;
                                    int fileNameEnd = fileNameStart + fileNameAttribute.fileNameLength * 2;

                                    char[] chars = Encoding.Unicode.GetString(mftBuffer[fileNameStart..fileNameEnd]).ToCharArray();

                                    fileData.fileName = new string(chars);

                                    files.Add(fileData);                                    
                                }
                            }
                            else if (attribute.attributeType == (uint)AttributeTypes.EndMarker)
                            {
                                break;
                            }

                            attributePosition += (int)attribute.length;
                            int attributeSize = attributePosition + Marshal.SizeOf(typeof(AttributeHeader));

                            if ((uint)attributePosition < mftBuffer.Length - 1) //attributePos can get so large it overflows int, so we convert to uint
                            {
                                attribute = BytesToStruct<AttributeHeader>(mftBuffer[attributePosition..attributeSize]);
                            }
                            else
                            {
                                attributePosition = mftBuffer.Length - 1 - Marshal.SizeOf(typeof(AttributeHeader));
                                attributeSize = attributePosition + Marshal.SizeOf(typeof(AttributeHeader));
                                attribute = BytesToStruct<AttributeHeader>(mftBuffer[attributePosition..attributeSize]);
                            }




                        }

                    }


                }

                dataRunPosition += dataRun.GetRunLength() + dataRun.GetRunOffset() + 1;
                dataRun = BytesToStruct<RunHeader>(mftFile[dataRunPosition..^0]);

            }

            Debug.WriteLine("Files + Folders Accessed: " + count);

            handle.Close();
        }

        private void CalculateLengthAndOffset(RunHeader dataRun, int dataRunPosition, byte[] mftFile, out uint length, out uint offset)
        {
            length = 0;
            offset = 0;

            for (int i = 0; i < dataRun.GetRunLength(); i++)
            {
                length |= ((uint)mftFile[dataRunPosition + 1 + i]) << (i * 8);
            }

            for (int i = 0; i < dataRun.GetRunOffset(); i++)
            {
                offset |= ((uint)mftFile[dataRunPosition + 1 + dataRun.GetRunLength() + i]) << (i * 8);
            }

            if ((offset & (1 << (dataRun.GetRunOffset() * 8 - 1))) > 0)
            {
                for (int i = dataRun.GetRunOffset(); i < 8; i++)
                {
                    offset |= (uint)0xFF << (i * 8);
                }
            }
        }

        private FileRecordHeader FindMFTFileRecord(SafeFileHandle handle, BootSector bootSector, byte[] mftFile, int bytesPerCluster)
        {

            FileRecordHeader fileRecord = BytesToStruct<FileRecordHeader>(mftFile);

            Debug.Assert(fileRecord.magicNum == 0x454C4946);

            return fileRecord;
        }

        private BootSector GetBootSector(SafeFileHandle handle)
        {

            BootSector bootSector = new BootSector();

            byte[] bootSecBuf = StructToBytes<BootSector>(bootSector);

            ReadHandle(handle, bootSecBuf, 0, 512);

            bootSector = BytesToStruct<BootSector>(bootSecBuf);

            return bootSector;
        }

        private NonResidentAttributeHeader FindDataAttribute(byte[] mftFile, int filePosition, out int dataAttributePosition, out ulong approximateRecordCount)
        {

            AttributeHeader attribute = BytesToStruct<AttributeHeader>(mftFile[filePosition..^0]);

            NonResidentAttributeHeader dataAttribute = new NonResidentAttributeHeader();

            approximateRecordCount = 0;
            dataAttributePosition = 0;

            while (attribute.attributeType != (uint)AttributeTypes.EndMarker) // 0xFFFFFFFF is the end marker for the attributes
            {
                if (attribute.attributeType == (uint)AttributeTypes.Data) // $DATA attribute
                {
                    dataAttribute = BytesToStruct<NonResidentAttributeHeader>(mftFile[filePosition..^0]);
                    dataAttributePosition = filePosition;
                }
                else if (attribute.attributeType == (uint)AttributeTypes.BitMap)
                {
                    approximateRecordCount = BytesToStruct<NonResidentAttributeHeader>(mftFile[filePosition..^0]).attributeSize * 8;
                }

                filePosition += (int)attribute.length;

                attribute = BytesToStruct<AttributeHeader>(mftFile[filePosition..^0]);
            }

            Debug.Assert(!dataAttribute.Equals(default(NonResidentAttributeHeader)));

            return dataAttribute;
        }

        /// <summary>
        /// Converts a BootSector struct into an array of bytes
        /// </summary>
        /// <param name="bootSector">The BootSector struct to be converted into an array of bytes</param>
        /// <returns></returns>
        private byte[] StructToBytes<T>(T str)
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
        /// Converts an array of bytes to a BootSector struct.
        /// </summary>
        /// <param name="bytes">The array of bytes to be converted into a struct</param>
        /// <returns></returns>
        private T BytesToStruct<T>(byte[] bytes) where T : new()
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
        /// <param name="count">The count of bytes to be read from the device</param>
        private unsafe void ReadHandle(SafeFileHandle handle, byte[] buffer,
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
    }
}
