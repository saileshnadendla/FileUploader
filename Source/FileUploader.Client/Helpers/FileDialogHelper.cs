using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Client.Helpers
{
    internal class FileDialogHelper : IFileDialogHelper
    {
        public string[] SelectFiles()
        {
            var dlg = new OpenFileDialog { Multiselect = true };

            if (dlg.ShowDialog() == true)
            {
                return dlg.FileNames;
            }

            return new string[0];
        }

        public void SaveSelectedFile(string fileName, byte[] fileBytes)
        {
            var saveFileDialog = new SaveFileDialog();
            saveFileDialog.FileName = fileName;
            saveFileDialog.Filter = "All files (*.*)|*.*";

            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllBytes(saveFileDialog.FileName, fileBytes);
            }
        }
    }
}
