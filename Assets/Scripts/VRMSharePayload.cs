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

using Newtonsoft.Json;
using Uralstech.AvLoader;

#nullable enable
[JsonObject]
public class VRMSharePayload
{
    [JsonProperty("vrm")]
    public byte[] ModelData;

    [JsonProperty("metadata")]
    public AvMetadata AvMetadata;

    public VRMSharePayload(byte[] modelData, AvMetadata avMetadata)
    {
        ModelData = modelData;
        AvMetadata = avMetadata;
    }
}