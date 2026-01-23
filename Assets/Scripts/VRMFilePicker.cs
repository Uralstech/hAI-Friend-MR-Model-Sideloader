// Copyright 2026 URAV ADVANCED LEARNING SYSTEMS PRIVATE LIMITED
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SimpleFileBrowser;
using UnityEngine;
using UnityEngine.UI;
using Uralstech.AvLoader;
using Uralstech.Utils.Singleton;

#nullable enable
public sealed class VRMFilePicker : DontCreateNewSingleton<VRMFilePicker>
{
    public event Action? OnClearLoaded;
    public event Action<byte[]>? OnAvatarLoaded;
    public event Action<AvMetadata>? OnAvatarMetadataLoaded;

    [SerializeField] private Button _loadAvatarButton;
    private string? _loadedFile;
    private CustomLogger logger;

    protected override void Awake()
    {
        base.Awake();
        logger = new(this);
        logger.LogCallStart();

        _loadAvatarButton.onClick.AddListener(() => StartCoroutine(LoadAvatarAsync(destroyCancellationToken)));

        logger.Log("Configuring file browser.");
        FileBrowser.SetFilters(false, new FileBrowser.Filter("VRM Model", ".vrm"));

        logger.LogCallComplete();
    }

    private async Awaitable LoadAvatarAsync(CancellationToken token)
    {
        logger.LogCallStart();
        if (FileBrowser.IsOpen)
        {
            logger.Log("File browser already open, skipping.");
            return;
        }

        try
        {
            logger.Log("Showing dialog.");
            
            TaskCompletionSource<string[]?> tcs = new();
            FileBrowser.ShowLoadDialog(result => tcs.TrySetResult(result), () => tcs.TrySetCanceled(),
                FileBrowser.PickMode.Files, allowMultiSelection: false, initialPath: Application.persistentDataPath, title: "Load VRM Model", loadButtonText: "Open");

            string[]? results;
            using (var _ = token.Register(() => tcs.TrySetCanceled()))
                results = await tcs.Task;

            if (results is null || results.Length == 0)
            {
                logger.Log("User did not pick any file, skipping.");
                return;
            }

            string vrmModelPath = results[0];
            logger.Log($"Result: {vrmModelPath}");
            
            if (!string.IsNullOrEmpty(_loadedFile))
            {
                logger.Log("Asking for overwrite.");
                Dialog.Options option = await Dialog.Instance.Show("Replace current avatar and reset scene?", Dialog.Options.Both, token);
                if (option != Dialog.Options.Confirm)
                {
                    logger.Log("User cancelled operation");
                    return;
                }

                OnClearLoaded?.Invoke();
            }
            else if (_loadedFile == vrmModelPath)
            {
                logger.Log("Selected path same as current path, asking for re-import.");
                Dialog.Options option = await Dialog.Instance.Show("Re-import existing avatar and reset scene?", Dialog.Options.Both, token);
                if (option != Dialog.Options.Confirm)
                {
                    logger.Log("User cancelled operation");
                    return;
                }

                OnClearLoaded?.Invoke();
            }

            byte[] data = await File.ReadAllBytesAsync(vrmModelPath, token);
            if (OnAvatarLoaded is not Action<byte[]> call)
            {
                logger.Log("No listener assigned, skipping.");
                return;
            }

            string parent = Path.GetDirectoryName(vrmModelPath);
            string metadataPath = Path.Join(parent, "metadata.json");
            if (AvMetadata.TryCreateFromFile(metadataPath, out AvMetadata? metadata))
                OnAvatarMetadataLoaded?.Invoke(metadata.Value);

            _loadedFile = vrmModelPath;
            call(data);
            
            logger.LogCallComplete();
        }
        catch (OperationCanceledException)
        {
            logger.Log("Cancelled.");
            return;
        }
        catch (SystemException ex)
        {
            logger.LogError("Failed to load avatar due to system exception.");
            logger.LogException(ex);

            await Dialog.Instance.Show("Could not load avatar, please try again.", Dialog.Options.Confirm, token);
        }
    }
}
