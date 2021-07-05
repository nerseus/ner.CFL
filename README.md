# ner.CFL
Library for compressing and decompressing CFL files. The format of these CFLs conforms to what is expected by IMVU.

## Usage
Usage of the library is through two static classes: CflCompressor and CflDecompressor.

Each class supports 2 public methods. Note the "string" versions are just helpers if loading files from disk.
```
CflCompressor.CompressFiles(string path)
CflCompressor.CompressFiles(List<CflFileItem> cflFiles)
```
and 
```
CflDecompressor.Decompress(string inputFile)
CflDecompressor.Decompress(byte[] fileData)
```

## Dependencies
The class library ner.CFL relies on package SevenZip for performing the LZMA compression.

## Testing
Included in the solution is a console project ner.CFL.ConsoleTest.
This project contains a folder called "TestFiles" with 5 sample files. Each file is marked with "Copy to output directory" = "Copy always" in Visual Studio.
This means the folder and the files will be copied to ...\bin\Debug\ when running/testing within Visual Studio.
This may not work in other IDEs (such as Visual Studio Code - untested).

After loading the solution, set the ConsoleTest project as Startup and run.
Function Main:
1. Calls CflCompressor.SaveFiles to save all files in a given inputFolder.
This creates the cfl named by outputFile.

2. Calls CflDecompressor.GetFile to load the CFL.
The CFLFile contains a list of FileItem, each with a name and byte[].

When the app finishes, it will open an explorer window to where the files were extracted.
NOTE: if you go up one folder you'll see the "TestFiles" folder that was compressed into "testOutput.cfl".
Then "testOutput.cfl" is decompressed into the "extracted" folder.

## Limitations on DFL3 format
While the CFL format supports CFL3 and DFL3 files, this library will only compress CFL3 files.
This libary will read DFL3 formatted files. These include a CRC hash, per file. This library will read the CRC hash as a byte[] but does not use it for comparison/validation of the decompressed files.
