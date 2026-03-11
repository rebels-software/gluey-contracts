// Copyright 2026 Rebels Software sp. z o.o.
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

namespace Gluey.Contract;

/// <summary>
/// A validation error with an RFC 6901 JSON Pointer path, machine-readable error code, and human-readable message.
/// </summary>
public readonly struct ValidationError
{
    /// <summary>RFC 6901 JSON Pointer path to the failing property.</summary>
    public readonly string Path;

    /// <summary>Machine-readable error code identifying the validation failure.</summary>
    public readonly ValidationErrorCode Code;

    /// <summary>Human-readable static error message describing the failure.</summary>
    public readonly string Message;

    /// <summary>
    /// Creates a new <see cref="ValidationError"/>.
    /// </summary>
    /// <param name="path">RFC 6901 JSON Pointer path to the failing property.</param>
    /// <param name="code">Machine-readable error code.</param>
    /// <param name="message">Human-readable error message.</param>
    public ValidationError(string path, ValidationErrorCode code, string message)
    {
        Path = path;
        Code = code;
        Message = message;
    }
}
