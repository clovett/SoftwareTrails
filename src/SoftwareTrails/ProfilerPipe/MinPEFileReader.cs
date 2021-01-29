using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;

namespace SoftwareTrails
{
    [Serializable]
    public class NotExecutableImageException : Exception
    {
    }

    [Serializable]
    public class InvalidPEFileException : Exception
    {
    }

    public class MinPEFileReader : IDisposable
    {
        private MemoryMappedFile fileMapping;
        private MemoryMappedViewAccessor fileAccessor;

        const short ImageDosSignature = 0x5A4D;
        const uint ImageNTSignature = 0x00004550;    // PE00

        ImageDosHeader dosHeader;
        ImageNTHeader ntHeader;
        ImageOptionalHeader32 optionalHeader32;
        ImageOptionalHeader64 optionalHeader64;
        bool hasCorHeader;
        long corHeaderPosition;
        COR20Header corHeader;
        long dataDirectory;
        MetadataHeader metadataHeader;
        SectionHeader[] sectionHeaders;
        int numberOfRvaAndSizes;
        string metadataVersion;

        public MinPEFileReader(string assembly)
        {
            fileMapping = MemoryMappedFile.CreateFromFile(assembly, System.IO.FileMode.Open);
            fileAccessor = fileMapping.CreateViewAccessor();

            long pos = 0;
            fileAccessor.Read<ImageDosHeader>(0, out dosHeader);

            if (dosHeader.e_magic != ImageDosSignature)
            {
                throw new NotExecutableImageException();
            }

            pos = dosHeader.e_lfanew;
            fileAccessor.Read<ImageNTHeader>(pos, out ntHeader);

            if (ntHeader.Signature != ImageNTSignature)
            {
                throw new NotExecutableImageException();
            }

            pos = dosHeader.e_lfanew + Marshal.SizeOf(typeof(ImageNTHeader));

            PEMagic magic = (PEMagic)ntHeader.StandardFields.Magic;
            switch (magic)
            {
                case PEMagic.PEMagic32:
                    fileAccessor.Read<ImageOptionalHeader32>(pos, out optionalHeader32);
                    numberOfRvaAndSizes = optionalHeader32.NumberOfRvaAndSizes;
                    pos += Marshal.SizeOf(typeof(ImageOptionalHeader32));
                    break;
                case PEMagic.PEMagic64:
                    fileAccessor.Read<ImageOptionalHeader64>(pos, out optionalHeader64);
                    numberOfRvaAndSizes = optionalHeader64.NumberOfRvaAndSizes;
                    pos += Marshal.SizeOf(typeof(ImageOptionalHeader64));
                    break;
                default:
                    throw new InvalidPEFileException();
            }

            dataDirectory = pos;

            ReadSectionHeaders(pos + (numberOfRvaAndSizes * Marshal.SizeOf(typeof(ImageDataDirectory))));

            ImageDataDirectory? corDir = GetDirectory((int)Directories.COR20Header);
            if (corDir.HasValue)
            {
                this.corHeaderPosition = GetDataPosition(corDir.Value);
                hasCorHeader = GetDataViaDirectory<COR20Header>(corDir.Value, out corHeader);
                if (hasCorHeader && corHeader.MetaDataDirectory.Size != 0)
                {
                    GetDataViaDirectory<MetadataHeader>(corHeader.MetaDataDirectory, out metadataHeader);

                    pos = GetDataPosition(corHeader.MetaDataDirectory);
                    pos += Marshal.SizeOf(metadataHeader);

                    int len = metadataHeader.VersionStringSize;
                    byte[] version = new byte[len];
                    fileAccessor.ReadArray<byte>(pos, version, 0, len);
                    metadataVersion = GetAsciiString(version);
                }
            }
        }

