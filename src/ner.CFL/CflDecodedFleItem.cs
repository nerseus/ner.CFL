namespace ner.CFL
{
    /// <summary>
    /// Represents a decoded CFL File item. Contains info from the CFL to help locate and decompress the actual file bytes.
    /// </summary>
    public class CflDecodedFileItem
    {
        public string Name { get; set; }
        public uint Offset { get; set; }
        public uint Compression { get; set; }
        public uint UncompressedSize { get; set; }
        public string ContentHash { get; set; }
    }
}
