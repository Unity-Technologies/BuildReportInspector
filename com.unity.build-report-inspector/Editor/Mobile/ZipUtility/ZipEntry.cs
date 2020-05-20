using System.IO;

namespace Unity.BuildReportInspector.Mobile.ZipUtility
{
    public class ZipEntry
    {
        public string Name { get; }
        public string FullName { get; }
        public uint CompressedSize { get; }
        public uint UncompressedSize { get; }

        internal ZipEntry(string fullName, uint compressedSize, uint uncompressedSize)
        {
            Name = Path.GetFileName(fullName);
            FullName = fullName;
            CompressedSize = compressedSize;
            UncompressedSize = uncompressedSize;
        }
        
        public override string ToString()
        {
            return FullName;
        }
    }
}
