# CmdPlay
Lets you play videos in command line using ASCII-Art.

Compiled binary for windows: https://github.com/young03281/CmdPlay-gpu/releases/tag/gpu-release1.0

Demonstration video: https://www.youtube.com/watch?v=qvRKJj9CbxE

# Known issues / weaknesses
Tell me if you find one

# License
The program code including all cs files and the CmdPlay binary are licensed under the MIT license. This does NOT apply to FFmpeg or NAudio.
FFmpeg, including its source code and binaries are licensed under the GNU Lesser General Public License (LGPL) version 2.1.
NAudio, including its source code and binaries are licensed under the Microsoft Public License (Ms-PL).

# Compiling
Create a new .NET framework (or .NET core) console application. Add a reference to NAudio (dotnet package cli or nuget package manager). Use the generated project to compile CmdPlay.cs
Note: The program requires a working copy of ffmpeg.exe in the current working directory.
