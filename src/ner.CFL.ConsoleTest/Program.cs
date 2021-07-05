using System;
using System.Diagnostics;
using System.IO;

namespace ner.CFL.ConsoleTest
{
    /// <summary>
    /// Sample app that will test the ner.CFL library for Compressing and Decompressing.
    /// This project should contain a folder named "TestFiles"
    /// All files contained in that folder shoulder be marked with "Copy to output directory" = "Copy always"
    /// This ensures that the test files can be located using Directory.GetCurrentDirectory.
    /// Alternatively, change the 3 strings at the top of main: inputFolder, outputFile, extractFolder.
    /// 
    /// Function Main:
    /// 1. Calls CflCompressor.SaveFiles to save all files in a given inputFolder.
    ///    This creates the cfl named by outputFile.
    /// 2. Calls CflDecompressor.GetFile to load the CFL.
    ///    The CFLFile contains a list of FileItem, each with a name and byte[].
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            string inputFolder = Path.Combine(Directory.GetCurrentDirectory(), "TestFiles");
            string outputFile = Path.Combine(Directory.GetCurrentDirectory(), "testOutput.cfl");
            string extractFolder = Path.Combine(Directory.GetCurrentDirectory(), "extracted");

            if (!Directory.Exists(extractFolder))
            {
                Directory.CreateDirectory(extractFolder);
            }

            // The project should have a TestFiles folder with some test files.
            // The following line will add files from the inputFolder into a cfl named by outputFile.
            // This should end up in the ...\bin\Debug\ folder where this project is running (at least, if running in Debug mode).
            var fileData = CflCompressor.CompressFiles(inputFolder);
            File.WriteAllBytes(outputFile, fileData);

            // Now that there's a cfl, the following will read and extract the data into the cflObject.
            // The cflObject will contain a List of FileItems with the Name and FileData byte[].
            var files = CflDecompressor.Decompress(outputFile);
            foreach (var cflFileItem in files)
            {
                var newFilename = Path.Combine(extractFolder, cflFileItem.Name);
                File.WriteAllBytes(newFilename, cflFileItem.FileData);
            }

            // Open an explorer window to the output folder
            Process.Start("explorer.exe", extractFolder);

            Console.WriteLine($"All finished. Extraced {files.Count} files.");
            Console.WriteLine("Press any key to exit.");
            Console.ReadKey();
        }
    }
}
