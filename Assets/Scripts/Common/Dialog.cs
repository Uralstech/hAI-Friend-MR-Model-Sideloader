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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Uralstech.Utils.Singleton;

#nullable enable
public sealed class Dialog : DontCreateNewSingleton<Dialog>
{
    [SerializeField] private GameObject _base;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TMP_Text _text;

    public enum Options
    {
        None = 0,
        Confirm = 1 << 0,
        Cancel = 1 << 1,
        Both = Confirm | Cancel
    }

    private readonly struct Request
    {
        public readonly string Text;
        public readonly Options Requested;
        public readonly Func<Options, bool> OnDone;
        public readonly CancellationToken Token;
        public Request(string text, Options options, Func<Options, bool> onDone, CancellationToken token)
        {
            Text = text;
            Requested = options;
            OnDone = onDone;
            Token = token;
        }
    }

    private readonly ConcurrentQueue<Request> _requests = new();
    private Task? _currentRequestProcess = null;

    private CustomLogger logger;
    protected override void Awake()
    {
        base.Awake();
        logger = new(this);
    }

    public async Awaitable<Options> Show(string text, Options options, CancellationToken token)
    {
        logger.LogCallStart();
        if (options == Options.None)
        {
            logger.LogError("Called with None options.");
            return Options.None;
        }

        await Awaitable.MainThreadAsync();
        TaskCompletionSource<Options> tcs = new();
        Request request = new(text, options, tcs.TrySetResult, token);
        _requests.Enqueue(request);

        logger.Log("Awaiting.");
        Options result;

        using (var _ = token.Register(() => tcs.TrySetCanceled()))
            result = await tcs.Task;

        logger.LogCallComplete();
        return result;
    }

    private void Update()
    {
        if ((_currentRequestProcess is null || _currentRequestProcess.IsCompleted) && _requests.TryDequeue(out Request request))
            _currentRequestProcess = ProcessRequestAsync(request, destroyCancellationToken);
    }

    private async Task ProcessRequestAsync(Request request, CancellationToken token)
    {
        logger.LogCallStart();
        if (request.Token.IsCancellationRequested)
        {
            logger.Log("Request cancelled.");
            return;
        }

        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(request.Token, token);
        token = linked.Token;

        TaskCompletionSource<Options> tcs = new();
        void SetConfirm() => tcs.TrySetResult(Options.Confirm);
        void SetCancel() => tcs.TrySetResult(Options.Cancel);

        _confirmButton.onClick.AddListener(SetConfirm);
        _cancelButton.onClick.AddListener(SetCancel);

        _base.SetActive(true);
        _confirmButton.gameObject.SetActive((request.Requested & Options.Confirm) == Options.Confirm);
        _cancelButton.gameObject.SetActive((request.Requested & Options.Cancel) == Options.Cancel);
        _text.text = request.Text;

        logger.Log("Listening.");

        try
        {
            Options result;
            using (var _ = token.Register(() => tcs.TrySetCanceled()))
                result = await tcs.Task;

            logger.Log($"Got result: {result}");
            request.OnDone(result);

            logger.LogCallComplete();
        }
        finally
        {
            _confirmButton.onClick.RemoveListener(SetConfirm);
            _cancelButton.onClick.RemoveListener(SetCancel);
            _base.SetActive(false);
        }
    }
}