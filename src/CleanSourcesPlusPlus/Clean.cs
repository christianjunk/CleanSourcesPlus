namespace CleanSourcesPlusPlus
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Security;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.VisualBasic;
    using System.Configuration;
    using System.Collections.Specialized;
    using System.Text.RegularExpressions;
    using ICSharpCode.SharpZipLib.Zip;

    /// <summary>

    /// '''

    /// ''' This is a console application to clean .net projects by removing the following:

    /// '''

    /// '''   * bin, obj and setup directories 

    /// '''   * all source bindings

    /// '''   * any user settings

    /// '''

    /// ''' This is useful when sharing projects with other developers.

    /// '''

    /// ''' Code based on Omar Shahine's "Clean Sources"

    /// '''    http://wiki.shahine.com/default.aspx/MyWiki/CleanSources.html

    /// '''

    /// ''' Jeff Atwood

    /// ''' http://www.codinghorror.com/blog/

    /// ''' 

    /// ''' </summary>
    public static class Clean
    {
        private static bool _HasError = false;

        private static Regex _DirectoryDeletionRegex = new Regex(ConfigurationManager.AppSettings.Get("DirectoryDeletionPattern"), RegexOptions.IgnoreCase);
        private static Regex _FileDeletionRegex = new Regex(ConfigurationManager.AppSettings.Get("FileDeletionPattern"), RegexOptions.IgnoreCase);
        private static Regex _FileBindingRegex = new Regex(ConfigurationManager.AppSettings.Get("FileBindingPattern"), RegexOptions.IgnoreCase);

        public static void Main(string[] args)
        {
            if ((args.Length == 0))
            {
                Console.WriteLine("No solution folder path was provided. Please provide a folder path as the first parameter to this executable.");
                return;
            }

            string path = args[0];
            if (!Directory.Exists(path))
            {
                Console.WriteLine("Solution folder path '" + path + " doesn't exist.");
                return;
            }

            CleanSolutionFolder(path);

            if (args.Length > 1)
            {
                if (args[1].ToLower() == "-zip")
                    ZipDirectory(path);
            }

            if (_HasError)
            {
                Console.WriteLine("Press ENTER to continue...");
                Console.ReadLine();
            }
        }

        /// <summary>
        ///     ''' Creates a ZIP file containing the contents of the folder
        ///     ''' </summary>
        public static void ZipDirectory(string directoryPath)
        {

            FileAttributes attr = File.GetAttributes(directoryPath);
            if ((attr & FileAttributes.Directory) != FileAttributes.Directory)
                return; 

            DirectoryInfo di = new DirectoryInfo(directoryPath);
            
            if (!directoryPath.EndsWith(@"\"))
                directoryPath += @"\";

            string ParentDirectory = di.Parent.FullName + @"\";

            string ZipFilePath = Path.Combine(ParentDirectory, Path.GetFileName(Path.GetDirectoryName(directoryPath)) + ".zip");
            if (File.Exists(ZipFilePath))
                DeletePath(ZipFilePath);

            
            FileStream s = null;
            byte[] buf;
            ZipOutputStream zs = null/* TODO Change to default(_) if this is not a reference type */;
            ZipEntry ze;

            try
            {
                StringCollection fileList = GenerateFileList(directoryPath);

                zs = new ZipOutputStream(File.Create(ZipFilePath));
                zs.SetLevel(9);

                foreach (string CurrentPath in fileList)
                {
                    ze = new ZipEntry(CurrentPath.Replace(ParentDirectory, ""));
                    zs.PutNextEntry(ze);
                    if (!CurrentPath.EndsWith("/"))
                    {
                        s = File.OpenRead(CurrentPath);
                        buf = new byte[Convert.ToInt32(s.Length) - 1 + 1];
                        s.Read(buf, 0, buf.Length);
                        zs.Write(buf, 0, buf.Length);
                        s.Close();
                        Console.Write(".");
                    }
                }
            }
            catch (Exception ex)
            {
                _HasError = true;
                DumpException(ex, "create zip file", ZipFilePath);
            }
            finally
            {
                if (s != null)
                    s.Close();
                if (zs != null)
                {
                    zs.Finish();
                    zs.Close();
                }
            }
        }

        /// <summary>
        ///     ''' Builds an string collection containing all the files under a specific path
        ///     ''' </summary>
        private static StringCollection GenerateFileList(string path)
        {
            StringCollection fileList = new StringCollection();
            bool isEmpty = true;

            foreach (string file in Directory.GetFiles(path))
            {
                fileList.Add(file);
                isEmpty = false;
            }
            if (isEmpty)
            {
                if (Directory.GetDirectories(path).Length == 0)
                    fileList.Add(path + "/");
            }

            foreach (string dir in Directory.GetDirectories(path))
            {
                DirectoryInfo di = new DirectoryInfo(dir);
                if (di.Name.Equals("packages") || di.Name.Equals(".vs"))
                {
                    continue;
                }
                foreach (string s in GenerateFileList(dir))
                    fileList.Add(s);
            }

            return fileList;
        }

        private static void CleanSolutionFolder(string solutionFolderPath)
        {
            StringCollection PathCollection = null;
            // -- build a collection of paths to delete
            CleanDirectory(solutionFolderPath, ref PathCollection);
            // -- now delete them
            DeletePaths(PathCollection);
        }

        /// <summary>
        ///     ''' Recursively builds a collection of paths to be deleted. 
        ///     ''' Also remove source control bindings while we're at it.
        ///     ''' </summary>
        private static void CleanDirectory(string path, ref StringCollection pathCollection)
        {
            DirectoryInfo tdi = new DirectoryInfo(path);

            if (pathCollection == null)
                pathCollection = new StringCollection();

            foreach (FileInfo fi in tdi.GetFiles())
            {
                if (_FileDeletionRegex.IsMatch(fi.Name))
                    pathCollection.Add(fi.FullName);
                if (_FileBindingRegex.IsMatch(fi.Name))
                    RemoveSourceBindings(fi.FullName);
            }

            foreach (DirectoryInfo di in tdi.GetDirectories())
            {
                if (_DirectoryDeletionRegex.IsMatch(di.Name))
                    pathCollection.Add(di.FullName + @"\");
                else
                    CleanDirectory(di.FullName, ref pathCollection);
            }
        }

        /// <summary>
        ///     ''' Returns true if the provided path is a directory
        ///     ''' </summary>
        private static bool IsDirectory(string path)
        {
            if (path.EndsWith(@"\"))
                return true;
            if (File.Exists(path) || Directory.Exists(path))
                return ((File.GetAttributes(path) & FileAttributes.Directory) == FileAttributes.Directory);
            else
                return false;
        }

        /// <summary>
        ///     ''' Delete the provided collection of files/directories
        ///     ''' </summary>
        private static void DeletePaths(StringCollection pathCollection)
        {
            foreach (string path in pathCollection)
                DeletePath(path);
        }

        /// <summary>
        ///     ''' Returns true if a file is marked Read-Only
        ///     ''' </summary>
        private static bool GetFileReadOnly(string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            return (fi.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly;
        }

        /// <summary>
        ///     ''' Toggles the Read-Only attribute on a file
        ///     ''' </summary>
        private static void SetFileReadOnly(string filePath, bool isReadOnly)
        {
            FileInfo fi = new FileInfo(filePath);
            if (!isReadOnly)
                fi.Attributes = fi.Attributes & FileAttributes.Normal;
            else
                fi.Attributes = fi.Attributes & FileAttributes.ReadOnly;
        }

        /// <summary>
        ///     ''' Deletes a file or directory, with exception trapping
        ///     ''' </summary>
        private static void DeletePath(string path)
        {
            try
            {
                if (IsDirectory(path))
                    Directory.Delete(path, true);
                else
                {
                    if (GetFileReadOnly(path))
                        SetFileReadOnly(path, false);
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                DumpException(ex, "delete file or path", path);
            }
        }

        /// <summary>
        ///     ''' Dumps an exception to the console
        ///     ''' </summary>
        private static void DumpException(Exception ex, string taskDescription, string path)
        {
            Console.WriteLine("Failed to " + taskDescription);
            Console.WriteLine("  " + path);
            Console.WriteLine("Exception message:");
            Console.WriteLine(ex.ToString());
            _HasError = true;
        }

        /// <summary>
        ///     ''' Removes source control bindings, if present, from the text file. 
        ///     ''' This is hard-coded to work only against VS.NET solution files.
        ///     ''' </summary>
        private static void RemoveSourceBindings(string path)
        {
            string oldFileContents = ReadFile(path);
            string newFileContents = oldFileContents;

            // -- remove any GlobalSection(SourceCodeControl) block
            newFileContents = Regex.Replace(newFileContents, @"\s+GlobalSection\(SourceCodeControl\)[\w|\W]+?EndGlobalSection", "");
            // -- remove any remaining lines that have keys beginning with 'Scc'
            newFileContents = Regex.Replace(newFileContents, @"^\s+Scc.*[\n\r\f]", "", RegexOptions.Multiline);

            if (newFileContents != oldFileContents)
                WriteFile(path, newFileContents);
        }

        /// <summary>
        ///     ''' Reads a text file from disk
        ///     ''' </summary>
        private static string ReadFile(string path)
        {
            StreamReader reader = null;
            string content = "";
            try
            {
                reader = File.OpenText(path);
                content = reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                DumpException(ex, "read contents of file", path);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
            return content;
        }

        /// <summary>
        ///     ''' Writes a text file to disk
        ///     ''' </summary>
        private static void WriteFile(string path, string fileContents)
        {
            SetFileReadOnly(path, false);
            StreamWriter writer = null;
            try
            {
                writer = new StreamWriter(path, false);
                writer.Write(fileContents);
            }
            catch (Exception ex)
            {
                DumpException(ex, "write contents to file", path);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }
    }

}