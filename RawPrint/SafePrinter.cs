﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace RawPrint
{
    internal class SafePrinter : SafeHandle
    {
        private SafePrinter(IntPtr hPrinter)
            : base(IntPtr.Zero, true)
        {
            handle = hPrinter;
        }

        protected override bool ReleaseHandle()
        {
            if (IsInvalid)
            {
                return false;
            }

            var result = NativeMethods.ClosePrinter(handle) != 0;
            handle = IntPtr.Zero;

            return result;
        }

        public override bool IsInvalid
        {
            get { return handle == IntPtr.Zero; }
        }

        public void StartDocPrinter(DOC_INFO_1 di1)
        {
            if (NativeMethods.StartDocPrinterW(handle, 1, ref di1) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void EndDocPrinter()
        {
            if (NativeMethods.EndDocPrinter(handle) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void StartPagePrinter()
        {
            if (NativeMethods.StartPagePrinter(handle) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void EndPagePrinter()
        {
            if (NativeMethods.EndPagePrinter(handle) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public void WritePrinter(byte[] buffer, int size)
        {
            int written = 0;
            if (NativeMethods.WritePrinter(handle, buffer, size, ref written) == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        public IEnumerable<string> GetPrinterDriverDependentFiles()
        {
            int bufferSize = 0;

            if (NativeMethods.GetPrinterDriver(handle, null, 8, IntPtr.Zero, 0, ref bufferSize) != 0 || Marshal.GetLastWin32Error() != 122) // 122 = ERROR_INSUFFICIENT_BUFFER
            {
                throw new Win32Exception();
            }

            var ptr = Marshal.AllocHGlobal(bufferSize);

            try
            {
                if (NativeMethods.GetPrinterDriver(handle, null, 8, ptr, bufferSize, ref bufferSize) == 0)
                {
                    throw new Win32Exception();
                }

                var di8 = (DRIVER_INFO_8) Marshal.PtrToStructure(ptr, typeof(DRIVER_INFO_8));

                return ReadMultiSz(di8.pDependentFiles).ToList(); // We need a list because FreeHGlobal will be called on return
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static IEnumerable<string> ReadMultiSz(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                yield break;
            }

            var builder = new StringBuilder();
            var pos = ptr;

            while (true)
            {
                var c = (char)Marshal.ReadInt16(pos);

                if (c == '\0')
                {
                    if (builder.Length == 0)
                    {
                        break;
                    }

                    yield return builder.ToString();
                    builder = new StringBuilder();
                }
                else
                {
                    builder.Append(c);
                }

                pos += 2;
            }
        }

        public static SafePrinter OpenPrinter(string printerName, ref PRINTER_DEFAULTS defaults)
        {
            IntPtr hPrinter;

            if (NativeMethods.OpenPrinterW(printerName, out hPrinter, ref defaults) == 0)
            {
                throw new Win32Exception();
            }

            return new SafePrinter(hPrinter);
        }

    }
}