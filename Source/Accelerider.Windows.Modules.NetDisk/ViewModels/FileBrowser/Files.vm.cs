﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Accelerider.Windows.Infrastructure;
using Accelerider.Windows.Infrastructure.Mvvm;
using Accelerider.Windows.Modules.NetDisk.Enumerations;
using Accelerider.Windows.Modules.NetDisk.Models;
using Accelerider.Windows.Modules.NetDisk.ViewModels.Dialogs;
using Accelerider.Windows.Modules.NetDisk.Views.Dialogs;
using Accelerider.Windows.Modules.NetDisk.Views.FileBrowser;
using Accelerider.Windows.Resources.I18N;
using Unity;
using MaterialDesignThemes.Wpf;


namespace Accelerider.Windows.Modules.NetDisk.ViewModels.FileBrowser
{
    public class FilesViewModel : LoadingFilesBaseViewModel<ILazyTreeNode<INetDiskFile>>, IViewLoadedAndUnloadedAware<Files>, INotificable
    {
        private ILazyTreeNode<INetDiskFile> _selectedSearchResult;
        private ILazyTreeNode<INetDiskFile> _currentFolder;

        public ISnackbarMessageQueue GlobalMessageQueue { get; set; }

        public FilesViewModel(IUnityContainer container) : base(container)
        {
            InitializeCommands();

            EventAggregator.GetEvent<SelectedSearchResultChangedEvent>().Subscribe(selectedSearchResult => SelectedSearchResult = selectedSearchResult);
        }

        public ILazyTreeNode<INetDiskFile> SelectedSearchResult
        {
            get => _selectedSearchResult;
            set => SetProperty(ref _selectedSearchResult, value);
        }

        public ILazyTreeNode<INetDiskFile> CurrentFolder
        {
            get => _currentFolder;
#pragma warning disable 4014
            set { if (SetProperty(ref _currentFolder, value)) RefreshFilesCommand.Execute(); }
#pragma warning restore 4014
        }

        #region Commands

        public ICommand EnterFolderCommand { get; private set; }

        public ICommand DownloadCommand { get; private set; }

        public ICommand UploadCommand { get; private set; }

        public ICommand ShareCommand { get; private set; }

        public ICommand DeleteCommand { get; private set; }


        private void InitializeCommands()
        {
            EnterFolderCommand = new RelayCommand<ILazyTreeNode<INetDiskFile>>(file => CurrentFolder = file, file => file?.Content?.Type == FileType.FolderType);
            DownloadCommand = new RelayCommand<IList>(DownloadCommandExecute, files => files != null && files.Count > 0);
            UploadCommand = new RelayCommand(UploadCommandExecute);
            ShareCommand = new RelayCommand<IList>(ShareCommandExecute, files => files != null && files.Count > 0);
            DeleteCommand = new RelayCommand<IList>(DeleteCommandExecute, files => files != null && files.Count > 0);
        }

        private async void DownloadCommandExecute(IList files)
        {
            var fileArray = files.Cast<ILazyTreeNode<INetDiskFile>>().ToArray();

            var (to, isDownload) = await DisplayDownloadDialogAsync(fileArray.Select(item => item.Content.Path.FileName));

            if (!isDownload) return;

            var currentFolderPathLength = CurrentFolder.Content.Path.FullPath.Length;
            var fileNames = new List<string>(fileArray.Length);
            foreach (var fileNode in fileArray)
            {
                var targetPath = to;
                var substringStart = currentFolderPathLength;
                if (fileNode.Content.Type == FileType.FolderType)
                {
                    var rootPath = fileNode.Content.Path.FullPath.Substring(substringStart);
                    targetPath = CombinePath(to, rootPath).GetUniqueLocalPath(Directory.Exists);
                    substringStart += rootPath.Length;
                }

                await fileNode.ForEachAsync(file =>
                {
                    if (file.Type == FileType.FolderType) return;

                    var downloadingFile = CurrentNetDiskUser.Download(
                        file,
                        CombinePath(targetPath, file.Path.FolderPath.Substring(substringStart)));
                    downloadingFile.Operations.Ready();
                    // Send this transfer item to the downloading view model.
                    EventAggregator.GetEvent<TransferItemsAddedEvent>().Publish(downloadingFile);
                    fileNames.Add(downloadingFile.File.Path.FileName);
                }, CancellationToken.None);
            }

            if (fileNames.Any())
            {
                var fileName = fileNames.First().TrimMiddle(40);
                var message = fileNames.Count == 1
                    ? string.Format(UiStrings.Message_AddedFileToDownloadList, fileName)
                    : string.Format(UiStrings.Message_AddedFilesToDownloadList, fileName, fileNames.Count);
                GlobalMessageQueue.Enqueue(message);
            }
            else
            {
                GlobalMessageQueue.Enqueue("No Files Found");
            }
        }

