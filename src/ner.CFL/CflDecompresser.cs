using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ner.CFL
{
    /// <summary>
    /// A utility class for decompressing a CFL file into a list of files.
    /// NOTE: Very little is done to check for errors. Invalid compression methods may fail modestly or spectacularly. ymmv.
    /// </summary>
    public static class CflDecompresser
    {
        private static readonly uint MaxVal = (uint)Math.Pow(2, 32) - 1;

        /// <summary>
        /// Get the list of cfl files contained in the fileListData buffer. This returns everything *except* the FileData. That is returned separately afer the list is returned.
        /// </summary>
        /// <param name="fileListData"></param>
        /// <param name="isDfl"></param>
        /// <returns></returns>
        private static List<CflDecodedFileItem> GetCflFiles(byte[] fileListData, bool isDfl)
        {
            var fileList = new List<CflDecodedFileItem>();

            int currentPos = 0;
            while (currentPos < fileListData.Length)
            {
                var fileItem = new CflDecodedFileItem();
                fileItem.UncompressedSize = BitConverter.ToUInt32(fileListData, currentPos);
                currentPos += 4;
                fileItem.Offset = BitConverter.ToUInt32(fileListData, currentPos);
                currentPos += 4;
                fileItem.Compression = BitConverter.ToUInt32(fileListData, currentPos);
                currentPos += 4;
                var nameLength = BitConverter.ToUInt16(fileListData, currentPos);
                currentPos += 2;

                var nameArray = new byte[nameLength];
                Array.Copy(fileListData, currentPos, nameArray, 0, nameLength);
                fileItem.Name = Encoding.UTF8.GetString(nameArray, 0, nameLength);
                currentPos += nameLength;

                // If the file is type DFL3 then there is also a CRC hash stored with each file.
                // The code below will read the hash but does NOT recompute/check the hash against each file.
                // If anyone wants this, it would require importing nuget packages Crc32.NET and Security.Cryptography.
                if (isDfl)
                {
                    var hashLength = BitConverter.ToInt32(fileListData, currentPos);
                    currentPos += 4;

                    var hashArray = new byte[hashLength];
                    Array.Copy(fileListData, currentPos, hashArray, 0, hashLength);
                    currentPos += hashLength;

                    fileItem.ContentHash = Encoding.UTF8.GetString(hashArray, 0, hashLength);
                }

                fileList.Add(fileItem);
            };

            return fileList;
        }

        /// <summary>
        /// Decompress the data from cflHeaderBuffer. This will use LzmaDecompress to do the actual decoding.
        /// The data in compressedData needs to be updated before lzma can decompress. Not sure why?
        /// The compressedData buffer becomes newBuffer:
        ///     // The first 5 bytes of compressedData are copied over.
        ///     newBuffer[0] = compressedData[0]    
        ///     newBuffer[1] = compressedData[1]
        ///     newBuffer[2] = compressedData[2]
        ///     newBuffer[3] = compressedData[3]
        ///     newBuffer[4] = compressedData[4]
        ///     // 2 more uint values are inserted:
        ///     newBuffer[5..8] = uint value of uncompressed size, rounded to nearest MaxVal.
        ///     newBuffer[9..12] = uint value of uncompressed size "remainder".
        ///     // The rest of compressedData is appended to newBuffer.
        ///     newBuffer[13...] = compressedData[5...]
        ///     
        /// </summary>
        /// <param name="compressionType">The compression type. Must be 4.</param>
        /// <param name="uncompressedSize">The size of the uncompressed data, used to pre-allocate the array.</param>
        /// <param name="compressedData">The byte[] of compressed data.</param>
        /// <returns></returns>
        private static byte[] CflDecompress(uint compressionType, uint uncompressedSize, byte[] compressedData)
        {
            if (compressionType != 4)
            {
                throw (new Exception("Wrong compression type in cfl"));
            }

            // Repack the array to get ready for lzma.
            // NOTE: the first byte is a bit-packed value for the LC, LP, and PB.
            // The next 4 bytes are the "dictionary size".
            // This is not important for Decompressing, but is used by the LZMA spec.
            var newBuffer = new byte[compressedData.Length + 8];
            Array.Copy(compressedData, 0, newBuffer, 0, 5);

            // Compute the uncompressed size in an 8-byte format and "inject" into the array.
            // This is what LZMA needs.
            var uncompressedSizeAdjusted = BitConverter.GetBytes(uncompressedSize & MaxVal);
            var uncompressedSizeRemainder = BitConverter.GetBytes(uncompressedSize - (uncompressedSize & MaxVal));
            Array.Copy(uncompressedSizeAdjusted, 0, newBuffer, 5, 4);
            Array.Copy(uncompressedSizeRemainder, 0, newBuffer, 9, 4);

            // Copy over the rest of the array from compressedData.
            Array.Copy(compressedData, 5, newBuffer, 13, compressedData.Length - 5);

            var uncompressedData = LzmaDecompress(newBuffer);
            return uncompressedData;
        }

        /// <summary>
        /// Decompress the inputBytes using the LZMA decoder. The decoder properties are part of the inputBytes.
        /// </summary>
        /// <param name="inputBytes"></param>
        /// <returns></returns>
        private static byte[] LzmaDecompress(byte[] inputBytes)
        {
            using (MemoryStream inStream = new MemoryStream(inputBytes))
            {
                SevenZip.Compression.LZMA.Decoder decoder = new SevenZip.Compression.LZMA.Decoder();

                inStream.Seek(0, 0);
                using (MemoryStream outStream = new MemoryStream())
                {
                    byte[] decoderProperties = new byte[5];
                    if (inStream.Read(decoderProperties, 0, 5) != 5)
                    {
                        throw (new Exception("Input .lzma is too short"));
                    }

                    // Calculate the uncompressed size. As noted in CflDecompress, this is
                    // uncompressedSizeAdjusted and uncompressedSizeRemainder.
                    long outSize = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        int v = inStream.ReadByte();
                        if (v < 0)
                        {
                            throw (new Exception("Can't Read 1"));
                        }
                        outSize |= ((long)(byte)v) << (8 * i);
                    }

                    // The decoder properties are the first 5 bytes of inputBytes.
                    // The first byte is a bit-packed value for the LC, LP, and PB.
                    // The next 4 bytes are the "dictionary size".
                    // This is not important for Decompressing, but is used by the LZMA spec.
                    decoder.SetDecoderProperties(decoderProperties);

                    long compressedSize = inStream.Length - inStream.Position;
                    decoder.Code(inStream, outStream, compressedSize, outSize, null);

                    byte[] uncompressedData = outStream.ToArray();

                    return uncompressedData;
                }
            }
        }

        /// <summary>
        /// Reads data from the inputFile and creates the CFL object.
        /// </summary>
        /// <param name="inputFile">The fully path/filename to the CFL file to read and decompress.</param>
        /// <returns></returns>
        public static List<CflFileItem> Decompress(string inputFile)
        {
            byte[] fileData = File.ReadAllBytes(inputFile);

            return Decompress(fileData);
        }

        /// <summary>
        /// Reads the CFL from the fileData bytes.
        /// The filename is only used to set the Filename property on the returned CFL and does NOT have to be the original filename.
        /// </summary>
        /// <param name="fileData"></param>
        /// <returns></returns>
        public static List<CflFileItem> Decompress(byte[] fileData)
        {
            // Verify the "header" of the file is a valid CFL.
            var first4 = Encoding.UTF8.GetString(fileData, 0, 4);
            if (first4 != "CFL3" && first4 != "DFL3")
            {
                throw new Exception("File doesn't start with CFL3 (or DFL3) as expected.");
            }

            var headerIndexPosition = BitConverter.ToUInt32(fileData, 4);
            var uncompressedSize = BitConverter.ToUInt32(fileData, 8);
            var compressionType = BitConverter.ToUInt32(fileData, (int)headerIndexPosition);
            var headerSize = BitConverter.ToUInt32(fileData, (int)headerIndexPosition + 4);

            byte[] cflHeaderBuffer = new byte[headerSize];
            Array.Copy(fileData, headerIndexPosition + 8, cflHeaderBuffer, 0, headerSize);

            var fileListData = CflDecompress(compressionType, uncompressedSize, cflHeaderBuffer);

            var isDfl = (first4 == "DFL3");
            var fileList = GetCflFiles(fileListData, isDfl);

            var files = new List<CflFileItem>();
            foreach (var fileItem in fileList)
            {
                var size = BitConverter.ToUInt32(fileData, (int)fileItem.Offset);
                var compressedFileData = new byte[size];
                Array.Copy(fileData, fileItem.Offset + 4, compressedFileData, 0, size);
                var uncompressedFileData = CflDecompress(fileItem.Compression, fileItem.UncompressedSize, compressedFileData);
                files.Add(new CflFileItem { Name = fileItem.Name, FileData = uncompressedFileData });
            }

            return files;
        }
    }
}
