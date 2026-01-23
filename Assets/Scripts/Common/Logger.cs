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

using System.Runtime.CompilerServices;
using UnityEngine;

#nullable enable
public class CustomLogger
{
    private readonly Object _instance;
    private readonly string _className;

    public CustomLogger(Object obj)
    {
        _instance = obj;
        _className = obj.GetType().Name;
    }

    public void Log(string message, [CallerMemberName] string caller = null!) => LogFormat(Debug.Log, caller, message);
    public void LogWarning(string message, [CallerMemberName] string caller = null!) => LogFormat(Debug.LogWarning, caller, message);
    public void LogError(string message, [CallerMemberName] string caller = null!) => LogFormat(Debug.LogError, caller, message);
    public void LogException(System.Exception ex) => Debug.LogException(ex, _instance);

    public void LogCallStart([CallerMemberName] string caller = null!) => LogFormat(Debug.Log, caller, "Started.");
    public void LogCallComplete([CallerMemberName] string caller = null!) => LogFormat(Debug.Log, caller, "Completed.");

    private void LogFormat(System.Action<object, Object> method, string caller, string message) => method($"{_className}: ({caller}) {message}", _instance);
}
