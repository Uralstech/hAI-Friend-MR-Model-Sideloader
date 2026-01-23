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

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

#nullable enable
public sealed class CameraMovement : MonoBehaviour
{
    [SerializeField] private float _speed = 2.5f;
    [SerializeField] private Button _resetPosition;

    private Vector3 _defaultPos;

    private void Start()
    {
        _defaultPos = transform.position;
        _resetPosition.onClick.AddListener(ResetPosition);
        VRMFilePicker.Instance.OnAvatarLoaded += _ => ResetPosition();
    }

    private void ResetPosition() => transform.position = _defaultPos;

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0)
                - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);

        float z = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0)
                - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);

        float y = (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed ? 1 : 0)
                - (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed ? 1 : 0);

        Vector3 move = new Vector3(x, y, z).normalized;
        transform.Translate(_speed * Time.deltaTime * move, Space.Self);
    }
}
