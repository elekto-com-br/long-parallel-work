using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Management;
using System.Text.RegularExpressions;

namespace Elekto.Diagnostics
{
    /// <summary>
    ///     Classe para obter, via P/Invoke, informações sobre as CPUs da máquina
    /// </summary>
    /// <remarks>
    ///     Adaptada a partir da versão original de Michael Vanhoutte em
    ///     http://blogs.adamsoftware.net/Engine/DeterminingthenumberofphysicalCPUsonWindows.aspx
    /// </remarks>
    public static class Machine
    {
        private const int ErrorInsufficientBuffer = 122;

        /// <summary>
        ///     Gets <b>true</b> if this process is running in a 64 bit
        ///     environment, <b>false</b> otherwise.
        /// </summary>
        public static bool Is64BitProcess
        {
            get { return Marshal.SizeOf(typeof (IntPtr)) == 8; }
        }

        /// <summary>
        ///     Gets <b>true</b> if this is a 64 bit Windows.
        /// </summary>
        public static bool Is64BitWindows
        {
            get
            {
                // The purpose is to know if we're running in pure 32-bit 
                // or if we're running in an emulated 32-bit environment.
                // Earlier versions of this method checked for the existence 
                // of the HKLM\Software\Wow6432Node node, but this turned 
                // out to be not realiable. Apparently, this node can exist 
                // on a 32-bit Windows as well.
                try
                {
                    var sArchitecture = Environment.GetEnvironmentVariable(
                        "PROCESSOR_ARCHITECTURE", EnvironmentVariableTarget.Machine);
                    if (sArchitecture == null)
                    {
                        return false;
                    }
                    return sArchitecture.Contains("64");
                }
                catch (NotSupportedException)
                {
                    return false;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
        }

        /// <summary>
        ///     Returns <b>true</b> if this is a 32-bit process
        ///     running on a 64-bit server.
        /// </summary>
        public static bool IsWow64Process
        {
            get { return Is64BitWindows && !Is64BitProcess; }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformation(
            [Out] SystemLogicalProcessorInformatioNx86[] infos,
            ref uint infoSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetLogicalProcessorInformation(
            [Out] SystemLogicalProcessorInformatioNx64[] infos,
            ref uint infoSize);

        private static List<ProcessorInfo> GetProcessorInfo86()
        {
            // First we're going to execute GetLogicalProcessorInformation 
            // once to make sure that we determine the size of the data 
            // that it is going to return.
            // This call should fail with error ERROR_INSUFFICIENT_BUFFER.
            uint iReturnLength = 0;
            SystemLogicalProcessorInformatioNx86[] oDummy = null;
            var bResult = GetLogicalProcessorInformation(oDummy,
                ref iReturnLength);
            if (bResult)
            {
                throw Fail("GetLogicalProcessorInformation failed.", "x86");
            }

            // Making sure that the error code that we got back isn't that 
            // there is insufficient space in the buffer.
            var iError = Marshal.GetLastWin32Error();
            if (iError != ErrorInsufficientBuffer)
            {
                throw Fail(
                    "Insufficient space in the buffer.",
                    "x86", iError.ToString(CultureInfo.InvariantCulture));
            }

            // Now that we know how much space we should reserve, 
            // we're going to reserve it and call 
            // GetLogicalProcessorInformation again.
            var iBaseSize = (uint) Marshal.SizeOf(
                typeof (SystemLogicalProcessorInformatioNx86));
            var iNumberOfElements = iReturnLength/iBaseSize;
            var oData =
                new SystemLogicalProcessorInformatioNx86[iNumberOfElements];
            var iAllocatedSize = iNumberOfElements*iBaseSize;
            if (!GetLogicalProcessorInformation(oData, ref iAllocatedSize))
            {
                throw Fail(
                    "GetLogicalProcessorInformation failed",
                    "x86",
                    Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture));
            }

            // Converting the data to a list that we can easily interpret.
            return oData.Select(oInfo => new ProcessorInfo(oInfo.Relationship, oInfo.Flags, oInfo.ProcessorMask)).ToList();
        }

        private static List<ProcessorInfo> GetProcessorInfo64()
        {
            // First we're going to execute GetLogicalProcessorInformation 
            // once to make sure that we determine the size of the data 
            // that it is going to return.
            // This call should fail with error ERROR_INSUFFICIENT_BUFFER.
            uint iReturnLength = 0;
            SystemLogicalProcessorInformatioNx64[] oDummy = null;
            var bResult = GetLogicalProcessorInformation(oDummy,
                ref iReturnLength);
            if (bResult)
            {
                throw Fail("GetLogicalProcessorInformation failed.", "x64");
            }

            // Making sure that the error code that we got back is not  
            // that there is in sufficient space in the buffer.
            var iError = Marshal.GetLastWin32Error();
            if (iError != ErrorInsufficientBuffer)
            {
                throw Fail(
                    "Insufficient space in the buffer.",
                    "x64", iError.ToString());
            }

            // Now that we know how much space we should reserve, 
            // we're going to reserve it and call 
            // GetLogicalProcessorInformation again.
            var iBaseSize = (uint) Marshal.SizeOf(
                typeof (SystemLogicalProcessorInformatioNx64));
            var iNumberOfElements = iReturnLength/iBaseSize;
            var oData =
                new SystemLogicalProcessorInformatioNx64[iNumberOfElements];
            var iAllocatedSize = iNumberOfElements*iBaseSize;
            if (!GetLogicalProcessorInformation(oData, ref iAllocatedSize))
            {
                throw Fail("GetLogicalProcessorInformation failed",
                    "x64", Marshal.GetLastWin32Error().ToString(CultureInfo.InvariantCulture));
            }

            // Converting the data to a list that we can easily interpret.
            return oData.Select(oInfo => new ProcessorInfo(oInfo.Relationship, oInfo.Flags, oInfo.ProcessorMask)).ToList();
        }

        private static Exception Fail(params string[] data)
        {
            return new NotSupportedException(
                "GetPhysicalProcessorCount unexpectedly failed " +
                "(" + String.Join(", ", data) + ")");
        }

        public static IEnumerable<ProcessorInfo> GetProcessorsInfo()
        {
            return
                GetInternalRelations()
                    .Where(
                        pi =>
                            pi.Relationship == RelationProcessorCore.RelationNumaNode ||
                            pi.Relationship == RelationProcessorCore.RelationProcessorCore ||
                            pi.Relationship == RelationProcessorCore.RelationProcessorPackage).OrderBy(pi => pi.Relationship).ThenBy(pi => pi.ProcessorMask);
        }

        public static IEnumerable<ProcessorInfo> GetInternalRelations()
        {
            if (!Is64BitProcess)
            {
                var oVersion = Environment.OSVersion.Version;
                if (oVersion < new Version(5, 1, 2600))
                {
                    throw new NotSupportedException(
                        "GetPhysicalProcessorCount is not supported " +
                        "on this operating system.");
                }
                if (oVersion.Major == 5 &&
                    oVersion.Minor == 1 &&
                    !Environment.OSVersion.ServicePack.Equals(
                        "Service Pack 3",
                        StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException(
                        "GetPhysicalProcessorCount is not supported " +
                        "on this operating system.");
                }
            }

            // Getting a list of processor information
            var oList = Is64BitProcess ? GetProcessorInfo64() : GetProcessorInfo86();

            return oList;
        }

        public static int GetPhysicalProcessorCount()
        {
            // Getting a list of processor information
            var oList = GetInternalRelations().ToList();

            // The list will basically contain something like this at this point:
            //
            // E.g. for a 2 x single core
            // Relationship              Flags      ProcessorMask
            // ---------------------------------------------------------
            // RelationProcessorCore     0          1
            // RelationProcessorCore     0          2
            // RelationNumaNode          0          3
            //
            // E.g. for a 2 x dual core
            // Relationship              Flags      ProcessorMask
            // ---------------------------------------------------------
            // RelationProcessorCore     1          5
            // RelationProcessorCore     1          10
            // RelationNumaNode          0          15
            //
            // E.g. for a 1 x quad core
            // Relationship              Flags      ProcessorMask
            // ---------------------------------------------------------
            // RelationProcessorCore     1          15
            // RelationNumaNode          0          15
            //
            // E.g. for a 1 x dual core
            // Relationship              Flags      ProcessorMask  
            // ---------------------------------------------------------
            // RelationProcessorCore     0          1              
            // RelationCache             1          1              
            // RelationCache             1          1              
            // RelationProcessorPackage  0          3              
            // RelationProcessorCore     0          2              
            // RelationCache             1          2              
            // RelationCache             1          2              
            // RelationCache             2          3              
            // RelationNumaNode          0          3
            // 
            // Vista or higher will return one RelationProcessorPackage 
            // line per socket. On other operating systems we need to 
            // interpret the RelationProcessorCore lines.
            //
            // More information:
            // http://msdn2.microsoft.com/en-us/library/ms683194(VS.85).aspx
            // http://msdn2.microsoft.com/en-us/library/ms686694(VS.85).aspx

            // First counting the number of RelationProcessorPackage lines
            var iCount = oList.Count(oItem => oItem.Relationship == RelationProcessorCore.RelationProcessorPackage);
            if (iCount > 0)
            {
                return iCount;
            }

            // Now we're going to use the information in RelationProcessorCore.
            iCount = oList.Count(oItem => oItem.Relationship == RelationProcessorCore.RelationProcessorCore);

            if (iCount > 0)
            {
                return iCount;
            }

            throw Fail("No cpus have been detected.");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CacheDescriptor
        {
            private readonly byte Level;
            private readonly byte Associativity;
            private readonly UInt16 LineSize;
            private readonly UInt32 Size;
            [MarshalAs(UnmanagedType.U4)] private readonly ProcessorCacheType Type;
        }

        private enum ProcessorCacheType
        {
            /// <summary>
            ///     The cache is unified.
            /// </summary>
            UnifiedCache = 0,

            /// <summary>
            ///     InstructionThe cache is for processor instructions.
            /// </summary>
            InstructionCache = 1,

            /// <summary>
            ///     The cache is for data.
            /// </summary>
            DataCache = 2,

            /// <summary>
            ///     TraceThe cache is for traces.
            /// </summary>
            TraceCache = 3
        }

        public class ProcessorInfo
        {
            private readonly byte _flags;
            private readonly uint _processorMask;
            private readonly RelationProcessorCore _relationship;

            public ProcessorInfo(RelationProcessorCore relationShip,
                byte flags, uint processorMask)
            {
                _relationship = relationShip;
                _flags = flags;
                _processorMask = processorMask;
            }

            public RelationProcessorCore Relationship
            {
                get { return _relationship; }
            }

            public byte Flags
            {
                get { return _flags; }
            }

            public uint ProcessorMask
            {
                get { return _processorMask; }
            }
        }

        public enum RelationProcessorCore
        {
            /// <summary>
            ///     The specified logical processors share a
            ///     single processor core.
            /// </summary>
            RelationProcessorCore = 0,

            /// <summary>
            ///     The specified logical processors are part
            ///     of the same NUMA node.
            /// </summary>
            RelationNumaNode = 1,

            /// <summary>
            ///     The specified logical processors  share a cache.
            ///     Windows Server 2003:  This value is not supported
            ///     until Windows Server 2003 SP1 and Windows XP
            ///     Professional x64 Edition.
            /// </summary>
            RelationCache = 2,

            /// <summary>
            ///     The specified logical processors share a physical
            ///     package (a single package socketed or soldered
            ///     onto a motherboard may contain multiple processor
            ///     cores or threads, each of which is treated as a
            ///     separate processor by the operating system).
            ///     Windows Server 2003:  This value is not
            ///     supported until Windows Vista.
            /// </summary>
            RelationProcessorPackage = 3
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SystemLogicalProcessorInformatioNx64
        {
            [FieldOffset(0)] public readonly uint ProcessorMask;
            [FieldOffset(8), MarshalAs(UnmanagedType.U4)] public readonly RelationProcessorCore Relationship;
            [FieldOffset(12)] public readonly byte Flags;
            [FieldOffset(12)] public readonly CacheDescriptor Cache;
            [FieldOffset(12)] public readonly UInt32 NodeNumber;
            [FieldOffset(12)] public readonly UInt64 Reserved1;
            [FieldOffset(20)] public readonly UInt64 Reserved2;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct SystemLogicalProcessorInformatioNx86
        {
            [FieldOffset(0)] public readonly uint ProcessorMask;
            [FieldOffset(4), MarshalAs(UnmanagedType.U4)] public readonly RelationProcessorCore Relationship;
            [FieldOffset(8)] public readonly byte Flags;
            [FieldOffset(8)] public readonly CacheDescriptor Cache;
            [FieldOffset(8)] public readonly UInt32 NodeNumber;
            [FieldOffset(8)] public readonly UInt64 Reserved1;
            [FieldOffset(16)] public readonly UInt64 Reserved2;
        }

        private static string CollapseSpaces(this string value)
        {
            return Regex.Replace(value, @"\s+", " ");
        }

        /// <summary>
        /// Id do processador
        /// </summary>
        public static string GetProcessorId()
        {
            var mbs = new ManagementObjectSearcher("Select * From Win32_processor");
            var mbsList = mbs.Get();
            foreach (ManagementObject mo in mbsList)
            {
                return
                    string.Format("{0}.{1}.{2}.{3}.{4}.{5}",
                        mo["Manufacturer"], mo["Name"].ToString().Trim().CollapseSpaces(), mo["ProcessorType"], mo["Family"], mo["Revision"],
                        mo["Stepping"]);
            }
            return string.Empty;
        }
    }
}