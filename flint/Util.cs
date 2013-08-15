﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace flint
{

    public static class Util
    {
        /// <summary>
        /// helper method to gather info from format element if it is a string
        /// </summary>
        /// <param name="format">format string</param>
        /// <param name="i">cur index into format string</param>
        /// <param name="slen">length of string</param>
        /// <param name="digcount">number of digits consumed by string def</param>
        /// <returns>number of format characters subsumed by string</returns>
        private static bool isString(string format, int i, out int slen, out int digcount)
        {
            char c = format[i];
            slen = 0;
            digcount = 0;
            while (((i + digcount) < format.Length) && c >= '0' && c <= '9')
            {
                digcount++;
                c = format[i + digcount];
            }
            if (digcount > 0)
            {
                if (c != 's')
                    throw new ArgumentOutOfRangeException("format", format, "only 's' can follow digits");
                slen += Int32.Parse(format.Substring(i, digcount));
                return true; // past digits but not s
            }
            if (c == 's') // special case of missing count
            {
                slen = 1;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Helper to provide size of data based on format string's elements
        /// </summary>
        /// <param name="format">format string to inspect</param>
        /// <returns>size of byte[] encoded by format string</returns>
        private static int dataSize(string format)
        {
            int flen = 0;
            for(int i=0;i<format.Length;++i)
            {
                int slen,spos;
                if (isString(format, i, out slen, out spos))
                {
                    flen += slen;
                    i += spos;
                    continue;
                }
                if (spos>0)
                {
                    flen += slen;
                    i += spos;
                    continue;
                }
                char c = format[i];
                switch (c)
                {
                    case 'b':
                    case 'B': flen++; break;
                    case 'l':
                    case 'L':
                    case 'i':
                    case 'I': flen += 4; break;
                    case 'h':
                    case 'H': flen += 2; break;
                    case '!': break;
                    default: throw new ArgumentOutOfRangeException("format", format, "Unknown format character");
                }
            }
            return flen;
        }
        /// <summary>
        /// Mimic's pythons struct.unpack()
        /// ! = network byte order
        /// b/B signed/unsigned byte 
        /// h/H signed/unsigned short (2 bytes)
        /// i/I signed/unsigned int (4 bytes)
        /// l/L signed/unsigned long (4 bytes)
        /// [size]s string (with optional size, defaults to 1); '0s' means empty string
        /// </summary>
        /// <param name="format">Only '!bBhHlLiI[xx]s' are implemented</param>
        /// <param name="data">packed byte array</param>
        /// <returns>unpacked primitives</returns>

        public static object[] Unpack(string format, byte[] data)
        {
            int dlen = dataSize(format);
            // allow sloppy unpacking so that the beginning of a byte[] can contain packed info
            if (data.Length < dlen)
                throw new Exception("unexpected number of bytes");
            var fpos = 0;
            bool networkOrder = format[0] == '!';
            if (networkOrder) fpos++;

            List<object> items = new List<object>();
            int flen = format.Length; //
            for (int dpos = 0; fpos < flen; ++fpos)
            {
                int slen, spos;
                if (isString(format, fpos, out slen, out spos))
                {
                    if (slen == 0) // special case empty string
                        items.Add("");
                    else
                    {
                        int shrinkNulls = slen; // s'posed to ignore nulls within slen
                        while (data[dpos + shrinkNulls-1] == 0)
                            shrinkNulls--;
                        var s = Encoding.UTF8.GetString(data, dpos, shrinkNulls);
                        items.Add(s);
                        dpos += slen;
                    }
                    fpos += spos;
                    continue;
                }
                char c = format[fpos];
                switch (c)
                {
                    case 'b':
                        {
                            items.Add((sbyte)data[dpos++]);
                        }
                        break;
                    case 'B':
                        {
                            items.Add(data[dpos++]);
                        }
                        break;
                    case 'i':
                    case 'I':
                    case 'l':
                    case 'L':
                    case 'h':
                    case 'H':
                        {
                            bool isShort = c == 'h' || c == 'H';
                            bool isSigned = Char.IsLower(c);
                            int nlen = isShort ? 2 : 4;
                            var bytes = data.Skip(dpos).Take(nlen);
                            if (BitConverter.IsLittleEndian && networkOrder)
                                bytes = bytes.Reverse();
                            if (isSigned)
                            {
                                if (isShort)
                                {
                                    short s = BitConverter.ToInt16(bytes.ToArray(), 0);
                                    items.Add(s);
                                }
                                else
                                {
                                    int i = BitConverter.ToInt32(bytes.ToArray(), 0);
                                    items.Add(i);
                                }
                            }
                            else
                            {
                                if (isShort)
                                {
                                    ushort s = BitConverter.ToUInt16(bytes.ToArray(), 0);
                                    items.Add(s);
                                }
                                else
                                {
                                    uint i = BitConverter.ToUInt32(bytes.ToArray(), 0);
                                    items.Add(i);
                                }
                            }
                            dpos += nlen;
                        }
                        break;
                }
            }
            return items.ToArray();
        }
        /// <summary>
        /// Mimic's pythons struct.pack()
        /// ! = network byte order
        /// b/B signed/unsigned byte 
        /// h/H signed/unsigned short (2 bytes)
        /// i/I signed/unsigned int (4 bytes)
        /// l/L signed/unsigned long (4 bytes)
        /// [size]s string (with optional size, defaults to 1); '0s' means empty string
        /// </summary>
        /// <param name="format">Only '!bBhHlLiI[xx]s' are implemented</param>
        /// <param name="items">items to pack</param>
        /// <returns>packed byte array</returns>
        public static byte[] Pack(string format, params object[] items)
        {
            int len = dataSize(format);
            if (len == 0)
                throw new Exception("no format string");

            var data = new byte[len];
            var fpos = 0;
            bool networkOrder = format[0] == '!';
            if (networkOrder) fpos++;
            for (int ipos=0,dpos=0; dpos < len; ++fpos,++ipos)
            {
                object item = items[ipos];
                int slen,spos;
                if (isString(format,fpos,out slen,out spos))
                {
                    if (slen != 0) // special case, empty string
                    {
                        var sbytes = Encoding.UTF8.GetBytes((string)item);
                        Array.Copy(sbytes, 0, data, dpos, Math.Min(sbytes.Length,slen)); // only copy how many bytes they claim they want or they actually have
                        dpos += slen;
                    }
                    fpos += spos;
                    continue;
                }
                char c = format[fpos];
                switch (c)
                {
                    case 'b':
                        {
                            data[dpos++] = (byte)Convert.ToSByte(item);
                        }
                        break;
                    case 'B':
                        {
                            data[dpos++] = Convert.ToByte(item);
                        }
                        break;
                    case 'I':
                    case 'i':
                    case 'L':
                    case 'l':
                    case 'H':
                    case 'h':
                        {
                            bool isShort = c == 'h' || c == 'H';
                            bool isSigned = Char.IsLower(c);
                            int nlen = isShort ? 2 : 4;
                            var bytes = data.Skip(dpos).Take(nlen);
                            byte[] arr;
                            if (isSigned)
                            {
                                if (isShort)
                                {
                                    short i = Convert.ToInt16(item);
                                    arr = BitConverter.GetBytes(i);
                                }
                                else
                                {
                                    int i = Convert.ToInt32(item);
                                    arr = BitConverter.GetBytes(i);
                                }
                            }
                            else
                            {
                                if (isShort)
                                {
                                    ushort i = Convert.ToUInt16(item);
                                    arr = BitConverter.GetBytes(i);
                                }
                                else
                                {
                                    uint i = Convert.ToUInt32(item);
                                    arr = BitConverter.GetBytes(i);
                                }
                            }
                            if (networkOrder && BitConverter.IsLittleEndian)
                                arr.RCopyTo(data, dpos);
                            else
                                arr.CopyTo(data, dpos);
                            dpos += nlen;
                        }
                        break;
                }
            }
            return data;
        }


        public static void RCopyTo(this Array t, Array r, int pos)
        {
            for (int i = t.Length - 1, j = 0; i >= 0; --i, ++j)
                r.SetValue(t.GetValue(i), pos+j);
        }

        public static void hton(Type type, byte[] data)
        {
            ntoh(type, data);
        }

        public static void ntoh(Type type, byte[] data)
        {
            if (!BitConverter.IsLittleEndian) return;
            var fields = type.GetFields().Where(f => f.FieldType == typeof(Int32))
                .Select(f => new
                {
                    Field = f,
                    Offset = Marshal.OffsetOf(type, f.Name).ToInt32()
                }).ToList();

            foreach (var field in fields)
            {
                Array.Reverse(data, field.Offset, Marshal.SizeOf(field.Field.FieldType));
            }
        }
        /// <summary>
        /// Reads serialized struct data back into a struct, much like fread() might do in C.
        /// </summary>
        /// <param name="fs"></param>
        public static T ReadStruct<T>(Stream fs) where T : struct
        {
            // Borrowed from http://stackoverflow.com/a/1936208 because BitConverter-ing all of this would be a pain
            byte[] buffer = new byte[Marshal.SizeOf(typeof(T))];
            fs.Read(buffer, 0, buffer.Length);
            return ReadStruct<T>(buffer);
        }

        public static byte[] WriteStruct<T>(T str) where T : struct
        {
            byte[] ret = new byte[Marshal.SizeOf(str)];
            GCHandle handle = GCHandle.Alloc(ret, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                Marshal.StructureToPtr(str, ptr, true);
            }
            finally
            {
                handle.Free();
            }
            hton(str.GetType(),ret);
            return ret;

        }

        public static T ReadStruct<T>(byte[] bytes) where T : struct
        {
            ntoh(typeof(T), bytes);

            if (bytes.Count() < Marshal.SizeOf(typeof(T)))
            {
                throw new ArgumentException("Byte array does not match size of target type.");
            }
            T ret;
            GCHandle hdl = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                ret = (T)Marshal.PtrToStructure(hdl.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                hdl.Free();
            }
            return ret;
        }

        /// <summary> Convert a Unix timestamp to a DateTime object.
        /// </summary>
        /// <remarks>
        /// This has some issues, as Pebble isn't timezone-aware and it's 
        /// unclear how either side deals with leap seconds.  For basic usage
        /// this should be plenty, though.
        /// </remarks>
        /// <param name="ts"></param>
        /// <returns></returns>
        public static DateTime TimestampToDateTime(Int32 ts)
        {
            return new DateTime(1970, 1, 1).AddSeconds(ts);
        }

        static uint CRC32_ProcessWord(uint data, uint crc)
        {
            // Crudely ported from https://github.com/pebble/libpebble/blob/master/pebble/stm32_crc.py
            uint poly = 0x04C11DB7;
            crc = crc ^ data;
            for (int i = 0; i < 32; i++)
            {
                if ((crc & 0x80000000) != 0)
                {
                    crc = (crc << 1) ^ poly;
                }
                else
                {
                    crc = (crc << 1);
                }
            }
            return crc;
        }

        /// <summary>
        /// CRC32 function that uses the same parameters etc as Pebble's hardware implementation.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static uint CRC32(byte[] data)
        {
            if (data.Count() % 4 != 0)
            {
                int padsize = 4 - data.Count() % 4;
                data = data.Concat(new byte[padsize]).ToArray();
            }
            uint crc = 0xFFFFFFFF;
            for (int i = 0; i < data.Count(); i += 4)
            {
                uint currentword = BitConverter.ToUInt32(data, i);
                crc = CRC32_ProcessWord(currentword, crc);
            }
            return crc;
        }

    }
}
