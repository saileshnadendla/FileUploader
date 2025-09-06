using FileUploader.Client.Model;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FileUploader.Client.ViewModel
{
    internal interface IFileUploaderClientViewModel
    {
        DelegateCommand OnSelectFiles { get; }
        DelegateCommand OnUploadFiles { get; }
        DelegateCommand<FileItem> DownloadCommand { get; }

        ObservableCollection<FileItem> Files { get; }
    }
}
