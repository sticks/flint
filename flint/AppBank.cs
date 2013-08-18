using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace flint
{
    /// <summary>
    /// Represents the Appbank present on the Pebble.
    /// </summary>
    /// <remarks>
    /// Data structure gleaned from https://github.com/pebble/libpebble/blob/f230d8c96e3ffbf011adbba95443c447852ff707/pebble/pebble.py#L678 
    /// </remarks>
    public class AppBank
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        public struct App
        {
            [MarshalAs(UnmanagedType.U4)]
            public readonly uint ID;
            [MarshalAs(UnmanagedType.U4)]
            public readonly uint Index;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public readonly String Name;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public readonly String Company;
            [MarshalAs(UnmanagedType.U4)]
            public readonly uint Flags;
            /*
            [MarshalAs(UnmanagedType.U1)]
            public readonly byte MajorVersion;
            [MarshalAs(UnmanagedType.U1)]
            public readonly byte MinorVersion;
             * */
            public readonly ushort RawVersion;
            /// <summary> A string representation of the app version. </summary>
            //public String Version { get { return String.Format("{0}.{1}", MajorVersion, MinorVersion); } }
            public String Version { get { return String.Format("{0}.{1}", RawVersion >> 8 , (byte)RawVersion); } }
            public override string ToString()
            {
                String format = "{0}, version {1} by {2}";
                return String.Format(format, Name, Version, Company);
            }

        }
        /// <summary> The number of available (free and occupied) app slots (?) </summary>
        public uint Size { get; private set; }

        public List<App> Apps { get; private set; }

        /// <summary>
        /// Load appbank data from the data received from a Pebble
        /// </summary>
        /// <param name="bytes">The entire payload from an appropriate APP_MANAGER message.</param>
        public AppBank(byte[] bytes)
        {
            int headersize = 9;
            int appinfosize = 78;
            Apps = new List<App>();
            if (bytes.Count() < headersize)
            {
                throw new ArgumentOutOfRangeException("Payload is shorter than 9 bytes, "+
                    "which is the minimum size for an appbank content response.");
            }
            var items = Util.Unpack("!bII", bytes);
            
            Size = (uint)items[1];

            uint appcount = (uint)items[2];
            if (bytes.Count() < headersize + appcount * appinfosize)
            {
                throw new ArgumentOutOfRangeException("Payload is not large enough for the claimed number of installed apps.");
            }

            for (int i = 0; i < appcount; i++)
            {
                Apps.Add(AppFromBytes(bytes.Skip(headersize + i * appinfosize).Take(appinfosize).ToArray()));
            }
            foreach (var app in Apps)
            {
                Console.WriteLine("{0} {1} {2}", app.ID, app.Index, app.Version);
            }
        }

        /// <summary>
        /// Populate an App struct from the 78 appinfo bytes.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static App AppFromBytes(byte[] bytes)
        {
            if (bytes.Count() != 78)
            {
                throw new ArgumentException("Provided byte array is not 78 bytes in size");
            }
            return Util.ReadStruct<App>(bytes);
        }

        public override string ToString()
        {
            String ret = "";
            foreach (App app in Apps)
            {
                ret += app.ToString();
                ret += "\n";
            }
            return ret;
        }

    }
}
