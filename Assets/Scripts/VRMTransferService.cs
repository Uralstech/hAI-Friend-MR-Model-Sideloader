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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using TMPro;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;
using Uralstech.AvLoader;
using Uralstech.Utils.Singleton;

#nullable enable
public sealed class VRMTransferService : DontCreateNewSingleton<VRMTransferService>
{
    [SerializeField] private Button _saveAvatarButton;
    [SerializeField] private Button _startSendingProcessButton;
    [SerializeField] private Button _cancelSendingProcessButton;
    [SerializeField] private GameObject _sharePopup;
    [SerializeField] private TMP_Text _ipInfoText;

    private byte[]? _rawAvatarModel;
    private AvMetadata _avatarMetadata;
    private CancellationTokenSource? _shareCts;
    private string _sessionAuthCode;

    private bool IsReadyForExport => _rawAvatarModel.IsValid() && !string.IsNullOrEmpty(_avatarMetadata.Id);
    
    private CustomLogger logger;
    protected override void Awake()
    {
        base.Awake();
        logger = new(this);

        _saveAvatarButton.onClick.AddListener(() => StartCoroutine(SaveAvatarAsync(destroyCancellationToken)));
        _startSendingProcessButton.onClick.AddListener(() => StartCoroutine(StartSendingProcessAsync(destroyCancellationToken)));
        _cancelSendingProcessButton.onClick.AddListener(() =>
        {
            _shareCts?.Cancel();
            _shareCts?.Dispose();
            _shareCts = null;
        });
    }

