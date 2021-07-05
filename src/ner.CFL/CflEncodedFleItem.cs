using System.Collections.Generic;

namespace ner.CFL
{
    /// <summary>
    /// Represents an encoded CFL File item. Contains some info that will be written to the file list info as well as the compressed data.
    /// </summary>
    public class CflEncodedFileItem : CflFileItem
    {
        public uint Offset { get; set; }
        public uint Compression { get; set; }
        public uint UncompressedSize { get; set; }
        public byte[] CompressedFileData { get; set; }
    }
}
