﻿namespace Merchello.Core.IO
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using System.Web;
    using System.Web.Hosting;

    using Merchello.Core;

    public static class IOHelper
    {
        private static string _rootDir = "";

        // static compiled regex for faster performance
        private static readonly Regex ResolveUrlPattern = new Regex("(=[\"\']?)(\\W?\\~(?:.(?![\"\']?\\s+(?:\\S+)=|[>\"\']))+.)[\"\']?", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.IgnorePatternWhitespace);

        public static char DirSepChar
        {
            get
            {
                return Path.DirectorySeparatorChar;
            }
        }
        
	    //helper to try and match the old path to a new virtual one
        public static string FindFile(string virtualPath)
        {
            string retval = virtualPath;

            if (virtualPath.StartsWith("~"))
                retval = virtualPath.Replace("~", SystemDirectories.Root);

            if (virtualPath.StartsWith("/") && virtualPath.StartsWith(SystemDirectories.Root) == false)
                retval = SystemDirectories.Root + "/" + virtualPath.TrimStart('/');

            return retval;
        }

        //Replaces tildes with the root dir
        public static string ResolveUrl(string virtualPath)
        {
             if (virtualPath.StartsWith("~"))
                return virtualPath.Replace("~", SystemDirectories.Root).Replace("//", "/");
            else if (Uri.IsWellFormedUriString(virtualPath, UriKind.Absolute))
                return virtualPath;
            else
                return VirtualPathUtility.ToAbsolute(virtualPath, SystemDirectories.Root);
        }

        public static Attempt<string> TryResolveUrl(string virtualPath)
        {
            try
            {
                if (virtualPath.StartsWith("~"))
                    return Attempt.Succeed(virtualPath.Replace("~", SystemDirectories.Root).Replace("//", "/"));
                if (Uri.IsWellFormedUriString(virtualPath, UriKind.Absolute))
                    return Attempt.Succeed(virtualPath);
                return Attempt.Succeed(VirtualPathUtility.ToAbsolute(virtualPath, SystemDirectories.Root));
            }
            catch (Exception ex)
            {
                return Attempt.Fail(virtualPath, ex);
            }
        }

        public static string MapPath(string path, bool useHttpContext)
        {
            // Check if the path is already mapped
            if ((path.Length >= 2 && path[1] == Path.VolumeSeparatorChar)
                || path.StartsWith(@"\\")) //UNC Paths start with "\\". If the site is running off a network drive mapped paths will look like "\\Whatever\Boo\Bar"
            {
                return path;
            }
			// Check that we even have an HttpContext! otherwise things will fail anyways
			// http://umbraco.codeplex.com/workitem/30946

            if (useHttpContext && HttpContext.Current != null)
            {
                //string retval;
                if (String.IsNullOrEmpty(path) == false && (path.StartsWith("~") || path.StartsWith(SystemDirectories.Root)))
                    return HostingEnvironment.MapPath(path);
                else
                    return HostingEnvironment.MapPath("~/" + path.TrimStart('/'));
            }

        	var root = GetRootDirectorySafe();
        	var newPath = path.TrimStart('~', '/').Replace('/', IOHelper.DirSepChar);
        	var retval = root + IOHelper.DirSepChar.ToString(CultureInfo.InvariantCulture) + newPath;

        	return retval;
        }

        public static string MapPath(string path)
        {
            return MapPath(path, true);
        }

        //use a tilde character instead of the complete path
		internal static string ReturnPath(string settingsKey, string standardPath, bool useTilde)
        {
            string retval = ConfigurationManager.AppSettings[settingsKey];

            if (String.IsNullOrEmpty(retval))
                retval = standardPath;

            return retval.TrimEnd('/');
        }

        internal static string ReturnPath(string settingsKey, string standardPath)
        {
            return ReturnPath(settingsKey, standardPath, false);

        }

        /// <summary>
        /// Verifies that the current filepath matches a directory where the user is allowed to edit a file.
        /// </summary>
        /// <param name="filePath">The filepath to validate.</param>
        /// <param name="validDir">The valid directory.</param>
        /// <returns>A value indicating whether the filepath is valid.</returns>
        internal static bool VerifyEditPath(string filePath, string validDir)
        {
            return VerifyEditPath(filePath, new[] { validDir });
        }

        /// <summary>
        /// Validates that the current filepath matches a directory where the user is allowed to edit a file.
        /// </summary>
        /// <param name="filePath">The filepath to validate.</param>
        /// <param name="validDir">The valid directory.</param>
        /// <returns>True, if the filepath is valid, else an exception is thrown.</returns>
        /// <exception cref="Exception">The filepath is invalid.</exception>
        internal static bool ValidateEditPath(string filePath, string validDir)
        {
            if (VerifyEditPath(filePath, validDir) == false)
                throw new Exception(string.Format("The filepath '{0}' is not within an allowed directory for this type of files", filePath.Replace(MapPath(SystemDirectories.Root), "")));
            return true;
        }

        /// <summary>
        /// Verifies that the current filepath matches one of several directories where the user is allowed to edit a file.
        /// </summary>
        /// <param name="filePath">The filepath to validate.</param>
        /// <param name="validDirs">The valid directories.</param>
        /// <returns>A value indicating whether the filepath is valid.</returns>
        internal static bool VerifyEditPath(string filePath, IEnumerable<string> validDirs)
        {
            // this is called from ScriptRepository, PartialViewRepository, etc.
            // filePath is the fullPath (rooted, filesystem path, can be trusted)
            // validDirs are virtual paths (eg ~/Views)
            //
            // except that for templates, filePath actually is a virtual path

            //TODO
            // what's below is dirty, there are too many ways to get the root dir, etc.
            // not going to fix everything today

            var mappedRoot = MapPath(SystemDirectories.Root);
            if (filePath.StartsWith(mappedRoot) == false)
                filePath = MapPath(filePath);

            // yes we can (see above)
            //// don't trust what we get, it may contain relative segments
            //filePath = Path.GetFullPath(filePath);

            foreach (var dir in validDirs)
            {
                var validDir = dir;
                if (validDir.StartsWith(mappedRoot) == false)
                    validDir = MapPath(validDir);

                if (PathStartsWith(filePath, validDir, Path.DirectorySeparatorChar))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Verifies that the current filepath has one of several authorized extensions.
        /// </summary>
        /// <param name="filePath">The filepath to validate.</param>
        /// <param name="validFileExtensions">The valid extensions.</param>
        /// <returns>A value indicating whether the filepath is valid.</returns>
        internal static bool VerifyFileExtension(string filePath, List<string> validFileExtensions)
        {
            var ext = Path.GetExtension(filePath);
            return ext != null && validFileExtensions.Contains(ext.TrimStart('.'));
        }

        /// <summary>
        /// Validates that the current filepath has one of several authorized extensions.
        /// </summary>
        /// <param name="filePath">The filepath to validate.</param>
        /// <param name="validFileExtensions">The valid extensions.</param>
        /// <returns>True, if the filepath is valid, else an exception is thrown.</returns>
        /// <exception cref="Exception">The filepath is invalid.</exception>
        internal static bool ValidateFileExtension(string filePath, List<string> validFileExtensions)
        {
            if (VerifyFileExtension(filePath, validFileExtensions) == false)
                throw new Exception(string.Format("The extension for the current file '{0}' is not of an allowed type for this editor. This is typically controlled from either the installed MacroEngines or based on configuration in /config/umbracoSettings.config", filePath.Replace(MapPath(SystemDirectories.Root), "")));
            return true;
        }

        public static bool PathStartsWith(string path, string root, char separator)
        {
            // either it is identical to root,
            // or it is root + separator + anything

            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) == false) return false;
            if (path.Length == root.Length) return true;
            if (path.Length < root.Length) return false;
            return path[root.Length] == separator;
        }

        /// <summary>
        /// Returns the path to the root of the application, by getting the path to where the assembly where this
        /// method is included is present, then traversing until it's past the /bin directory. Ie. this makes it work
        /// even if the assembly is in a /bin/debug or /bin/release folder
        /// </summary>
        /// <returns></returns>
        internal static string GetRootDirectorySafe()
        {
            if (String.IsNullOrEmpty(_rootDir) == false)
            {
                return _rootDir;
            }

			var codeBase = Assembly.GetExecutingAssembly().CodeBase;
			var uri = new Uri(codeBase);
			var path = uri.LocalPath;
        	var baseDirectory = Path.GetDirectoryName(path);
            if (String.IsNullOrEmpty(baseDirectory))
                throw new Exception("No root directory could be resolved. Please ensure that your Umbraco solution is correctly configured.");

            _rootDir = baseDirectory.Contains("bin")
                           ? baseDirectory.Substring(0, baseDirectory.LastIndexOf("bin", StringComparison.OrdinalIgnoreCase) - 1)
                           : baseDirectory;

            return _rootDir;
        }

        internal static string GetRootDirectoryBinFolder()
        {
            string binFolder = string.Empty;
            if (string.IsNullOrEmpty(_rootDir))
            {
                var directoryInfo = Assembly.GetExecutingAssembly().GetAssemblyFile().Directory;
                if (directoryInfo != null)
                {
                    binFolder = directoryInfo.FullName;
                }
                return binFolder;
            }

            binFolder = Path.Combine(GetRootDirectorySafe(), "bin");

#if DEBUG
            var debugFolder = Path.Combine(binFolder, "debug");
            if (Directory.Exists(debugFolder))
                return debugFolder;
#endif
            var releaseFolder = Path.Combine(binFolder, "release");
            if (Directory.Exists(releaseFolder))
                return releaseFolder;

            if (Directory.Exists(binFolder))
                return binFolder;

            return _rootDir;
        }

        /// <summary>
        /// Allows you to overwrite RootDirectory, which would otherwise be resolved
        /// automatically upon application start.
        /// </summary>
        /// <remarks>The supplied path should be the absolute path to the root of the umbraco site.</remarks>
        /// <param name="rootPath"></param>
        internal static void SetRootDirectory(string rootPath)
	    {
            _rootDir = rootPath;
	    }

	    public static void EnsurePathExists(string path)
	    {
	        var absolutePath = MapPath(path);
	        if (Directory.Exists(absolutePath) == false)
	            Directory.CreateDirectory(absolutePath);
	    }

	    public static void EnsureFileExists(string path, string contents)
	    {
	        var absolutePath = IOHelper.MapPath(path);
	        if (File.Exists(absolutePath)) return;

	        using (var writer = File.CreateText(absolutePath))
	        {
	            writer.Write(contents);
	        }
	    }
    }
}