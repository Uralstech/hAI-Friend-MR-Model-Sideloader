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
using System.Threading;
using UnityEngine;
using UniVRM10;
using Uralstech.Utils.Singleton;

#nullable enable
public sealed class VRMRenderViewer : DontCreateNewSingleton<VRMRenderViewer>
{
    public event Action<Vrm10Instance>? OnAvatarLoaded;

    [SerializeField] private RuntimeAnimatorController _animatorController;

    private CustomLogger logger;
    private Vrm10Instance? _model;
    private bool _waitShown;

    protected override void Awake()
    {
        base.Awake();
        logger = new(this);
        Application.targetFrameRate = 120;
    }

    private void Start()
    {
        logger.LogCallStart();

        VRMFilePicker.Instance.OnAvatarLoaded += data => StartCoroutine(LoadAvatarAsync(data, destroyCancellationToken));
        VRMFilePicker.Instance.OnClearLoaded += EnsureModelDestroyed;
        logger.LogCallComplete();
    }

    private async Awaitable LoadAvatarAsync(byte[] data, CancellationToken token)
    {
        EnsureModelDestroyed();

        if (!_waitShown)
        {
            _ = Dialog.Instance.Show("Loading the model may take some time. Please wait.", Dialog.Options.Confirm, token);
            _waitShown = true;
        }

        _model = await Vrm10.LoadBytesAsync(data, ct: token);
        _model.Runtime.ControlRig.ControlRigAnimator.runtimeAnimatorController = _animatorController;
        _model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        OnAvatarLoaded?.Invoke(_model);
    }

    private void EnsureModelDestroyed()
    {
        if (_model != null)
        {
            Destroy(_model.gameObject);
            _model.DisposeRuntime();
        }
    }
}
