﻿#region Copyright (C) 2007-2015 Team MediaPortal

/*
    Copyright (C) 2007-2015 Team MediaPortal
    http://www.team-mediaportal.com

    This file is part of MediaPortal 2

    MediaPortal 2 is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    MediaPortal 2 is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with MediaPortal 2. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion

using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using MediaPortal.Common.ResourceAccess;

namespace MediaPortal.Plugins.Transcoding.Service.Transcoders.Base
{
  internal class TranscodeData
  {
    protected string _overrideParams = null;

    public TranscodeData(string binTranscoder, string workPath)
    {
      TranscoderBinPath = binTranscoder;
      WorkPath = workPath;
    }

    public string TranscodeId;
    public string TranscoderBinPath;
    public List<string> GlobalArguments = new List<string>();
    public List<string> InputArguments = new List<string>();
    public List<string> InputSubtitleArguments = new List<string>();
    public List<string> OutputArguments = new List<string>();
    public List<string> OutputFilter = new List<string>();
    public IResourceAccessor InputResourceAccessor;
    public string InputSubtitleFilePath;
    public string OutputFilePath;
    public string SegmentPlaylist = null;
    public string WorkPath;

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    protected static extern uint GetShortPathName([MarshalAs(UnmanagedType.LPTStr)] string lpszLongPath, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszShortPath, uint cchBuffer);
    protected string GetFileShortName(string fileName)
    {
      StringBuilder shortNameBuffer = new StringBuilder(256);
      uint result = GetShortPathName(fileName, shortNameBuffer, 256);
      return shortNameBuffer.ToString();
    }

    public virtual string TranscoderArguments { get; set; }
  }
}
