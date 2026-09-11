using System.Runtime.InteropServices;

namespace ITMartin.Media.Infrastructure.FileSystem;

// A second name for the same file data, on the same filesystem.
//
// This is what lets a photo appear in People/Charlie, Rejser/Mallorca and Årbog
// 2020 at once without existing three times. Building 22 person folders as
// real copies took over five hours on 2026-09-11 - 1,057 full-size JPEGs for
// one child alone, every byte written to a USB drive - and would have added
// ~25 GB of duplicates to the delivery. As hardlinks the same folders take
// minutes and no space.
//
// WHY HARDLINKS AND NOT SYMLINKS. The delivered library is an NTFS drive handed
// to a person who plugs it into Windows. A symlink created by Linux on NTFS is
// stored in a form Windows does not follow - Explorer shows a broken file - and
// its target is a path that may not exist on the other machine at all. That is
// why the code moved from symlinks to copies in the first place, and the
// reasoning still holds. A hardlink has no target path; NTFS has supported them
// since Windows 2000, and Explorer shows one as an ordinary file.
//
// WHAT CHANGES FOR THE USER, honestly: deleting the photo from one folder does
// not remove it from the others (the data lives while any link does); editing it
// in one folder edits it everywhere (usually wanted - one rotation fixes every
// folder); and copying the drive elsewhere with Explorer or rsync without -H
// expands every link back into a full copy, so the space saving exists on the
// delivered drive only.
//
// .NET has no File.CreateHardLink, so this goes straight to the OS. Failure is
// expected and ordinary - a filesystem that does not support links (exFAT, FAT32
// on many USB sticks), or source and destination on different filesystems - and
// callers fall back to a copy, which is exactly today's behaviour.
public static class HardLink
{
    public static bool TryCreate(string existingFile, string newLink)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                return CreateHardLinkW(newLink, existingFile, IntPtr.Zero);

            return link(existingFile, newLink) == 0;
        }
        catch
        {
            return false;
        }
    }

    // Both names of one file report the same inode - the only reliable way to
    // tell "is this already linked to that" without comparing bytes. Windows has
    // no cheap equivalent, so there it simply answers no and callers relink.
    public static bool IsSameFile(string a, string b)
    {
        if (OperatingSystem.IsWindows()) return false;

        try
        {
            return stat(a, out var sa) == 0 && stat(b, out var sb) == 0
                && sa.st_ino == sb.st_ino && sa.st_dev == sb.st_dev;
        }
        catch
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateHardLinkW(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    [DllImport("libc", SetLastError = true)]
    private static extern int link(string oldpath, string newpath);

    [DllImport("libc", SetLastError = true)]
    private static extern int stat(string path, out StatBuf buf);

    // Only the two fields we read are named; the rest is padding sized to the
    // glibc x86-64 struct stat. Enough to compare identity, nothing more.
    [StructLayout(LayoutKind.Sequential)]
    private struct StatBuf
    {
        public ulong st_dev;
        public ulong st_ino;
        private ulong _pad0;
        private uint _pad1, _pad2, _pad3;
        private int _pad4;
        private ulong _pad5, _pad6, _pad7, _pad8;
        private long _pad9, _pad10, _pad11, _pad12, _pad13, _pad14;
        private long _pad15, _pad16, _pad17;
    }
}
