using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;
using Windows.Win32;
using Windows.Win32.Storage.FileSystem;

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
            uint magicNum;
            ushort updateSequenceOffset;
            ushort updateSequenceSize;
            ulong logSequenceNum;
            ushort sequenceNum;
            ushort hardLinkCount;
            ushort firstAttributeOffset;
            ushort inUse; //these are 1 bit bitfields
            ushort isDirectory; // yikes
            uint realSize;
            uint allocatedSize;
            ulong fileReference;
            ushort nextAttributeID;
            ushort unused;
            uint recordNum;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct AttributeHeader
        {
            uint attributeType;
            uint length;
            byte nonResident;
            byte nameLength;
            ushort nameOffset;
            ushort flags;
            ushort attributeID;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct ResidentAttributeHeader
        {
            AttributeHeader attributeHeader;
            uint attributeLength;
            ushort attributeOffset;
            byte indexed;
            byte unused;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct NonResidentAttributeHeader
        {
            AttributeHeader attributeHeader;
            ulong firstCluster;
            ulong lastCluster;
            ushort dataRunsOffset;
            ushort compressionUnit;
            uint unused;
            ulong attributeAllocated;
            ulong attributeSize;
            ulong streamDataSize;
        }

        #endregion

        public MFTHandler()
        {
            foo();
            Debug.WriteLine("DONE");
        }

        public unsafe void foo()
        {
            uint mftSize = 1024;
       
            BootSector bootSector = new BootSector();

            byte[] bootSecBuf = StructToBytes<BootSector>(bootSector);

            SafeFileHandle handle = PInvoke.CreateFile(@"\\.\C:",
            (uint)GenericAccessRights.GENERIC_READ,
            FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_READ,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            0,
            null);

            ReadHandle(handle, bootSecBuf, 0, 512);

            bootSector = BytesToStruct<BootSector>(bootSecBuf);

            byte[] mftFile = new byte[mftSize];

            ulong bytesPerCluster = (ulong)bootSector.bytesPerSector * bootSector.sectorsPerCluster;

            ReadHandle(handle, mftFile, (int)(bootSector.mftStart * bytesPerCluster), mftSize);
            
            FileRecordHeader fileRecord = BytesToStruct<FileRecordHeader>(mftFile);

            

            handle.Close();
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
        private T BytesToStruct<T>(byte[] bytes) where T : new ()
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
            int from, uint count)
        {
            fixed (byte* bufferPtr = buffer)
            {
                uint bytesAccessed;
                PInvoke.SetFilePointer(handle, from,
                    &from, SET_FILE_POINTER_MOVE_METHOD.FILE_BEGIN);

                PInvoke.ReadFile((Windows.Win32.Foundation.HANDLE)handle.DangerousGetHandle(),
                    bufferPtr, count, &bytesAccessed, null);

                if (bytesAccessed != count)
                {
                    Debug.WriteLine("Bytes accessed not equal to count");
                    Debug.WriteLine("Count: " + count);
                    Debug.WriteLine("Bytes accessed: " + bytesAccessed);
                }
            }
        }





        //public void StreamToRecord(Stream stream, USN_RECORD_V2 record)
        //{
        //    var recordStart = stream.Position;
        //    record.RecordLength = BitConverter.ToUInt32(ReadStream(stream, 4));
        //    record.MajorVersion = BitConverter.ToUInt16(ReadStream(stream, 2));
        //    record.MinorVersion = BitConverter.ToUInt16(ReadStream(stream, 2));
        //    record.FileReferenceNumber = BitConverter.ToUInt64(ReadStream(stream, 8));
        //    record.ParentFileReferenceNumber = BitConverter.ToUInt64(ReadStream(stream, 8));
        //    record.Usn = BitConverter.ToInt64(ReadStream(stream, 8));
        //    record.TimeStamp = BitConverter.ToInt64(ReadStream(stream, 8));
        //    record.Reason = BitConverter.ToUInt32(ReadStream(stream, 4));
        //    record.SourceInfo = BitConverter.ToUInt32(ReadStream(stream, 4));
        //    record.SecurityId = BitConverter.ToUInt32(ReadStream(stream, 4));
        //    record.FileAttributes = BitConverter.ToUInt32(ReadStream(stream, 4));
        //    record.FileNameLength = BitConverter.ToUInt16(ReadStream(stream, 2));
        //    record.FileNameOffset = BitConverter.ToUInt16(ReadStream(stream, 2));

        //    stream.Position = recordStart + record.FileNameOffset;

        //    // find out how to assign string to stream.FileName

        //    Debug.WriteLine(Encoding.Unicode.GetString(ReadStream(stream, record.FileNameLength)));

        //    stream.Position = recordStart + record.RecordLength;

        //}

        //public byte[] ReadStream(Stream stream, int byteAmount)
        //{

        //    var bytes = new byte[byteAmount];
        //    var offset = 0;
        //    var bytesRead = 0;
        //    bytesRead = stream.Read(bytes, offset, byteAmount);


        //    offset += bytesRead;

        //    return bytes;
        //}

    }
}
