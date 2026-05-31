using System;
using System.Runtime.InteropServices;

namespace Tolk
{
    public class Tolk
    {
        private const string TOLK_DLL = "Tolk.dll";

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Load();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_IsLoaded();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern void Tolk_Unload();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern IntPtr Tolk_DetectScreenReader();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl)]
        private static extern bool Tolk_HasSpeech();

        [DllImport(TOLK_DLL, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
        private static extern bool Tolk_Speak(string str, bool interrupt);

        public void Load()
        {
            Tolk_Load();
        }

        public bool IsLoaded()
        {
            return Tolk_IsLoaded();
        }

        public void Unload()
        {
            Tolk_Unload();
        }

        public string DetectScreenReader()
        {
            IntPtr ptr = Tolk_DetectScreenReader();
            if (ptr == IntPtr.Zero)
                return null;
            return Marshal.PtrToStringUni(ptr);
        }

        public bool HasSpeech()
        {
            return Tolk_HasSpeech();
        }

        public bool Speak(string text, bool interrupt = false)
        {
            return Tolk_Speak(text, interrupt);
        }
    }
}
