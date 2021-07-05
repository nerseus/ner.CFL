using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ner.CFL
{
    /// <summary>
    /// A utility class for compressing a folder of files into a CFL file.
    /// </summary>
    public static class CflCompresser
    {
        /// <summary>
        /// Compresses the buffer into a new byte[]. This uses LZMA compression.
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        private static byte[] Compress(byte[] buffer)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (BinaryWriter writer = new BinaryWriter(ms))
                {
                    Int32 dictionary = 1 << 16;
                    Int32 numFastBytes = 64;
                    Int32 litPosBits = 0;
                    Int32 litContextBits = 3;
                    Int32 posStateBits = 2;
                    Int32 algorithm = 2;

                    SevenZip.CoderPropID[] propIDs =
                    {
                        SevenZip.CoderPropID.DictionarySize,
                        SevenZip.CoderPropID.PosStateBits,
                        SevenZip.CoderPropID.LitContextBits,
                        SevenZip.CoderPropID.LitPosBits,
                        SevenZip.CoderPropID.Algorithm,
                        SevenZip.CoderPropID.NumFastBytes,
                        SevenZip.CoderPropID.MatchFinder,
                        SevenZip.CoderPropID.EndMarker
                    };

                    object[] properties =
                    {
                        (Int32)(dictionary),
                        (Int32)(posStateBits),
                        (Int32)(litContextBits),
                        (Int32)(litPosBits),
                        (Int32)(algorithm),
                        (Int32)(numFastBytes),
                        "bt4",
                        false
                    };

                    SevenZip.Compression.LZMA.Encoder encoder = new SevenZip.Compression.LZMA.Encoder();
                    encoder.SetCoderProperties(propIDs, properties);
                    encoder.WriteCoderProperties(ms);

                    using (MemoryStream input = new MemoryStream(buffer))
                    {
                        encoder.Code(input, ms, -1, -1, null);
                    }

                    return ms.ToArray();
                }
            }
        }

        /// <summary>
        /// Extension method to convert a string to a byte[].
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        private static byte[] ToBytes(this string s)
        {
            return Encoding.ASCII.GetBytes(s);
        }

        /// <summary>
        /// Extension method to write a full byte array to a MemoryStream.
        /// </summary>
        /// <param name="memoryStream"></param>
        /// <param name="bytes"></param>
        private static void Write(this MemoryStream memoryStream, byte[] bytes)
        {
            memoryStream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Extension method to write an uint to a MemoryStream.
        /// </summary>
        /// <param name="memoryStream"></param>
        /// <param name="value"></param>
        private static void Write(this MemoryStream memoryStream, uint value)
        {
            var bytes = BitConverter.GetBytes(value);
            memoryStream.Write(bytes);
        }

        /// <summary>
        /// Extension method to write an Uint16 to a MemoryStream.
        /// </summary>
        /// <param name="memoryStream"></param>
        /// <param name="value"></param>
        private static void Write(this MemoryStream memoryStream, UInt16 value)
        {
            var bytes = BitConverter.GetBytes(value);
            memoryStream.Write(bytes);
        }

        /// <summary>
        /// Creates a byte[] of the "file list" info. This will later be compressed and written at the end of the cfl.
        /// </summary>
        /// <param name="files"></param>
        /// <returns></returns>
        private static byte[] CreateFileList(List<CflEncodedFileItem> files)
        {
            // Each file will have an Offset of where the file's compressed data is stored
            // within the cfl.
            // Start by calculating the size of the "header" fields.
            uint offset =
                4    // 4 bytes for "CFL3"
                + 4  // 4 bytes for uint headerIndexPos itself
                + 4; // 4 bytes for uint uncompressed size

            // Create the "file list" that goes at the end of the file.
            using (var memoryStream = new MemoryStream())
            {
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    file.Offset = offset;

                    // [0..3] = uint of the uncompressed file size.
                    memoryStream.Write((uint)file.FileData.Length);
                    // [4..7] = uint of the offset in the file where the compressed data for this file begins.
                    memoryStream.Write(file.Offset);
                    // [8..11] = uint of the compression type (always 4).
                    memoryStream.Write((uint)4);
                    // [12..13] = the UInt16 length of the file name.
                    memoryStream.Write((UInt16)file.Name.Length);
                    // [14...] = the string filename (length is set above).
                    memoryStream.Write(file.Name.ToBytes());

                    // Each file will consist of 4 bytes for the length of the compressed data
                    // Plus the compressed data.
                    // Calculate the next offset accordingly.
                    offset += 4; 
                    offset += (uint)file.CompressedFileData.Length;
                }

                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Compress the data in cflFiles.
        /// NOTE: Only the Name, FileData, and CompressedFileData are set on each CflEncodedFileItem.
        /// The Offset, compression, etc. will be set later, separately.
        /// </summary>
        /// <param name="cflFiles"></param>
        /// <returns></returns>
        private static List<CflEncodedFileItem> CompressFileData(List<CflFileItem> cflFiles)
        {
            var files = new List<CflEncodedFileItem>();
            foreach (var cflFileItem in cflFiles)
            {
                var file = new CflEncodedFileItem
                {
                    Name = cflFileItem.Name,
                    FileData = cflFileItem.FileData
                };

                file.CompressedFileData = Compress(file.FileData);
                files.Add(file);
            }

            return files;
        }

        /// <summary>
        /// Helper method to get a list of all files from the given path and then call CompressFiles with the List<CflFileItem>.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static byte[] CompressFiles(string path)
        {
            var filenames = Directory.GetFiles(path);
            var cflFiles = new List<CflFileItem>();
            foreach (var filename in filenames)
            {
                byte[] fileData = File.ReadAllBytes(filename);
                var cflFileItem = new CflFileItem
                {
                    Name = Path.GetFileName(filename),
                    FileData = fileData
                };

                cflFiles.Add(cflFileItem);
            }

            return CompressFiles(cflFiles);
        }

        /// <summary>
        /// Compresses the list of files given by cflFiles.
        /// </summary>
        /// <param name="cflFiles"></param>
        /// <returns></returns>
        public static byte[] CompressFiles(List<CflFileItem> cflFiles)
        {
            if (cflFiles == null || cflFiles.Count == 0)
            {
                return null;
            }

            if (cflFiles.Any(x => string.IsNullOrEmpty(x.Name) || x.FileData == null || x.FileData.Length == 0))
            {
                throw new ArgumentException("Each file in cflFiles should have a Name and FileData.");
            }

            // Compress all of the files. This stories the compressed data in CompressedfileData byte[].
            var files = CompressFileData(cflFiles);

            // Total up the compressed files.
            // Add 4 since the cfl format will first write out the
            // 4 byte "size" of the compressed data (the array.Length).
            var compressedDataSize = (uint)files.Sum(x => x.CompressedFileData.Length + 4);

            // Calculate the file list info position.
            // This is where the "file list" is stored, near the end of the file.
            // This will contain the list of files and their names and some meta data used to extract.
            uint fileListInfoPosition =
                4                       // 4 bytes for "CFL3"
                + 4                     // 4 bytes for uint fileListInfoPosition itself
                + 4                     // 4 bytes for uint uncompressed size
                + compressedDataSize;   // the length of the compressed files, end-to-end

            // The file list is created, then compressed as it will be stored as compressed data in the CFL.
            var fileList = CreateFileList(files);
            var compressedFileList = Compress(fileList);

            using (var memoryStream = new MemoryStream())
            {
                // The first 4 bytes should be "CFL3".
                // This can be "DFL3" if each file is storing a CRC value. This is not implemented
                // although the correspdonding CflDecompress does handle DFL3 files.
                memoryStream.Write("CFL3".ToBytes());

                // 4 bytes for the position of the "file list info".
                memoryStream.Write(fileListInfoPosition);

                // 4 bytes for the size of the Uncompressed file list info.
                memoryStream.Write((uint)fileList.Length);

                // Write out each file's compressed data, one after another.
                for (int i = 0; i < files.Count; i++)
                {
                    var file = files[i];
                    // First write the 4 byte "size" of the compressed data.
                    // The uncompressed size is stored within each file in the "file list info" section.
                    memoryStream.Write((uint)file.CompressedFileData.Length);

                    // Write out the compressed data.
                    memoryStream.Write(file.CompressedFileData);
                }

                // NOTE: The following is where "fileListInfoPosition" should be pointing.

                // Compression type - always 4.
                memoryStream.Write((uint)4);

                // Write out the size of the compressed file list data.
                memoryStream.Write((uint)compressedFileList.Length);

                // Write out the "file list" at the end of the file.
                memoryStream.Write(compressedFileList);

                return memoryStream.ToArray();
            }
        }
    }
}