        private string CombinePath(params string[] paths)
        {
            return paths.Aggregate((acc, item) =>
                item.StartsWith("/") || item.StartsWith("\\")
                    ? acc + item
                    : Path.Combine(acc, item));
        }

        private async void UploadCommandExecute()
        {
            //var dialog = new OpenFileDialog { Multiselect = true };
            //if (dialog.ShowDialog() != DialogResult.OK || dialog.FileNames.Length <= 0) return;

            //var uploadItemList = new List<TransferItem>();
            //await Task.Run(() =>
            //{
            //    foreach (var from in dialog.FileNames)
            //    {
            //        var to = CurrentFolder.Content;
            //        var token = CurrentNetDiskUser.UploadAsync(from, to, item =>
            //        {
            //            // Add new task to download list. ??

            //            // Records tokens
            //            uploadItemList.Add(item);
            //        });
            //    }
            //});

            //var fileName = TrimFileName(dialog.FileNames[0], 40);
            //var message = dialog.FileNames.Length == 1
            //    ? string.Format(UiStrings.Message_AddedFileToUploadList, fileName)
            //    : string.Format(UiStrings.Message_AddedFilesToUploadList, fileName, dialog.FileNames.Length);
            //GlobalMessageQueue.Enqueue(message);
        }

        private void ShareCommandExecute(IList files)
        {
            // 1. Display dialog.

            // 2. Determines whether to share based on the return value of dialog.

            // 3. Sends the GlobalMessageQueue for reporting result.
        }

        private async void DeleteCommandExecute(IList files)
        {
            var currentFolder = CurrentFolder;
            var fileArray = files.Cast<ILazyTreeNode<INetDiskFile>>().ToArray();

            var errorFileCount = 0;
            foreach (var file in fileArray)
            {
                if (!await CurrentNetDiskUser.DeleteFileAsync(file.Content)) errorFileCount++;
            }

            if (errorFileCount < fileArray.Length)
            {
                await RefreshFilesCommand.Execute();
            }

            GlobalMessageQueue.Enqueue($"({fileArray.Length - errorFileCount}/{fileArray.Length}) files have been deleted.");
        }
        #endregion

        protected override async Task<IList<ILazyTreeNode<INetDiskFile>>> GetFilesAsync()
        {
            if (PreviousNetDiskUser != CurrentNetDiskUser)
            {
                PreviousNetDiskUser = CurrentNetDiskUser;
                _currentFolder = await CurrentNetDiskUser.GetFileRootAsync();
                RaisePropertyChanged(nameof(CurrentFolder));
            }

            await CurrentFolder.RefreshAsync();
            return CurrentFolder.ChildrenCache?.ToList();
        }

        private async Task<(string folder, bool isDownload)> DisplayDownloadDialogAsync(IEnumerable<string> files)
        {
            var settings = Container.Resolve<IDataRepository>().Get<NetDiskSettings>();
            if (settings.DoNotDisplayDownloadDialog)
                return (settings.DownloadDirectory, true);

            var dialog = new DownloadDialog();
            var vm = dialog.DataContext as DownloadDialogViewModel;
            vm.DownloadItems = files.ToList();

            if (!(bool)await DialogHost.Show(dialog, "RootDialog")) return (null, false);

            settings.DoNotDisplayDownloadDialog = vm.NotDisplayDownloadDialog;
            if (vm.NotDisplayDownloadDialog)
            {
                settings.DownloadDirectory = vm.DownloadFolder.ToString();
            }
            return (vm.DownloadFolder, true);
        }

        public void OnLoaded(Files view)
        {
            base.OnLoaded();

            view.ListboxFileList.SelectionChanged += OnSelectedFileItemChanged;
        }

        public void OnUnloaded(Files view)
        {
            base.OnUnloaded();

            view.ListboxFileList.SelectionChanged -= OnSelectedFileItemChanged;
        }

        private void OnSelectedFileItemChanged(object sender, SelectionChangedEventArgs e)
        {
            var fileList = (System.Windows.Controls.ListBox)sender;
            fileList.ScrollIntoView(SelectedSearchResult);
        }
    }
}
