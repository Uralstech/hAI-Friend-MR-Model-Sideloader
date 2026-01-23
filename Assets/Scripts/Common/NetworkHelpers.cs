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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

#nullable enable
public static class NetworkHelpers
{
    public static bool TryGetLocalIPv4([NotNullWhen(true)] out IEnumerable<string> results)
    {
        List<string> resultsList = new();
        foreach (NetworkInterface? ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus is not OperationalStatus.Up
                || ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;
        
            foreach (var ua in ni.GetIPProperties().UnicastAddresses)
            {
                if (ua.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(ua.Address))
                    resultsList.Add(ua.Address.ToString());
            }
        }

        results = resultsList.Distinct();
        return resultsList.Count > 0;
    }

    public static string GenerateShortAuthCode(int length)
    {
        if (length is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(length));

        int spanLength = Mathf.CeilToInt((float)length / 2);
        Span<byte> data = stackalloc byte[spanLength];
        RandomNumberGenerator.Fill(data);

        StringBuilder sb = new(spanLength * 2);
        for (int i = 0; i < spanLength; i++)
            sb.AppendFormat("{0:x2}", data[i]);
        
        return sb.ToString(0, length);
    }
}