    private void Start()
    {
        logger.LogCallStart();

        _sessionAuthCode = NetworkHelpers.GenerateShortAuthCode(6);
        VRMFilePicker.Instance.OnAvatarLoaded += data => _rawAvatarModel = data;
        VRMFilePicker.Instance.OnAvatarMetadataLoaded += metadata => _avatarMetadata = metadata;
        VRMFilePicker.Instance.OnClearLoaded += () =>
        {
            _rawAvatarModel = null;
            _avatarMetadata = default;
        };

        VRMRenderViewer.Instance.OnAvatarLoaded += avatar =>
        {
            if (!string.IsNullOrEmpty(_avatarMetadata.Id))
                return;

            _avatarMetadata = new()
            {
                Id = avatar.Vrm.Meta.Name,
                Type = AvType.HumanoidFullBody,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        };

        logger.LogCallComplete();
    }

    private async Awaitable StartSendingProcessAsync(CancellationToken token)
    {
        const int Port = 8080;
        const int TimeoutMS = 1000 * 60 * 10; // 10 Minutes

        logger.LogCallStart();
        if (!IsReadyForExport)
        {
            logger.Log($"Avatar not in state for sharing (model size: {_rawAvatarModel?.Length}, id: {_avatarMetadata.Id}).");
            await Dialog.Instance.Show("No avatar to share or avatar has not finished loading yet.", Dialog.Options.Confirm, token);
            return;
        }

        if (!HttpListener.IsSupported)
        {
            logger.LogError("HttpListener not supported.");
            await Dialog.Instance.Show("Http sharing is not supported on this OS.", Dialog.Options.Confirm, token);
            return;
        }

        if (!NetworkHelpers.TryGetLocalIPv4(out IEnumerable<string> ipsIE))
        {
            logger.LogError("Could not get IP address.");
            await Dialog.Instance.Show("Could not get local IP address to start sharing data to Quest.", Dialog.Options.Confirm, token);
            return;
        }

        VRMSharePayload payload = new(_rawAvatarModel!, _avatarMetadata);
        string[] ips = ipsIE.ToArray();

        _ipInfoText.text =
            $"IP Addresses: {string.Join(", ", ips)}\n" +
            $"Password: {_sessionAuthCode}\nPort: {Port}\n\n" +
            $"Enter these details in the hAI! Friend MR Meta Quest app. " +
            $"If multiple IPs are shown, use your Wi-Fi IP.";

        _sharePopup.SetActive(true);
        
        _shareCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        token = _shareCts.Token;

        try
        {
            logger.Log("Starting listener.");
            await Awaitable.BackgroundThreadAsync();

            using HttpListener listener = new();
            foreach (string ip in ips)
                listener.Prefixes.Add($"http://{ip}:{Port}/vrmShare/");

            listener.Start();

            using CancellationTokenSource cts = new(TimeoutMS);
            HttpListenerContext context;
            
            logger.Log("Listening for requests.");
            using (var _ = token.Register(listener.Abort))
            using (var __ = cts.Token.Register(listener.Abort))
                context = await listener.GetContextAsync();

            token.ThrowIfCancellationRequested();
            if (cts.IsCancellationRequested)
            {
                logger.Log("Listener timed out.");
                _ = Dialog.Instance.Show("Operation timed out.", Dialog.Options.Confirm, token);
                return;
            }

            string? receivedAuthCode = context.Request.Headers["X-Auth-Code"];
            if (string.IsNullOrEmpty(receivedAuthCode) || !string.Equals(receivedAuthCode, _sessionAuthCode, StringComparison.OrdinalIgnoreCase))
            {
                logger.Log($"Received auth code ('{receivedAuthCode}') does not match set code '{_sessionAuthCode}'.");
                listener.Stop();

                _ = Dialog.Instance.Show("Received incorrect password from share target.", Dialog.Options.Confirm, token);
                return;
            }

            logger.Log("Serializing data for sharing.");
            string payloadJson = JsonConvert.SerializeObject(payload);
            
            int payloadSize = Encoding.UTF8.GetByteCount(payloadJson);
            using NativeArray<byte> payloadArray = new(payloadSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            int written = Encoding.UTF8.GetBytes(payloadJson, payloadArray);

            logger.Log("Sharing data.");
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = written;
            context.Response.OutputStream.Write(payloadArray.AsReadOnlySpan()[..written]);
            context.Response.OutputStream.Close();
            listener.Stop();

            logger.Log("Sharing complete.");
            _ = Dialog.Instance.Show("Sharing completed!", Dialog.Options.Confirm, token);
            logger.LogCallComplete();
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException) throw;
            logger.LogError("Failed to share avatar due to exception.");
            logger.LogException(ex);

            _ = Dialog.Instance.Show("Could not share avatar, please try again.", Dialog.Options.Confirm, token);
        }
        finally
        {
            _shareCts?.Dispose();
            _shareCts = null;

            await Awaitable.MainThreadAsync();
            _sharePopup.SetActive(false);
        }
    }

    private async Awaitable SaveAvatarAsync(CancellationToken token)
    {
        logger.LogCallStart();
        if (!IsReadyForExport)
        {
            logger.Log($"Avatar not in state for saving (model size: {_rawAvatarModel?.Length}, id: {_avatarMetadata.Id}).");
            await Dialog.Instance.Show("No avatar to save or avatar has not finished loading yet.", Dialog.Options.Confirm, token);
            return;
        }

        AvMetadata metadata = _avatarMetadata;
        byte[] model = _rawAvatarModel!;

        try
        {
            string directory = Path.Join(Application.persistentDataPath, metadata.Id);
            if (Directory.Exists(directory))
            {
                logger.Log($"Found existing directory with name: {metadata.Id}");
                string additionalData = "(model metadata could not be loaded)";
                if (AvMetadata.TryCreateFromFile(Path.Join(directory, "metadata.json"), out AvMetadata? existing))
                    additionalData = $"ID: {existing.Value.Id}\nCreated at: {existing.Value.CreatedAt.ToLocalTime():f}\nLast update: {existing.Value.UpdatedAt.ToLocalTime():f}";

                Dialog.Options result = await Dialog.Instance.Show($"Found existing avatar, override?\n{additionalData}", Dialog.Options.Both, token);
                if (result != Dialog.Options.Confirm)
                {
                    logger.Log("Cancelled.");
                    return;
                }

                logger.Log("Deleting existing directory.");
                Directory.Delete(directory, recursive: true);
            }

            logger.Log("Writing data.");
            Directory.CreateDirectory(directory);
            await File.WriteAllBytesAsync(Path.Join(directory, "model.vrm"), model, token);
            await File.WriteAllTextAsync(Path.Join(directory, "metadata.json"), JsonConvert.SerializeObject(metadata), token);

            logger.Log("Model saved.");
            await Dialog.Instance.Show("Model successfully saved!", Dialog.Options.Confirm, token);

            logger.LogCallComplete();
        }
        catch (SystemException ex)
        {
            logger.LogError("Failed to save avatar due to system exception.");
            logger.LogException(ex);

            await Dialog.Instance.Show("Could not save avatar, please try again.", Dialog.Options.Confirm, token);
        }
    }
}