﻿using System.Collections.Generic;

namespace BlogEngine.Core.FileSystem
{
    public class FileResponse
    {
        public FileResponse()
        {
            Files = new List<FileInstance>();
            Path = string.Empty;
        }
        public IEnumerable<FileInstance> Files { get; set; }
        public string Path { get; set; }
    }

    public class FileInstance
    {
        public bool IsChecked { get; set; }
        public string Created { get; set; }
        public string Name { get; set; }
        public string FileSize { get; set; }
        public FileType FileType { get; set; }
        public string FullPath { get; set; }
        /// <summary>
        /// Image assosiated with file extension
        /// </summary>
        public string ImgPlaceholder
        {
            get
            {
                if(FileType == FileType.File)
                {
                    return getPlaceholder(Name);
                }
                return string.Empty;
            }
        }

        static string getPlaceholder(string name)
        {
            var file = name.ToLower().Trim();

            if (file.EndsWith(".zip") || file.EndsWith(".gzip") || file.EndsWith(".7zip") || file.EndsWith(".rar"))
            {
                return "zip.png";
            }
            if (file.EndsWith(".doc") || file.EndsWith(".docx"))
            {
                return "doc.png";
            }
            if (file.EndsWith(".xls") || file.EndsWith(".xlsx"))
            {
                return "xls.png";
            }
            if (file.EndsWith(".pdf"))
            {
                return "pdf.png";
            }
            if (file.EndsWith(".txt"))
            {
                return "txt.png";
            }

            return "new.png";
        }
    }

    public enum FileType
    {
        Directory,
        File,
        Image,
        None
    }
}
