using System;
using System.IO;
using System.Linq;
using System.Threading;
using ImageMagick;

class Program
{
    static void Main()
    {
        // Get the directory of the executable
        string executablePath = AppDomain.CurrentDomain.BaseDirectory;

        string toConvertFolderPath = Path.Combine(executablePath, "ToConvert");
        string convertedFolderPath = Path.Combine(executablePath, "Converted");

        if (!Directory.Exists(toConvertFolderPath))
        {
            Directory.CreateDirectory(toConvertFolderPath);
            Console.WriteLine($"'{toConvertFolderPath}' folder does not exist, I have just created it for you! \nPlace your images in this folder and run the program again.");
            return;
        }

        // Continue with the conversion process
        int fileCount = GetFileCount(toConvertFolderPath);
        Console.WriteLine($"Contents of folder ({fileCount} files):");

        string[] imageExtensions = { ".tif", ".tiff", ".jpg", ".jpeg", ".png",
                                     ".3fr", ".ari", ".arw", ".bay", ".crw", ".cr2", ".cap",
                                     ".dcs", ".dcr", ".dng", ".drf", ".eip", ".erf", ".fff",
                                     ".gpr", ".iiq", ".k25", ".kdc", ".mdc", ".mef", ".mos",
                                     ".mrw", ".nef", ".nrw", ".obm", ".orf", ".pef", ".ptx",
                                     ".pxn", ".r3d", ".raf", ".raw", ".rwl", ".rw2", ".rwz",
                                     ".sr2", ".srf", ".srw", ".x3f" };
        string[] imageFiles = GetFilesWithExtensions(toConvertFolderPath, imageExtensions);

        Stampa(imageFiles);

        if (imageFiles.Length > 0)
        {
            // Create timestamped subfolder
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string sessionFolderPath = Path.Combine(convertedFolderPath, timestamp);
            Directory.CreateDirectory(sessionFolderPath);

            int totalFiles = imageFiles.Length;
            int convertedFiles = 0;
            object progressLock = new object();

            Console.WriteLine("Converting...");

            Thread[] threads = new Thread[imageFiles.Length];
            for (int i = 0; i < imageFiles.Length; i++)
            {
                string imageFile = imageFiles[i];
                threads[i] = new Thread(() =>
                {
                    ConvertToJpgOrCopy(imageFile, sessionFolderPath);
                    lock (progressLock)
                    {
                        convertedFiles++;
                        UpdateProgressBar(convertedFiles, totalFiles);
                    }
                });
                threads[i].Start();
            }

            foreach (Thread thread in threads)
            {
                thread.Join();
            }

            Console.WriteLine("\nConversion completed!");
            Console.WriteLine($"The converted images have been placed in the '{sessionFolderPath}' folder.");
        }
        else
        {
            Console.WriteLine($"No image files found in '{toConvertFolderPath}' folder.");
        }
    }

    public static void Stampa(string[] imageFiles)
    {
        foreach (string imageFile in imageFiles)
        {
            string filename = Path.GetFileName(imageFile);
            Console.WriteLine("- " + filename);
        }
    }

    static int GetFileCount(string folderPath)
    {
        return Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories).Length;
    }

    static string[] GetFilesWithExtensions(string folderPath, string[] extensions)
    {
        return Directory.GetFiles(folderPath, "*.*")
                        .Where(file => extensions.Contains(Path.GetExtension(file).ToLower()))
                        .ToArray();
    }

    static void ConvertToJpgOrCopy(string imagePath, string convertedFolderPath, double maxSizeInMB = 7.5, int maxDimension = 7500)
    {
        string outputFileName = Path.Combine(convertedFolderPath, Path.GetFileNameWithoutExtension(imagePath) + ".jpg");

        using var image = new MagickImage(imagePath);
        
        // Check if the original image meets the size and dimension requirements
        FileInfo fileInfo = new FileInfo(imagePath);
        if (fileInfo.Length <= maxSizeInMB * 1024 * 1024 && image.Width <= maxDimension && image.Height <= maxDimension)
        {
            // Copy the file as is
            string copiedFileName = Path.Combine(convertedFolderPath, Path.GetFileName(imagePath));
            File.Copy(imagePath, copiedFileName, true);
            return;
        }

        // Otherwise, process the image
        if (image.Width > maxDimension || image.Height > maxDimension)
        {
            image.Resize(maxDimension, maxDimension);
        }

        int quality = CalculateCompressionQuality(image, maxSizeInMB);

        image.Quality = quality;
        image.Format = MagickFormat.Jpg;
        image.Write(outputFileName);
    }

    static int CalculateCompressionQuality(MagickImage image, double maxSizeInMB)
    {
        const int maxQuality = 100;
        const int minQuality = 1;
        int quality = maxQuality;

        while (true)
        {
            using var compressedImage = new MagickImage(image);
            compressedImage.Quality = quality;
            compressedImage.Format = MagickFormat.Jpg;

            using var stream = new MemoryStream();
            compressedImage.Write(stream);

            if (stream.Length > maxSizeInMB * 1024 * 1024)
            {
                quality -= 10;
                if (quality < minQuality)
                {
                    quality = minQuality;
                    break;
                }
            }
            else
            {
                break;
            }
        }
        return quality;
    }

    static void UpdateProgressBar(int completed, int total)
    {
        int progressWidth = 50;
        int progress = (int)((double)completed / total * progressWidth);

        Console.CursorLeft = 0;
        Console.Write("[");
        Console.CursorLeft = progressWidth + 1;
        Console.Write("]");
        Console.CursorLeft = 1;

        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write("".PadRight(progress, '='));
        Console.ForegroundColor = ConsoleColor.Gray;
    }
}