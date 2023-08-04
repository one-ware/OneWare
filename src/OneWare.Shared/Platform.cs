using System.Runtime.InteropServices;

namespace OneWare.Shared
{
    public static class Platform
    {
        public enum PlatformId
        {
            Win32S = 0,
            Win32Windows = 1,
            Win32Nt = 2,
            WinCe = 3,
            Unix = 4,
            Xbox = 5,
            MacOsx = 6
        }
        
        public static string ExecutableExtension
        {
            get
            {
                switch (PlatformIdentifier)
                {
                    case PlatformId.Unix:
                    case PlatformId.MacOsx:
                    {
                        return string.Empty;
                    }

                    case PlatformId.Win32Nt:
                    {
                        return ".exe";
                    }

                    default:
                        throw new NotImplementedException("Not implemented for your platform.");
                }
            }
        }

        public static PlatformId PlatformIdentifier
        {
            get
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return PlatformId.Win32Nt;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return PlatformId.Unix;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) 
                    return PlatformId.MacOsx;
                
                return PlatformId.Unix;
            }
        }
    }
}