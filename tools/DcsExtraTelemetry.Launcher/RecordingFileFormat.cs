using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace DcsExtraTelemetry.Launcher
{
    internal static class RecordingFileFormat
    {
        internal const string CompressedExtension = ".jsonl.gz";

        internal static IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo folder)
        {
            if (folder == null || !folder.Exists) yield break;
            foreach (FileInfo file in folder.GetFiles("*.jsonl")) yield return file;
            foreach (FileInfo file in folder.GetFiles("*.jsonl.gz")) yield return file;
        }

        internal static StreamReader OpenText(string path)
        {
            FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            try
            {
                if (path.EndsWith(CompressedExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return new StreamReader(new GZipStream(stream, CompressionMode.Decompress), Encoding.UTF8, true);
                }
                return new StreamReader(stream, Encoding.UTF8, true);
            }
            catch
            {
                stream.Dispose();
                throw;
            }
        }
    }
}
