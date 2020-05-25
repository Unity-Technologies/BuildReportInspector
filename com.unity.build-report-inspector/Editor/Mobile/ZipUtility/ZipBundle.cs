using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;

namespace Unity.BuildReportInspector.Mobile.ZipUtility
{
    internal enum Markers
    {
        EntryCount = 10,
        CentralDirectoryRecord = 16,
        CompressedSize = 20,
        UncompressedSize = 24,
        NameLength = 28,
        ExtraLength = 30,
        CommentLength = 32,
        Name = 46
    }

    internal class ZipBundle : IDisposable
    {
        public string FullName { get; }
        public long Length { get; }
        public List<ZipEntry> Entries { get; }

        private const int k_EndOfCentralDirectoryMarker = 101010256;
        private const int k_MaxEndOfCentralDirectoryOffset = 65557;
        private readonly FileStream m_ZipStream;
        private long m_EndOfCentralDirectoryOffset;
        private long EndOfCentralDirectoryOffset
        {
            get
            {
                if (m_EndOfCentralDirectoryOffset == 0)
                    m_EndOfCentralDirectoryOffset = FindEndOfCentralDirectoryOffset();
                return m_EndOfCentralDirectoryOffset;
            }
        }

        private long FindEndOfCentralDirectoryOffset()
        {
            var marker = BitConverter.GetBytes(k_EndOfCentralDirectoryMarker);
            for (var offset = 4; offset <= k_MaxEndOfCentralDirectoryOffset; offset++)
            {
                m_ZipStream.Seek(-offset, SeekOrigin.End);
                var sizeBytes = new byte[4];
                m_ZipStream.Read(sizeBytes, 0, 4);
                if (!sizeBytes.SequenceEqual(marker))
                    continue;
                m_ZipStream.Seek(-4, SeekOrigin.Current);
                return m_ZipStream.Position;
            }

            throw new Exception("Archive invalid - could not find end-of-central-directory marker.");
        }

        private int GetArchiveEntryCount()
        {
            return ReadUShort(EndOfCentralDirectoryOffset + (int)Markers.EntryCount);
        }

        private long GetCentralDirectoryOffset()
        {
            return ReadUInt(EndOfCentralDirectoryOffset + (int)Markers.CentralDirectoryRecord);
        }

        private byte[] GetBytes(long position, int length)
        {
            m_ZipStream.Seek(position, SeekOrigin.Begin);
            var resultBytes = new byte[length];
            m_ZipStream.Read(resultBytes, 0, length);
            return resultBytes;
        }

        private ushort ReadUShort(long position)
        {
            return BitConverter.ToUInt16(GetBytes(position, 2), 0);
        }

        private uint ReadUInt(long position)
        {
            return BitConverter.ToUInt32(GetBytes(position, 4), 0);
        }

        private string ReadString(long position, int length)
        {
            return System.Text.Encoding.Default.GetString(GetBytes(position, length));
        }

        private void ValidateZip()
        {
            FindEndOfCentralDirectoryOffset();
        }

        public ZipBundle(string path)
        {
            m_ZipStream = File.Open(path, FileMode.Open);

            ValidateZip();

            FullName = path;
            Length = new FileInfo(path).Length;

            var entryCount = GetArchiveEntryCount();
            var recordStart = GetCentralDirectoryOffset();
            Entries = new List<ZipEntry>();
            for (var _ = 0; _ < entryCount; _++)
            {
                var compressedSize = ReadUInt(recordStart + (int)Markers.CompressedSize);
                var uncompressedSize = ReadUInt(recordStart + (int)Markers.UncompressedSize);
                var nameLength = ReadUShort(recordStart + (int)Markers.NameLength);
                var extraLength = ReadUShort(recordStart + (int)Markers.ExtraLength);
                var commentLength = ReadUShort(recordStart + (int)Markers.CommentLength);
                var name = ReadString(recordStart + (int)Markers.Name, nameLength);
                Entries.Add(new ZipEntry(name, compressedSize, uncompressedSize));
                recordStart = recordStart + (int) Markers.Name + nameLength + extraLength + commentLength;
            }
        }

        public override string ToString()
        {
            return FullName;
        }

        public void Dispose()
        {
            m_ZipStream.Close();
        }
    }
}
