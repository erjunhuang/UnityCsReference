// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEditor.PackageManager.UI.Internal
{
    using ConfigStatus = UnityEditorInternal.AssetStoreCachePathManager.ConfigStatus;

    [Serializable]
    internal class AssetStoreDownloadOperation : IOperation
    {
        internal static readonly string k_DownloadErrorMessage = L10n.Tr("The download could not be completed. See details in console.");
        internal static readonly string k_AbortErrorMessage = L10n.Tr("The download could not be aborted. Please try again.");
        internal static readonly string k_AssetStoreDownloadPrefix = "content__";
        internal static readonly string k_ForbiddenErrorMessage = L10n.Tr("The Asset Store package you are trying to download is not available to the current Unity account. If you purchased this asset from the Asset Store using a different account, use that Unity account to sign into the Editor.");
        private static readonly string k_ConsoleLogPrefix = L10n.Tr("[Package Manager Window]");

        [SerializeField]
        private string m_ProductId;
        public string packageUniqueId => m_ProductId;
        public string versionUniqueId => string.Empty;

        [SerializeField]
        private string m_ProductOldPath;
        public string packageOldPath => m_ProductOldPath;

        [SerializeField]
        private string m_ProductNewPath;
        public string packageNewPath => m_ProductNewPath;


        // a timestamp is added to keep track of how `fresh` the result is
        // it doesn't apply in the case of download operations
        public long timestamp => 0;
        public long lastSuccessTimestamp => 0;

        public bool isOfflineMode => false;

        public bool isInProgress => (state & DownloadState.InProgress) != 0;

        public bool isInPause => (state & DownloadState.InPause) != 0;

        public bool isProgressVisible => (state & ~DownloadState.DownloadRequested & (DownloadState.InPause | DownloadState.InProgress)) != 0;

        public bool isProgressTrackable => true;

        public float progressPercentage => m_TotalBytes > 0 ? m_DownloadedBytes / (float)m_TotalBytes : 0.0f;

        public RefreshOptions refreshOptions => RefreshOptions.None;

        public event Action<IOperation, UIError> onOperationError = delegate {};
        public event Action<IOperation> onOperationSuccess = delegate {};
        public event Action<IOperation> onOperationFinalized = delegate {};
        public event Action<IOperation> onOperationProgress = delegate {};
        public event Action<IOperation> onOperationPaused = delegate {};

        [SerializeField]
        private ulong m_DownloadedBytes;
        [SerializeField]
        private ulong m_TotalBytes;

        [SerializeField]
        private DownloadState m_State;
        public DownloadState state => m_State;

        [SerializeField]
        private string m_ErrorMessage;
        public string errorMessage => m_ErrorMessage;

        [SerializeField]
        private AssetStoreDownloadInfo m_DownloadInfo;
        public AssetStoreDownloadInfo downloadInfo => m_DownloadInfo;

        [NonSerialized]
        private AssetStoreUtils m_AssetStoreUtils;
        [NonSerialized]
        private AssetStoreRestAPI m_AssetStoreRestAPI;
        [NonSerialized]
        private AssetStoreCachePathProxy m_AssetStoreCachePathProxy;
        public void ResolveDependencies(AssetStoreUtils assetStoreUtils,
            AssetStoreRestAPI assetStoreRestAPI,
            AssetStoreCachePathProxy assetStoreCachePathProxy)
        {
            m_AssetStoreUtils = assetStoreUtils;
            m_AssetStoreRestAPI = assetStoreRestAPI;
            m_AssetStoreCachePathProxy = assetStoreCachePathProxy;
        }

        private AssetStoreDownloadOperation()
        {
        }

        public AssetStoreDownloadOperation(AssetStoreUtils assetStoreUtils, AssetStoreRestAPI assetStoreRestAPI, AssetStoreCachePathProxy assetStoreCachePathProxy, string productId, string oldPath)
        {
            ResolveDependencies(assetStoreUtils, assetStoreRestAPI, assetStoreCachePathProxy);

            m_ProductId = productId;
            m_ProductOldPath = oldPath;
        }

        public void OnDownloadProgress(string message, ulong bytes, ulong total, int errorCode)
        {
            switch (message)
            {
                case "ok":
                    m_State = DownloadState.Completed;
                    onOperationSuccess?.Invoke(this);
                    onOperationFinalized?.Invoke(this);
                    break;
                case "connecting":
                    m_State = DownloadState.Connecting;
                    break;
                case "downloading":
                    if (!isInPause)
                        m_State = DownloadState.Downloading;
                    m_DownloadedBytes = Math.Max(m_DownloadedBytes, bytes);
                    m_TotalBytes = Math.Max(m_TotalBytes, total);
                    break;
                case "decrypt":
                    m_State = DownloadState.Decrypting;
                    break;
                case "aborted":
                    if (!isInPause)
                    {
                        m_DownloadedBytes = 0;
                        m_State = DownloadState.Aborted;
                        m_ErrorMessage = L10n.Tr("Download aborted.");
                        onOperationError?.Invoke(this, new UIError(UIErrorCode.AssetStoreOperationError, m_ErrorMessage, UIError.Attribute.IsClearable | UIError.Attribute.IsWarning));
                        onOperationFinalized?.Invoke(this);
                    }
                    else
                    {
                        m_State = DownloadState.Paused;
                        onOperationPaused?.Invoke(this);
                    }
                    break;
                default:
                    OnErrorMessage(message, errorCode);
                    break;
            }

            onOperationProgress?.Invoke(this);
        }

        private void OnErrorMessage(string errorMessage, int operationErrorCode = -1, UIError.Attribute attr = UIError.Attribute.None)
        {
            m_State = DownloadState.Error;

            if ((attr & UIError.Attribute.IsWarning) != 0)
                Debug.LogWarning($"{k_ConsoleLogPrefix} {errorMessage}");
            else
                Debug.LogError($"{k_ConsoleLogPrefix} {errorMessage}");

            if (operationErrorCode == 403)
            {
                m_ErrorMessage = k_ForbiddenErrorMessage;
            }
            else
            {
                attr |= UIError.Attribute.IsDetailInConsole | UIError.Attribute.IsClearable;
                m_ErrorMessage = k_DownloadErrorMessage;
            }

            onOperationError?.Invoke(this, new UIError(UIErrorCode.AssetStoreOperationError, m_ErrorMessage, attr, operationErrorCode));
            onOperationFinalized?.Invoke(this);
        }

        public void Pause()
        {
            if (downloadInfo?.isValid != true)
                return;

            if (state == DownloadState.Aborted || state == DownloadState.Completed || state == DownloadState.Error || state == DownloadState.Paused)
                return;

            m_State = DownloadState.Pausing;

            // Pause here is the same as aborting the download, but we don't delete the file so we can resume from where we paused it from
            if (!m_AssetStoreUtils.AbortDownload(downloadInfo.destination))
                Debug.LogError($"{k_ConsoleLogPrefix} {k_AbortErrorMessage}");
        }

        public void Cancel()
        {
            if (downloadInfo?.isValid != true)
                return;

            m_AssetStoreUtils.AbortDownload(downloadInfo.destination);
            m_DownloadedBytes = 0;
            m_State = DownloadState.None;
            onOperationFinalized?.Invoke(this);
        }

        public void Abort()
        {
            if (!isInProgress && !isInPause)
                return;

            // We reset everything if we cancel after pausing a download
            if (state == DownloadState.Paused)
            {
                m_DownloadedBytes = 0;
                m_State = DownloadState.Aborted;
                onOperationFinalized?.Invoke(this);
                return;
            }

            m_State = DownloadState.AbortRequsted;

            if (downloadInfo?.isValid != true)
                return;

            // the actual download state change from `downloading` to `aborted` happens in `OnDownloadProgress` callback
            if (!m_AssetStoreUtils.AbortDownload(downloadInfo.destination))
                Debug.LogError($"{k_ConsoleLogPrefix} {k_AbortErrorMessage}");
        }

        public void Download(bool resume)
        {
            var config = m_AssetStoreCachePathProxy.GetConfig();
            if (config.status == ConfigStatus.ReadOnly)
            {
                OnErrorMessage("The Assets Cache location is read-only, see configuration in Preferences | Package Manager", -1, UIError.Attribute.IsWarning);
                return;
            }
            if (config.status == ConfigStatus.InvalidPath)
            {
                OnErrorMessage("The Assets Cache location is invalid or inaccessible, see configuration in Preferences | Package Manager", -1, UIError.Attribute.IsWarning);
                return;
            }

            m_State = resume ? DownloadState.ResumeRequested : DownloadState.DownloadRequested;
            var productId = long.Parse(m_ProductId);
            m_AssetStoreRestAPI.GetDownloadDetail(productId, downloadInfo =>
            {
                // if the user requested to abort before receiving the download details, we can simply discard the download info and do nothing
                if (m_State == DownloadState.AbortRequsted)
                    return;

                m_DownloadInfo = downloadInfo;
                if (!downloadInfo.isValid)
                {
                    OnErrorMessage(downloadInfo.errorMessage, downloadInfo.errorCode);
                    return;
                }

                var dest = downloadInfo.destination;

                var publisher = string.Empty;
                var category = string.Empty;
                var packageName = string.Empty;

                if (dest.Length >= 1)
                    publisher = dest[0];
                if (dest.Length >= 2)
                    category = dest[1];
                if (dest.Length >= 3)
                    packageName = dest[2];

                var basePath = m_AssetStoreUtils.BuildBaseDownloadPath(publisher, category);
                m_ProductNewPath = m_AssetStoreUtils.BuildFinalDownloadPath(basePath, packageName);

                var json = m_AssetStoreUtils.CheckDownload(
                    $"{k_AssetStoreDownloadPrefix}{downloadInfo.productId}",
                    downloadInfo.url, dest,
                    downloadInfo.key);

                var resumeOK = false;
                try
                {
                    var current = Json.Deserialize(json) as IDictionary<string, object>;
                    if (current == null)
                        throw new ArgumentException("Invalid JSON");

                    var inProgress = current.ContainsKey("in_progress") && (current["in_progress"] is bool? (bool)current["in_progress"] : false);
                    if (inProgress)
                    {
                        if (!isInPause)
                            m_State = DownloadState.Downloading;
                        return;
                    }

                    if (current.ContainsKey("download") && current["download"] is IDictionary<string, object>)
                    {
                        var download = (IDictionary<string, object>)current["download"];
                        var existingUrl = download.ContainsKey("url") ? download["url"] as string : string.Empty;
                        var existingKey = download.ContainsKey("key") ? download["key"] as string : string.Empty;
                        resumeOK = (existingUrl == downloadInfo.url && existingKey == downloadInfo.key);
                    }
                }
                catch (Exception e)
                {
                    OnErrorMessage(e.Message);
                    return;
                }

                json = $"{{\"download\":{{\"url\":\"{downloadInfo.url}\",\"key\":\"{downloadInfo.key}\"}}}}";
                m_AssetStoreUtils.Download(
                    $"{k_AssetStoreDownloadPrefix}{downloadInfo.productId}",
                    downloadInfo.url,
                    dest,
                    downloadInfo.key,
                    json,
                    resumeOK && resume);

                m_State = DownloadState.Connecting;
            });
        }
    }
}
