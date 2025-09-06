using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Client.Helpers
{
    internal interface IFileDialogHelper
    {
        string[] SelectFiles();
        void SaveSelectedFile(string fileName, byte[] fileBytes);
    }
}