        internal static string GetAsciiString(byte[] bytes)
        {
            char[] chars = ASCIIEncoding.Default.GetChars(bytes);
            StringBuilder sb = new StringBuilder();
            foreach (char c in chars)
            {
                if (c == '\0')
                {
                    break;
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        bool ReadSectionHeaders(long pos)
        {
            int numberOfSections = this.ntHeader.FileHeader.NumberOfSections;
            this.sectionHeaders = new SectionHeader[numberOfSections];

            for (int i = 0; i < numberOfSections; ++i)
            {
                SectionHeader header;
                fileAccessor.Read<SectionHeader>(pos, out header);
                this.sectionHeaders[i] = header;
                pos += Marshal.SizeOf(header);
            }
            return true;
        }

        internal ImageDataDirectory? GetDirectory(long index)
        {
            if (index < numberOfRvaAndSizes)
            {
                long directoryPos = dataDirectory + (index * Marshal.SizeOf(typeof(ImageDataDirectory)));
                ImageDataDirectory directory = new ImageDataDirectory();
                fileAccessor.Read<ImageDataDirectory>(directoryPos, out directory);
                return directory;
            }

            return null;
        }

        private bool GetDataViaDirectory<T>(ImageDataDirectory directory, out T data) where T : struct
        {
            long pos = GetDataPosition(directory);
            if (pos > 0)
            {
                fileAccessor.Read<T>(pos, out data);
                return true;
            }
            data = new T();
            return false;
        }

        private long GetDataPosition(ImageDataDirectory directory)
        {
            foreach (SectionHeader sectionHeaderIter in this.sectionHeaders)
            {
                if (sectionHeaderIter.VirtualAddress <= directory.VirtualAddress &&
                    directory.VirtualAddress < sectionHeaderIter.VirtualAddress + sectionHeaderIter.VirtualSize)
                {
                    int relativeOffset = directory.VirtualAddress - sectionHeaderIter.VirtualAddress;
                    if (directory.Size <= sectionHeaderIter.VirtualSize - relativeOffset)
                    {
                        long pos = sectionHeaderIter.OffsetToRawData + relativeOffset;
                        return pos;
                    }
                }
            }
            return -1;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MinPEFileReader()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            using (fileAccessor)
            {
                fileAccessor = null;
            }
            using (fileMapping)
            {
                fileMapping = null;
            }
        }

        public bool IsExe
        {
            get
            {
                return (this.ntHeader.FileHeader.Characteristics & (short)Characteristics.Dll) == 0;
            }
        }

        public bool Requires64Bits
        {
            get
            {
                return this.ntHeader.StandardFields.Magic == (int)PEMagic.PEMagic64
                  || this.ntHeader.FileHeader.Machine == (ushort)Machine.AMD64
                  || this.ntHeader.FileHeader.Machine == (ushort)Machine.IA64;
            }
        }

        public bool IsManaged
        {
            get
            {
                return hasCorHeader;
            }
        }

        public string ClrMetadataVersion
        {
            get
            {
                return metadataVersion;
            }
        }

        public bool IsManaged32Required
        {
            get
            {
                return hasCorHeader && (corHeader.COR20Flags & COR20Flags.Bit32Required) != 0;
            }
            set
            {
                if (hasCorHeader)
                {
                    if (value) {
                        corHeader.COR20Flags |= COR20Flags.Bit32Required;
                    } else {
                        corHeader.COR20Flags &= ~COR20Flags.Bit32Required;
                    }

                    using (var writeAccessor = fileMapping.CreateViewAccessor(corHeaderPosition, Marshal.SizeOf(corHeader), MemoryMappedFileAccess.ReadWrite))
                    {
                        writeAccessor.Write<COR20Header>(0, ref corHeader);
                    }
                }
            }
        }

        public void Save(string file)
        {
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ImageDosHeader
    {      // DOS .EXE header
        public short e_magic;                     // Magic number
        public short e_cblp;                      // Bytes on last page of file
        public short e_cp;                        // Pages in file
        public short e_crlc;                      // Relocations
        public short e_cparhdr;                   // Size of header in paragraphs
        public short e_minalloc;                  // Minimum extra paragraphs needed
        public short e_maxalloc;                  // Maximum extra paragraphs needed
        public short e_ss;                        // Initial (relative) SS value
        public short e_sp;                        // Initial SP value
        public short e_csum;                      // Checksum
        public short e_ip;                        // Initial IP value
        public short e_cs;                        // Initial (relative) CS value
        public short e_lfarlc;                    // File address of relocation table
        public short e_ovno;                      // Overlay number
        public short e_res11;                     // Reserved words
        public short e_res12;                     // Reserved words
        public short e_res13;                     // Reserved words
        public short e_res14;                     // Reserved words
        public short e_oemid;                     // OEM identifier (for e_oeminfo)
        public short e_oeminfo;                   // OEM information; e_oemid specific
        public short e_res20;                  // Reserved words
        public short e_res21;                  // Reserved words
        public short e_res22;                  // Reserved words
        public short e_res23;                  // Reserved words
        public short e_res24;                  // Reserved words
        public short e_res25;                  // Reserved words
        public short e_res26;                  // Reserved words
        public short e_res27;                  // Reserved words
        public short e_res28;                  // Reserved words
        public short e_res29;                  // Reserved words
        public int e_lfanew;                     // File address of new exe header
    };

    [StructLayout(LayoutKind.Sequential)]
    struct ImageNTHeader
    {
        public int Signature;
        public ImageFileHeader FileHeader;
        public ImageOptionalHeaderStandardFields StandardFields;
    } ;

    [StructLayout(LayoutKind.Sequential)]
    struct ImageFileHeader
    {
        public ushort Machine;
        public short NumberOfSections;
        public int TimeDateStamp;
        public int PointerToSymbolTable;
        public int NumberOfSymbols;
        public short SizeOfOptionalHeader;
        public short Characteristics;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct ImageOptionalHeaderStandardFields
    {
        //
        // Standard fields.
        //
        public short Magic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public int SizeOfCode;
        public int SizeOfInitializedData;
        public int SizeOfUninitializedData;
        public int AddressOfEntryPoint;
        public int BaseOfCode;
        //public int BaseOfData;
    };

    [StructLayout(LayoutKind.Sequential)]
    struct ImageOptionalHeader32
    {
        //
        // Standard fields.
        //

        // ImageOptionalHeaderStandardFields (already read)
        //short Magic;
        //byte MajorLinkerVersion;
        //byte MinorLinkerVersion;
        //int SizeOfCode;
        //int SizeOfInitializedData;
        //int SizeOfUninitializedData;
        //int AddressOfEntryPoint;
        //int BaseOfCode;
        public int BaseOfData;

        //
        // NT additional fields.
        //

        public int ImageBase;
        public int SectionAlignment;
        public int FileAlignment;
        public short MajorOperatingSystemVersion;
        public short MinorOperatingSystemVersion;
        public short MajorImageVersion;
        public short MinorImageVersion;
        public short MajorSubsystemVersion;
        public short MinorSubsystemVersion;
        public int Win32VersionValue;
        public int SizeOfImage;
        public int SizeOfHeaders;
        public int CheckSum;
        public short Subsystem;
        public short DllCharacteristics;
        public int SizeOfStackReserve;
        public int SizeOfStackCommit;
        public int SizeOfHeapReserve;
        public int SizeOfHeapCommit;
        public int LoaderFlags;
        public int NumberOfRvaAndSizes;
        // IMAGE_DATA_DIRECTORY DataDirectory[IMAGE_NUMBEROF_DIRECTORY_ENTRIES];
    };

    [StructLayout(LayoutKind.Sequential)]
    struct ImageOptionalHeader64
    {
        // ImageOptionalHeaderStandardFields (already read)
        //short Magic;
        //byte MajorLinkerVersion;
        //byte MinorLinkerVersion;
        //int SizeOfCode;
        //int SizeOfInitializedData;
        //int SizeOfUninitializedData;
        //int AddressOfEntryPoint;
        //int BaseOfCode;

        public long ImageBase;
        public int SectionAlignment;
        public int FileAlignment;
        public short MajorOperatingSystemVersion;
        public short MinorOperatingSystemVersion;
        public short MajorImageVersion;
        public short MinorImageVersion;
        public short MajorSubsystemVersion;
        public short MinorSubsystemVersion;
        public int Win32VersionValue;
        public int SizeOfImage;
        public int SizeOfHeaders;
        public int CheckSum;
        public short Subsystem;
        public short DllCharacteristics;
        public long SizeOfStackReserve;
        public long SizeOfStackCommit;
        public long SizeOfHeapReserve;
        public long SizeOfHeapCommit;
        public int LoaderFlags;
        public int NumberOfRvaAndSizes;

        //IMAGE_DATA_DIRECTORY DataDirectory[IMAGE_NUMBEROF_DIRECTORY_ENTRIES];
    };

    internal enum Directories : ushort
    {
        Export,
        Import,
        Resource,
        Exception,
        Certificate,
        BaseRelocation,
        Debug,
        Copyright,
        GlobalPointer,
        ThreadLocalStorage,
        LoadConfig,
        BoundImport,
        ImportAddress,
        DelayImport,
        COR20Header,
        Reserved,
        Cor20HeaderMetaData,
        Cor20HeaderResources,
        Cor20HeaderStrongNameSignature,
        Cor20HeaderCodeManagerTable,
        Cor20HeaderVtableFixups,
        Cor20HeaderExportAddressTableJumps,
        Cor20HeaderManagedNativeHeader,
    }

    /// <summary>
    /// Target CPU types.
    /// </summary>
    internal enum Machine : ushort
    {
        /// <summary>
        /// The target CPU is unknown or not specified.
        /// </summary>
        Unknown = 0x0000,
        /// <summary>
        /// Intel 386.
        /// </summary>
        I386 = 0x014C,
        /// <summary>
        /// MIPS little-endian
        /// </summary>
        R3000 = 0x0162,
        /// <summary>
        /// MIPS little-endian
        /// </summary>
        R4000 = 0x0166,
        /// <summary>
        /// MIPS little-endian
        /// </summary>
        R10000 = 0x0168,
        /// <summary>
        /// MIPS little-endian WCE v2
        /// </summary>
        WCEMIPSV2 = 0x0169,
        /// <summary>
        /// Alpha_AXP
        /// </summary>
        Alpha = 0x0184,
        /// <summary>
        /// SH3 little-endian
        /// </summary>
        SH3 = 0x01a2,
        /// <summary>
        /// SH3 little-endian. DSP.
        /// </summary>
        SH3DSP = 0x01a3,
        /// <summary>
        /// SH3E little-endian.
        /// </summary>
        SH3E = 0x01a4,
        /// <summary>
        /// SH4 little-endian.
        /// </summary>
        SH4 = 0x01a6,
        /// <summary>
        /// SH5.
        /// </summary>
        SH5 = 0x01a8,
        /// <summary>
        /// ARM Little-Endian
        /// </summary>
        ARM = 0x01c0,
        /// <summary>
        /// Thumb.
        /// </summary>
        Thumb = 0x01c2,
        /// <summary>
        /// AM33
        /// </summary>
        AM33 = 0x01d3,
        /// <summary>
        /// IBM PowerPC Little-Endian
        /// </summary>
        PowerPC = 0x01F0,
        /// <summary>
        /// PowerPCFP
        /// </summary>
        PowerPCFP = 0x01f1,
        /// <summary>
        /// Intel 64
        /// </summary>
        IA64 = 0x0200,
        /// <summary>
        /// MIPS
        /// </summary>
        MIPS16 = 0x0266,
        /// <summary>
        /// ALPHA64
        /// </summary>
        Alpha64 = 0x0284,
        /// <summary>
        /// MIPS
        /// </summary>
        MIPSFPU = 0x0366,
        /// <summary>
        /// MIPS
        /// </summary>
        MIPSFPU16 = 0x0466,
        /// <summary>
        /// AXP64
        /// </summary>
        AXP64 = Alpha64,
        /// <summary>
        /// Infineon
        /// </summary>
        Tricore = 0x0520,
        /// <summary>
        /// CEF
        /// </summary>
        CEF = 0x0CEF,
        /// <summary>
        /// EFI Byte Code
        /// </summary>
        EBC = 0x0EBC,
        /// <summary>
        /// AMD64 (K8)
        /// </summary>
        AMD64 = 0x8664,
        /// <summary>
        /// M32R little-endian
        /// </summary>
        M32R = 0x9041,
        /// <summary>
        /// CEE
        /// </summary>
        CEE = 0xC0EE,
    }

    internal enum PEMagic : ushort
    {
        PEMagic32 = 0x010B,
        PEMagic64 = 0x020B,
    }

    internal enum Characteristics : ushort
    {
        RelocsStripped = 0x0001,         // Relocation info stripped from file.
        ExecutableImage = 0x0002,        // File is executable  (i.e. no unresolved external references).
        LineNumsStripped = 0x0004,       // Line numbers stripped from file.
        LocalSymsStripped = 0x0008,      // Local symbols stripped from file.
        AggressiveWsTrim = 0x0010,       // Agressively trim working set
        LargeAddressAware = 0x0020,      // App can handle >2gb addresses
        BytesReversedLo = 0x0080,        // Bytes of machine word are reversed.
        Bit32Machine = 0x0100,           // 32 bit word machine.
        DebugStripped = 0x0200,          // Debugging info stripped from file in .DBG file
        RemovableRunFromSwap = 0x0400,   // If Image is on removable media, copy and run from the swap file.
        NetRunFromSwap = 0x0800,         // If Image is on Net, copy and run from the swap file.
        System = 0x1000,                 // System File.
        Dll = 0x2000,                    // File is a DLL.
        UpSystemOnly = 0x4000,           // File should only be run on a UP machine
        BytesReversedHi = 0x8000,        // Bytes of machine word are reversed.
    }


    [StructLayout(LayoutKind.Sequential)]
    internal struct ImageDataDirectory
    {
        public int VirtualAddress;
        public int Size;
    };


    internal enum COR20Flags : uint
    {
        ILOnly = 0x00000001,
        Bit32Required = 0x00000002,
        ILLibrary = 0x00000004,
        StrongNameSigned = 0x00000008,
        NativeEntryPoint = 0x00000010,
        TrackDebugData = 0x00010000,
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct COR20Header
    {
        internal int CountBytes;
        internal ushort MajorRuntimeVersion;
        internal ushort MinorRuntimeVersion;
        internal ImageDataDirectory MetaDataDirectory;
        internal COR20Flags COR20Flags;
        internal uint EntryPointTokenOrRVA;
        internal ImageDataDirectory ResourcesDirectory;
        internal ImageDataDirectory StrongNameSignatureDirectory;
        internal ImageDataDirectory CodeManagerTableDirectory;
        internal ImageDataDirectory VtableFixupsDirectory;
        internal ImageDataDirectory ExportAddressTableJumpsDirectory;
        internal ImageDataDirectory ManagedNativeHeaderDirectory;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MetadataHeader
    {
        internal uint Signature;
        internal ushort MajorVersion;
        internal ushort MinorVersion;
        internal uint ExtraData;
        internal int VersionStringSize;
        //internal string VersionString;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SectionHeader
    {
        internal byte c1;
        internal byte c2;
        internal byte c3;
        internal byte c4;
        internal byte c5;
        internal byte c6;
        internal byte c7;
        internal byte c8;
        internal int VirtualSize;
        internal int VirtualAddress;
        internal int SizeOfRawData;
        internal int OffsetToRawData;
        internal int RVAToRelocations;
        internal int PointerToLineNumbers;
        internal ushort NumberOfRelocations;
        internal ushort NumberOfLineNumbers;
        internal uint SectionCharacteristics;

        public string Name
        {
            get
            {
                byte[] data = new byte[] { c1, c2, c3, c4, c5, c6, c7, c8 };
                return MinPEFileReader.GetAsciiString(data);
            }
        }
    }

    internal enum SectionCharacteristics : uint
    {
        TypeReg = 0x00000000,               // Reserved.
        TypeDSect = 0x00000001,             // Reserved.
        TypeNoLoad = 0x00000002,            // Reserved.
        TypeGroup = 0x00000004,             // Reserved.
        TypeNoPad = 0x00000008,             // Reserved.
        TypeCopy = 0x00000010,              // Reserved.

        CNTCode = 0x00000020,               // Section contains code.
        CNTInitializedData = 0x00000040,    // Section contains initialized data.
        CNTUninitializedData = 0x00000080,  // Section contains uninitialized data.

        LNKOther = 0x00000100,            // Reserved.
        LNKInfo = 0x00000200,             // Section contains comments or some other type of information.
        TypeOver = 0x00000400,            // Reserved.
        LNKRemove = 0x00000800,           // Section contents will not become part of image.
        LNKCOMDAT = 0x00001000,           // Section contents comdat.
        //                                0x00002000  // Reserved.
        MemProtected = 0x00004000,
        No_Defer_Spec_Exc = 0x00004000,   // Reset speculative exceptions handling bits in the TLB entries for this section.
        GPRel = 0x00008000,               // Section content can be accessed relative to GP
        MemFardata = 0x00008000,
        MemSysheap = 0x00010000,
        MemPurgeable = 0x00020000,
        Mem16Bit = 0x00020000,
        MemLocked = 0x00040000,
        MemPreload = 0x00080000,

        Align1Bytes = 0x00100000,     //
        Align2Bytes = 0x00200000,     //
        Align4Bytes = 0x00300000,     //
        Align8Bytes = 0x00400000,     //
        Align16Bytes = 0x00500000,    // Default alignment if no others are specified.
        Align32Bytes = 0x00600000,    //
        Align64Bytes = 0x00700000,    //
        Align128Bytes = 0x00800000,   //
        Align256Bytes = 0x00900000,   //
        Align512Bytes = 0x00A00000,   //
        Align1024Bytes = 0x00B00000,  //
        Align2048Bytes = 0x00C00000,  //
        Align4096Bytes = 0x00D00000,  //
        Align8192Bytes = 0x00E00000,  //
        // Unused                     0x00F00000
        AlignMask = 0x00F00000,

        LNKNRelocOvfl = 0x01000000,   // Section contains extended relocations.
        MemDiscardable = 0x02000000,  // Section can be discarded.
        MemNotCached = 0x04000000,    // Section is not cachable.
        MemNotPaged = 0x08000000,     // Section is not pageable.
        MemShared = 0x10000000,       // Section is shareable.
        MemExecute = 0x20000000,      // Section is executable.
        MemRead = 0x40000000,         // Section is readable.
        MemWrite = 0x80000000,        // Section is writeable.
    }

}
