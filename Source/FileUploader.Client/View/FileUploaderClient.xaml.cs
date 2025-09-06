using FileUploader.Client.Model;
using FileUploader.Client.ViewModel;
using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;

namespace FileUploader.Client.View
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IFileUploaderClientViewModel myViewModel;

        public MainWindow()
        {
            InitializeComponent();
            myViewModel = new FileUploaderClientViewModel();
            DataContext = myViewModel;

            myViewModel.Files.CollectionChanged += Files_CollectionChanged;

        }

        private void Files_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (FileItem item in e.NewItems)
                    item.PropertyChanged += FileItem_PropertyChanged;
            }

            if (e.OldItems != null)
            {
                foreach (FileItem item in e.OldItems)
                    item.PropertyChanged -= FileItem_PropertyChanged;
            }
        }

        private void FileItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(FileItem.Status))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    foreach (var cvsKey in new[] { "ReadyFilesView", "QueuedFilesView", "CompletedFilesView", "FailedFilesView" })
                    {
                        if (Application.Current.MainWindow.Resources[cvsKey] is CollectionViewSource cvs)
                            cvs.View.Refresh();
                    }
                });
            }
        }

        private void ReadyFiles_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is FileItem file)
                e.Accepted = file.Status == "Ready";
        }

        private void QueuedFiles_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is FileItem file)
                e.Accepted = file.Status == "Queued" || file.Status == "InProgress";
        }

        private void CompletedFiles_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is FileItem file)
                e.Accepted = file.Status == "Completed";
        }

        private void FailedFiles_Filter(object sender, FilterEventArgs e)
        {
            if (e.Item is FileItem file)
                e.Accepted = file.Status == "Failed";
        }

    }
}
