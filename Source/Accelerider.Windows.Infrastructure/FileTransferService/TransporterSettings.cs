﻿using System.Net;
using System.Net.Http.Headers;

namespace Accelerider.Windows.Infrastructure.FileTransferService
{
    public class TransporterSettings
    {
        public WebHeaderCollection Headers { get; set; }=new WebHeaderCollection()
        {
            ["User-Agent"]="Accelerider.Windows.DownloadEngine"
        };

        public int MaxErrorCount { get; set; }

        /// <summary>
        /// If some uri speed is faster than current uri, this option can auto switch into that uri.
        /// </summary>
        public bool AutoSwitchUri { get; set; }

        public int ConnectTimeout { get; set; } = 1000 * 30; //30S

        public int ReadWriteTimeout { get; set; } = 1000 * 30; //30S

        public int ThreadCount { get; set; } = 16;

        public DataSize BlockSize { get; set; } = 10 * DataSize.OneMB; //10MB

        public void CopyTo(TransporterSettings settings)
        {
            settings.AutoSwitchUri = AutoSwitchUri;
            settings.BlockSize = BlockSize;
            settings.ConnectTimeout = ConnectTimeout;
            settings.Headers = Headers;
            settings.MaxErrorCount = MaxErrorCount;
            settings.ReadWriteTimeout = ReadWriteTimeout;
            settings.ThreadCount = ThreadCount;
        }
    }
}
