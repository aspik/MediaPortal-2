﻿#region Copyright (C) 2007-2012 Team MediaPortal

/*
    Copyright (C) 2007-2012 Team MediaPortal
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Threading;
using System.Linq;
using System.Threading.Tasks;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Common.Services.ResourceAccess.LocalFsResourceProvider;
using MediaPortal.Extensions.MetadataExtractors.FFMpegLib;
using MediaPortal.Plugins.Transcoding.Service.Transcoders.Base;
using MediaPortal.Plugins.Transcoding.Service.Transcoders.Base.Metadata;
using MediaPortal.Plugins.Transcoding.Service.Transcoders.FFMpeg;
using MediaPortal.Plugins.Transcoding.Service.Transcoders.FFMpeg.Converters;
using MediaPortal.Utilities.Process;
using System.Collections.ObjectModel;
using MediaPortal.Plugins.Transcoding.Service.Transcoders.FFMpeg.Encoders;
using System.Globalization;

namespace MediaPortal.Plugins.Transcoding.Service
{
  public class MediaConverter : Metadata
  {
    public string TranscoderCachePath { get; set; }
    public string TranscoderBinPath { get; set; }
    public long TranscoderMaximumCacheSize { get; set; }
    public long TranscoderMaximumCacheAge { get; set; }
    public int TranscoderMaximumThreads { get; set; }
    public int TranscoderTimeout { get; set; }
    public int HLSSegmentTimeInSeconds { get; set; }
    public string HLSSegmentFileTemplate { get; set; }
    public string SubtitleDefaultEncoding { get; set; }
    public string SubtitleDefaultLanguage { get; set; }
    public ILogger Logger { get; set; }
    public bool SupportHardcodedSubs
    {
      get
      {
        return _supportHardcodedSubs;
      }
    }

    public static ReadOnlyDictionary<string, List<TranscodeContext>> RunningTranscodes
    {
      get
      {
        lock (_runningTranscodes)
        {
          return new ReadOnlyDictionary<string, List<TranscodeContext>>(_runningTranscodes);
        }
      }
    }
    private static Dictionary<string, List<TranscodeContext>> _runningTranscodes = new Dictionary<string, List<TranscodeContext>>();
    private static FFMpegEncoderHandler _ffMpegEncoderHandler;

    private FFMpegCommandline _ffMpegCommandline;
    private bool _supportHardcodedSubs = true;
    private bool _supportNvidiaHW = true;
    private bool _supportIntelHW = true;
    
    public MediaConverter()
    {
      InitSettings();
    }

    #region HW Acelleration

    public bool RegisterHardwareEncoder(EncoderHandler encoder, int maximumStreams, List<VideoCodec> supportedCodecs)
    {
      if(encoder == EncoderHandler.Software) 
        return false;
      else if(encoder == EncoderHandler.HardwareIntel && _supportIntelHW == false)
        return false;
      else if(encoder == EncoderHandler.HardwareNvidia && _supportNvidiaHW == false)
        return false;
      _ffMpegEncoderHandler.RegisterEncoder(encoder, maximumStreams, supportedCodecs);
      return true;
    }

    public void UnregisterHardwareEncoder(EncoderHandler encoder)
    {
      _ffMpegEncoderHandler.UnregisterEncoder(encoder);
    }

    #endregion

    #region Cache

    private void InitSettings()
    {
      if (_ffMpegCommandline != null)
      {
        //Already inited
        return;
      }

      TranscoderCachePath = TranscodingServicePlugin.TranscoderCachePath;
      TranscoderMaximumCacheSize = TranscodingServicePlugin.TranscoderMaximumCacheSizeInGB; //GB
      TranscoderMaximumCacheAge = TranscodingServicePlugin.TranscodeMaximumCacheAgeInDays; //Days
      TranscoderMaximumThreads = TranscodingServicePlugin.TranscoderMaximumThreads;
      TranscoderTimeout = TranscodingServicePlugin.TranscoderTimeout;
      HLSSegmentTimeInSeconds = TranscodingServicePlugin.HLSSegmentTimeInSeconds;
      HLSSegmentFileTemplate = TranscodingServicePlugin.HLSSegmentFileTemplate;
      SubtitleDefaultLanguage = TranscodingServicePlugin.SubtitleDefaultLanguage;
      SubtitleDefaultEncoding = TranscodingServicePlugin.SubtitleDefaultEncoding;
      TranscoderBinPath = ServiceRegistration.Get<IFFMpegLib>().FFMpegBinaryPath;
      string result;
      using (Process process = new Process { StartInfo = new ProcessStartInfo(TranscoderBinPath, "") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true } })
      {
        process.Start();
        using (process.StandardError)
        {
          result = process.StandardError.ReadToEnd();
        }
        if (!process.HasExited)
          process.Close();
      }

      if (result.IndexOf("--enable-libass") == -1)
      {
        if(Logger != null) Logger.Warn("MediaConverter: FFMPEG is not compiled with libass support, hardcoded subtitles will not work.");
        _supportHardcodedSubs = false;
      }
      if (result.IndexOf("--enable-nvenc") == -1)
      {
        if (Logger != null) Logger.Warn("MediaConverter: FFMPEG is not compiled with nvenc support, Nvidia hardware acceleration will not work.");
        _supportNvidiaHW = false;
      }
      if (result.IndexOf("--enable-libmfx") == -1)
      {
        if (Logger != null) Logger.Warn("MediaConverter: FFMPEG is not compiled with libmfx support, Intel hardware acceleration will not work.");
        _supportIntelHW = false;
      }

      if (TranscodingServicePlugin.IntelHWAccelerationAllowed && _supportIntelHW)
      {
        if (RegisterHardwareEncoder(EncoderHandler.HardwareIntel, TranscodingServicePlugin.IntelHWMaximumStreams, new List<VideoCodec>(TranscodingServicePlugin.IntelHWSupportedCodecs)) == false)
        {
          Logger.Warn("MediaConverter: Failed to register Intel hardware acceleration");
        }
      }
      if (TranscodingServicePlugin.NvidiaHWAccelerationAllowed && _supportNvidiaHW)
      {
        if (RegisterHardwareEncoder(EncoderHandler.HardwareNvidia, TranscodingServicePlugin.NvidiaHWMaximumStreams, new List<VideoCodec>(TranscodingServicePlugin.NvidiaHWSupportedCodecs)) == false)
        {
          Logger.Warn("MediaConverter: Failed to register Nvidia hardware acceleration");
        }
      }

      _ffMpegCommandline = new FFMpegCommandline(this);
      _ffMpegEncoderHandler = new FFMpegEncoderHandler();
    }

    private void TouchFile(string filePath)
    {
      if (File.Exists(filePath))
      {
        try
        {
          File.SetCreationTime(filePath, DateTime.Now);
        }
        catch { }
      }
    }

    private void TouchDirectory(string folderPath)
    {
      if (Directory.Exists(folderPath))
      {
        try
        {
          Directory.SetCreationTime(folderPath, DateTime.Now);
        }
        catch { }
      }
    }

    public void CleanUpTranscodeCache()
    {
      if (Directory.Exists(TranscoderCachePath) == true)
      {
        int maxTries = 10;
        SortedDictionary<DateTime, string> fileList = new SortedDictionary<DateTime, string>();
        long cacheSize = 0;
        List<string> dirObjects = new List<string>(Directory.GetFiles(TranscoderCachePath, "*.mp*"));
        dirObjects.AddRange(Directory.GetDirectories(TranscoderCachePath, "*_mptf"));
        foreach (string dirObject in dirObjects)
        {
          string[] tokens = dirObject.Split('.');
          if (tokens.Length >= 3)
          {
            if (Directory.Exists(dirObject) == true)
            {
              DirectoryInfo info;
              try
              {
                info = new DirectoryInfo(dirObject);
              }
              catch
              {
                continue;
              }
              FileInfo[] folderFiles = info.GetFiles();
              foreach (FileInfo folderFile in folderFiles)
              {
                if (folderFile.Length == 0)
                {
                  try
                  {
                    folderFile.Delete();
                  }
                  catch
                  {
                  }
                  continue;
                }
                cacheSize += folderFile.Length;
              }
              if (fileList.ContainsKey(info.CreationTime) == false)
              {
                fileList.Add(info.CreationTime, dirObject);
              }
              else
              {
                DateTime fileTime = info.CreationTime.AddMilliseconds(1);
                while (fileList.ContainsKey(fileTime) == true)
                {
                  fileTime = fileTime.AddMilliseconds(1);
                }
                fileList.Add(fileTime, dirObject);
              }
            }
            else
            {
              FileInfo info;
              try
              {
                info = new FileInfo(dirObject);
              }
              catch
              {
                continue;
              }
              if (info.Length == 0)
              {
                try
                {
                  File.Delete(dirObject);
                }
                catch
                {
                }
                continue;
              }
              cacheSize += info.Length;
              if (fileList.ContainsKey(info.CreationTime) == false)
              {
                fileList.Add(info.CreationTime, dirObject);
              }
              else
              {
                DateTime fileTime = info.CreationTime.AddMilliseconds(1);
                while (fileList.ContainsKey(fileTime) == true)
                {
                  fileTime = fileTime.AddMilliseconds(1);
                }
                fileList.Add(fileTime, dirObject);
              }
            }
          }
        }

        bool bDeleting = true;
        int tryCount = 0;
        while (fileList.Count > 0 && bDeleting && TranscoderMaximumCacheAge > 0 && tryCount < maxTries)
        {
          tryCount++;
          bDeleting = false;
          KeyValuePair<DateTime, string> dirObject = fileList.First();
          if ((DateTime.Now - dirObject.Key).TotalDays > TranscoderMaximumCacheAge)
          {
            bDeleting = true;
            fileList.Remove(dirObject.Key);
            if (Directory.Exists(dirObject.Value) == true)
            {
              try
              {
                Directory.Delete(dirObject.Value, true);
              }
              catch { }
            }
            else
            {
              try
              {
                File.Delete(dirObject.Value);
              }
              catch { }
            }
          }
        }

        tryCount = 0;
        while (fileList.Count > 0 && cacheSize > (TranscoderMaximumCacheSize * 1024 * 1024 * 1024) && TranscoderMaximumCacheSize > 0 && tryCount < maxTries)
        {
          tryCount++;
          KeyValuePair<DateTime, string> dirObject = fileList.First();
          if (Directory.Exists(dirObject.Value) == true)
          {
            DirectoryInfo info;
            try
            {
              info = new DirectoryInfo(dirObject.Value);
            }
            catch
            {
              fileList.Remove(dirObject.Key);
              continue;
            }
            FileInfo[] folderFiles = info.GetFiles();
            cacheSize = folderFiles.Aggregate(cacheSize, (current, folderFile) => current - folderFile.Length);
            fileList.Remove(dirObject.Key);
            try
            {
              info.Delete(true);
            }
            catch { }
          }
          else
          {
            FileInfo info;
            try
            {
              info = new FileInfo(dirObject.Value);
            }
            catch
            {
              fileList.Remove(dirObject.Key);
              continue;
            }
            cacheSize -= info.Length;
            fileList.Remove(dirObject.Key);
            try
            {
              info.Delete();
            }
            catch { }
          }
        }
      }
    }

    public bool IsFileInTranscodeCache(string transcodeId)
    {
      if (Checks.IsTranscodingRunning(transcodeId, ref _runningTranscodes) == false)
      {
        List<string> dirObjects = new List<string>(Directory.GetFiles(TranscoderCachePath, "*.mp*"));
        return dirObjects.Any(file => file.StartsWith(transcodeId + ".mp"));
      }
      return false;
    }

    #endregion

    #region Subtitles

    private SubtitleStream FindSubtitle(VideoTranscoding video)
    {
      SubtitleStream currentEmbeddedSub = null;
      SubtitleStream currentExternalSub = null;

      SubtitleStream defaultEmbeddedSub = null;
      SubtitleStream englishEmbeddedSub = null;
      List<SubtitleStream> subsEmbedded = new List<SubtitleStream>();
      List<SubtitleStream> langSubsEmbedded = new List<SubtitleStream>();
      List<SubtitleStream> allSubs = new List<SubtitleStream>(video.SourceSubtitles);
      if (video.SourceFile is ILocalFsResourceAccessor)
      {
        ILocalFsResourceAccessor lfsra = (ILocalFsResourceAccessor)video.SourceFile;
        allSubs.AddRange(FindExternalSubtitles(lfsra));
      }

      foreach (SubtitleStream sub in allSubs)
      {
        if (sub.IsEmbedded == false)
        {
          continue;
        }
        if (sub.Default == true)
        {
          defaultEmbeddedSub = sub;
        }
        else if (string.Compare(sub.Language, "EN", true, CultureInfo.InvariantCulture) == 0)
        {
          englishEmbeddedSub = sub;
        }
        if (string.IsNullOrEmpty(video.TargetSubtitleLanguages) == false)
        {
          string[] langs = video.TargetSubtitleLanguages.Split(',');
          foreach (string lang in langs)
          {
            if (string.IsNullOrEmpty(lang) == false && string.Compare(sub.Language, lang, true, CultureInfo.InvariantCulture) == 0)
            {
              langSubsEmbedded.Add(sub);
            }
          }
        }
        else
        {
          subsEmbedded.Add(sub);
        }
      }
      if (currentEmbeddedSub == null && langSubsEmbedded.Count > 0)
      {
        currentEmbeddedSub = langSubsEmbedded[0];
      }

      SubtitleStream defaultSub = null;
      SubtitleStream englishSub = null;
      List<SubtitleStream> subs = new List<SubtitleStream>();
      List<SubtitleStream> langSubs = new List<SubtitleStream>();
      foreach (SubtitleStream sub in allSubs)
      {
        if (sub.IsEmbedded == true)
        {
          continue;
        }
        if (sub.Default == true)
        {
          defaultSub = sub;
        }
        else if (string.Compare(sub.Language, "EN", true, CultureInfo.InvariantCulture) == 0)
        {
          englishSub = sub;
        }
        if (string.IsNullOrEmpty(video.TargetSubtitleLanguages) == false)
        {
          string[] langs = video.TargetSubtitleLanguages.Split(',');
          foreach (string lang in langs)
          {
            if (string.IsNullOrEmpty(lang) == false && string.Compare(sub.Language, lang, true, CultureInfo.InvariantCulture) == 0)
            {
              langSubs.Add(sub);
            }
          }
        }
        else
        {
          subs.Add(sub);
        }
      }
      if (currentExternalSub == null && langSubs.Count > 0)
      {
        currentExternalSub = langSubs[0];
      }

      //Best language subtitle
      if (currentExternalSub != null)
      {
        return currentExternalSub;
      }
      if (currentEmbeddedSub != null)
      {
        return currentEmbeddedSub;
      }

      //Best default subtitle
      if (currentExternalSub == null && defaultSub != null)
      {
        currentExternalSub = defaultSub;
      }
      if (currentEmbeddedSub == null && defaultEmbeddedSub != null)
      {
        currentEmbeddedSub = defaultEmbeddedSub;
      }
      if (currentExternalSub != null)
      {
        return currentExternalSub;
      }
      if (currentEmbeddedSub != null)
      {
        return currentEmbeddedSub;
      }

      //Best english
      if (currentExternalSub == null && englishSub != null)
      {
        currentExternalSub = englishSub;
      }
      if (currentEmbeddedSub == null && englishEmbeddedSub != null)
      {
        currentEmbeddedSub = englishEmbeddedSub;
      }
      if (currentExternalSub != null)
      {
        return currentExternalSub;
      }
      if (currentEmbeddedSub != null)
      {
        return currentEmbeddedSub;
      }

      //Best remaining subtitle
      if (currentExternalSub == null && subs.Count > 0)
      {
        currentExternalSub = subs[0];
      }
      if (currentEmbeddedSub == null && subsEmbedded.Count > 0)
      {
        currentEmbeddedSub = subsEmbedded[0];
      }
      if (currentExternalSub != null)
      {
        return currentExternalSub;
      }
      if (currentEmbeddedSub != null)
      {
        return currentEmbeddedSub;
      }
      return null;
    }

    private List<SubtitleStream> FindExternalSubtitles(ILocalFsResourceAccessor lfsra)
    {
      List<SubtitleStream> externalSubtitles = new List<SubtitleStream>();
      if (lfsra.Exists)
      {
        // Impersonation
        using (ServiceRegistration.Get<IImpersonationService>().CheckImpersonationFor(lfsra.CanonicalLocalResourcePath))
        {
          string[] files = Directory.GetFiles(Path.GetDirectoryName(lfsra.LocalFileSystemPath), Path.GetFileNameWithoutExtension(lfsra.LocalFileSystemPath) + "*.*");
          foreach (string file in files)
          {
            SubtitleStream sub = new SubtitleStream();
            sub.StreamIndex = -1;
            sub.Codec = SubtitleCodec.Unknown;
            if (string.Compare(Path.GetExtension(file), ".srt", true, CultureInfo.InvariantCulture) == 0)
            {
              sub.Codec = SubtitleCodec.Srt;
            }
            else if (string.Compare(Path.GetExtension(file), ".smi", true, CultureInfo.InvariantCulture) == 0)
            {
              sub.Codec = SubtitleCodec.Smi;
            }
            else if (string.Compare(Path.GetExtension(file), ".ass", true, CultureInfo.InvariantCulture) == 0)
            {
              sub.Codec = SubtitleCodec.Ass;
            }
            else if (string.Compare(Path.GetExtension(file), ".ssa", true, CultureInfo.InvariantCulture) == 0)
            {
              sub.Codec = SubtitleCodec.Ssa;
            }
            else if (string.Compare(Path.GetExtension(file), ".sub", true, CultureInfo.InvariantCulture) == 0)
            {
              string subContent = File.ReadAllText(file);
              if (subContent.Contains("[INFORMATION]")) sub.Codec = SubtitleCodec.SubView;
              else if (subContent.Contains("}{")) sub.Codec = SubtitleCodec.MicroDvd;
            }
            if (sub.Codec != SubtitleCodec.Unknown)
            {
              sub.Source = file;
              sub.Language = SubtitleAnalyzer.GetLanguage(file, SubtitleDefaultEncoding, SubtitleDefaultLanguage);
              externalSubtitles.Add(sub);
            }
          }
        }
      }
      return externalSubtitles;
    }

    public BufferedStream GetSubtitleStream(VideoTranscoding video)
    {
      Subtitle sub = GetSubtitle(video);
      if (sub == null || sub.SourceFile == null)
      {
        return null;
      }
      if (Checks.IsTranscodingRunning(video.TranscodeId, ref _runningTranscodes) == false)
      {
        TouchFile(sub.SourceFile);
      }
      return GetReadyFileBuffer(sub.SourceFile);
    }

    private bool SubtitleIsUnicode(string encoding)
    {
      if (string.IsNullOrEmpty(encoding))
      {
        return false;
      }
      if (encoding.ToUpperInvariant().StartsWith("UTF-") || encoding.ToUpperInvariant().StartsWith("UNICODE"))
      {
        return true;
      }
      return false;
    }

    private Subtitle GetSubtitle(VideoTranscoding video)
    {
      SubtitleStream sourceSubtitle = FindSubtitle(video);
      if (sourceSubtitle == null) return null;
      if (video.TargetSubtitleSupport == SubtitleSupport.None) return null;

      Subtitle res = new Subtitle
      {
        Codec = sourceSubtitle.Codec,
        Language = sourceSubtitle.Language,
        SourceFile = sourceSubtitle.Source,
        CharacterEncoding = SubtitleAnalyzer.GetEncoding(sourceSubtitle.Source, sourceSubtitle.Language, SubtitleDefaultEncoding)
      };

      // SourceSubtitle == TargetSubtitleCodec -> just return
      if (video.TargetSubtitleCodec != SubtitleCodec.Unknown && video.TargetSubtitleCodec == sourceSubtitle.Codec)
      {
        return res;
      }

      // create a file name for the output file which contains the subtitle informations
      string transcodingFile = Path.Combine(TranscoderCachePath, video.TranscodeId);
      if (sourceSubtitle != null && string.IsNullOrEmpty(sourceSubtitle.Language) == false)
      {
        transcodingFile += "." + sourceSubtitle.Language;
      }
      transcodingFile += ".mpts";
      SubtitleCodec targetCodec = video.TargetSubtitleCodec;
      if (targetCodec == SubtitleCodec.Unknown)
      {
        targetCodec = sourceSubtitle.Codec;
      }

      // the file already exists in the cache -> just return
      if (File.Exists(transcodingFile))
      {
        if (Checks.IsTranscodingRunning(video.TranscodeId, ref _runningTranscodes) == false)
        {
          TouchFile(transcodingFile);
        }
        res.Codec = targetCodec;
        res.SourceFile = transcodingFile;
        if (SubtitleIsUnicode(res.CharacterEncoding) == false)
        {
          res.CharacterEncoding = "UTF-8";
        }
        return res;
      }

      // subtitle is embedded in the source file
      if (sourceSubtitle.IsEmbedded)
      {
        _ffMpegCommandline.ExtractSubtitleFile(video, sourceSubtitle, res.CharacterEncoding, transcodingFile);
        if (File.Exists(transcodingFile))
        {
          res.Codec = targetCodec;
          res.CharacterEncoding = "UTF-8";
          res.SourceFile = transcodingFile;
          return res;
        }
        return null;
      }

      // Burn external subtitle into video
      if (res.SourceFile == null)
      {
        return null;
      }

      FFMpegTranscodeData data = new FFMpegTranscodeData(TranscoderCachePath) { TranscodeId = video.TranscodeId + "_sub" };
      if (string.IsNullOrEmpty(video.TranscoderBinPath) == false)
      {
        data.TranscoderBinPath = video.TranscoderBinPath;
      }
      if (string.IsNullOrEmpty(video.TranscoderArguments) == false)
      {
        // TODO: not sure if this is working
        data.TranscoderArguments = video.TranscoderArguments;
        LocalFsResourceProvider localFsResourceProvider = new LocalFsResourceProvider();
        IResourceAccessor resourceAccessor = new LocalFsResourceAccessor(localFsResourceProvider, res.SourceFile);
        data.InputResourceAccessor = resourceAccessor;
      }
      else
      {
        if (SubtitleIsUnicode(res.CharacterEncoding) == false)
        {
          if (string.IsNullOrEmpty(res.CharacterEncoding) == false)
          {
            string newFile = transcodingFile.Replace(".mpts", ".utf8.mpts");
            File.WriteAllText(newFile, File.ReadAllText(res.SourceFile, Encoding.GetEncoding(res.CharacterEncoding)), Encoding.UTF8);
            res.CharacterEncoding = "UTF-8";
            res.SourceFile = newFile;
            if (Logger != null) Logger.Debug("MediaConverter: Converted subtitle file '{0}' to UTF-8 for transcode '{1}'", sourceSubtitle.Source, data.TranscodeId);
          }
        }

        // TODO: not sure if this is working
        LocalFsResourceProvider localFsResourceProvider = new LocalFsResourceProvider();
        IResourceAccessor resourceAccessor = new LocalFsResourceAccessor(localFsResourceProvider, res.SourceFile);
        _ffMpegCommandline.InitTranscodingParameters(resourceAccessor, ref data);
        data.InputArguments.Add(string.Format("-f {0}", FFMpegGetSubtitleContainer.GetSubtitleContainer(sourceSubtitle.Codec)));

        res.Codec = targetCodec;
        string subtitleEncoder = "copy";
        if (res.Codec == SubtitleCodec.Unknown)
        {
          res.Codec = SubtitleCodec.Ass;
        }
        if (sourceSubtitle.Codec != res.Codec)
        {
          subtitleEncoder = FFMpegGetSubtitleContainer.GetSubtitleContainer(res.Codec);
        }
        string subtitleFormat = FFMpegGetSubtitleContainer.GetSubtitleContainer(res.Codec);
        data.OutputArguments.Add("-vn");
        data.OutputArguments.Add("-an");
        data.OutputArguments.Add(string.Format("-c:s {0}", subtitleEncoder));
        data.OutputArguments.Add(string.Format("-f {0}", subtitleFormat));
      }
      data.OutputFilePath = transcodingFile;

      if (Logger != null) Logger.Debug("MediaConverter: Invoking transcoder to transcode subtitle file '{0}' for transcode '{1}'", res.SourceFile, data.TranscodeId);
      FFMpegFileProcessor.FileProcessor(ref data, TranscoderTimeout);
      if (File.Exists(transcodingFile) == true)
      {
        res.SourceFile = transcodingFile;
        return res;
      }
      return null;
    }

    #endregion

    #region Transcoding

    private bool AssignExistingTranscodeContext(string transcodeId, ref TranscodeContext context)
    {
      lock (_runningTranscodes)
      {
        if (_runningTranscodes.ContainsKey(transcodeId))
        {
          List<TranscodeContext> runningContexts = _runningTranscodes[transcodeId];
          if (runningContexts != null)
          {
            for (int contextNo = 0; contextNo < runningContexts.Count; contextNo++)
            {
              if (runningContexts[contextNo].Partial == false)
              {
                context = runningContexts[contextNo];
                return true;
              }
            }
          }
        }
      }
      return false;
    }

    public TranscodeContext GetMediaStream(BaseTranscoding transcodingInfo, double timeStart, double timeDuration, bool waitForBuffer)
    {
      InitSettings();
      if (((ILocalFsResourceAccessor)transcodingInfo.SourceFile).Exists == false)
      {
        if (Logger != null) Logger.Error("MediaConverter: File '{0}' does not exist for transcode '{1}'", transcodingInfo.SourceFile, transcodingInfo.TranscodeId);
        return null;
      }
      else if (transcodingInfo is ImageTranscoding)
      {
        return TranscodeImageFile(transcodingInfo as ImageTranscoding, waitForBuffer);
      }
      else if (transcodingInfo is AudioTranscoding)
      {
        return TranscodeAudioFile(transcodingInfo as AudioTranscoding, timeStart, timeDuration, waitForBuffer);
      }
      else if (transcodingInfo is VideoTranscoding)
      {
        return TranscodeVideoFile(transcodingInfo as VideoTranscoding, timeStart, timeDuration, waitForBuffer);
      }
      if (Logger != null) Logger.Error("MediaConverter: Transcoding info is not valid for transcode '{0}'", transcodingInfo.TranscodeId);
      return null;
    }

    private TranscodeContext TranscodeVideoFile(VideoTranscoding video, double timeStart, double timeDuration, bool waitForBuffer)
    {
      TranscodeContext context = new TranscodeContext { Failed = false };
      context.TargetDuration = video.SourceDuration;
      if (timeStart == 0)
      {
        timeDuration = 0;
        context.Partial = false;
      }
      else
      {
        context.Partial = true;
      }
      if(video.TargetVideoContainer == VideoContainer.Unknown)
      {
        video.TargetVideoContainer = video.SourceVideoContainer;
      }
      string transcodingFile = Path.Combine(TranscoderCachePath, video.TranscodeId);
      long partId = DateTime.Now.Ticks - DateTime.Today.Ticks;
      string partialTranscodingFile = Path.Combine(TranscoderCachePath, partId + "." + video.TranscodeId + ".mptv");
      transcodingFile += ".A" + video.SourceAudioStreamIndex;
      bool embeddedSupported = false;
      SubtitleCodec embeddedSubCodec = SubtitleCodec.Unknown;
      if (video.TargetSubtitleSupport == SubtitleSupport.Embedded)
      {
        if (video.TargetVideoContainer == VideoContainer.Matroska)
        {
          embeddedSupported = true;
          embeddedSubCodec = SubtitleCodec.Ass;
          video.TargetSubtitleCodec = SubtitleCodec.Ass;
        }
        else if (video.TargetVideoContainer == VideoContainer.Mp4)
        {
          embeddedSupported = true;
          embeddedSubCodec = SubtitleCodec.MovTxt;
          video.TargetSubtitleCodec = SubtitleCodec.Ass;
        }
        //else if (video.TargetVideoContainer == VideoContainer.Mpeg2Ts)
        //{
        //  embeddedSupported = true;
        //  embeddedSubCodec = SubtitleCodec.DvbSub;
        //  video.TargetSubtitleCodec = SubtitleCodec.Ass;
        //}
      }

      Subtitle currentSub = GetSubtitle(video);
      if (currentSub != null) video.SourceSubtitleAvailable = true;
      else video.SourceSubtitleAvailable = false;
      if (currentSub != null && _supportHardcodedSubs == true && (embeddedSupported || video.TargetSubtitleSupport == SubtitleSupport.HardCoded))
      {
        if (string.IsNullOrEmpty(currentSub.Language) == false)
        {
          transcodingFile += ".S" + currentSub.Language;
        }
      }
      transcodingFile += ".mptv";

      //Use non-partial transcode if possible
      if (File.Exists(transcodingFile))
      {
        TranscodeContext existingContext = null;
        if (AssignExistingTranscodeContext(video.TranscodeId, ref existingContext) == true)
        {
          TouchFile(transcodingFile);
          existingContext.TargetFile = transcodingFile;
          if (existingContext.TranscodedStream == null)
            existingContext.AssignStream(GetReadyFileBuffer(transcodingFile));
          if (existingContext.CurrentDuration.TotalSeconds == 0)
          {
            double bitrate = 0;
            if (video.TargetVideoBitrate > 0 && video.TargetAudioBitrate > 0)
            {
              bitrate = video.TargetVideoBitrate + video.TargetAudioBitrate;
            }
            else if (video.SourceVideoBitrate > 0 && video.SourceAudioBitrate > 0)
            {
              bitrate = video.SourceVideoBitrate + video.SourceAudioBitrate;
            }
            bitrate *= 1024; //Bitrate in bits/s
            if (bitrate > 0)
            {
              long startByte = Convert.ToInt64((bitrate * timeStart) / 8.0);
              if (existingContext.TranscodedStream.Length > startByte)
              {
                return existingContext;
              }
            }
          }
          else
          {
            if (existingContext.CurrentDuration.TotalSeconds > timeStart)
            {
              return existingContext;
            }
          }
        }
      }
      if (video.TargetVideoContainer == VideoContainer.Hls)
      {
        string pathName = Path.Combine(TranscoderCachePath, Path.GetFileNameWithoutExtension(transcodingFile).Replace(".", "_") + "_mptf");
        string playlist = Path.Combine(pathName, "playlist.m3u8");

        //Use exisitng context if possible
        if (File.Exists(playlist) == true)
        {
          TranscodeContext existingContext = null;
          if (AssignExistingTranscodeContext(video.TranscodeId, ref existingContext) == true)
          {
            TouchDirectory(pathName);
            existingContext.TargetFile = playlist;
            existingContext.SegmentDir = pathName;
            if (existingContext.TranscodedStream == null)
              existingContext.AssignStream(GetReadyFileBuffer(playlist));
            existingContext.HlsBaseUrl = video.HlsBaseUrl;
            return existingContext;
          }
        }
      }

      FFMpegTranscodeData data = new FFMpegTranscodeData(TranscoderCachePath) { TranscodeId = video.TranscodeId };
      if (string.IsNullOrEmpty(video.TranscoderBinPath) == false)
      {
        data.TranscoderBinPath = video.TranscoderBinPath;
      }
      if (string.IsNullOrEmpty(video.TranscoderArguments) == false)
      {
        data.TranscoderArguments = video.TranscoderArguments;
        data.InputResourceAccessor = video.SourceFile;
        if (currentSub != null && string.IsNullOrEmpty(currentSub.SourceFile) == false)
        {
          data.InputSubtitleFilePath = currentSub.SourceFile;
        }
      }
      else
      {
        data.Encoder = _ffMpegEncoderHandler.StartEncoding(video.TranscodeId, video.TargetVideoCodec);
        _ffMpegCommandline.InitTranscodingParameters(video.SourceFile, ref data);

        bool useX26XLib = video.TargetVideoCodec == VideoCodec.H264 || video.TargetVideoCodec == VideoCodec.H265;
        _ffMpegCommandline.AddTranscodingThreadsParameters(!useX26XLib, ref data);

        _ffMpegCommandline.AddTimeParameters(timeStart, timeDuration, video.SourceDuration.TotalSeconds, ref data);

        FFMpegEncoderConfig encoderConfig = _ffMpegEncoderHandler.GetEncoderConfig(data.Encoder);
        _ffMpegCommandline.AddVideoParameters(video, data.TranscodeId, currentSub, encoderConfig, ref data);

        _ffMpegCommandline.AddTargetVideoFormatAndOutputFileParameters(video, transcodingFile, ref data);
        _ffMpegCommandline.AddVideoAudioParameters(video, ref data);
        if (currentSub != null && embeddedSupported)
        {
          _ffMpegCommandline.AddSubtitleEmbeddingParameters(currentSub, embeddedSubCodec, ref data);
        }
        else
        {
          embeddedSupported = false;
          data.OutputArguments.Add("-sn");
        }
        _ffMpegCommandline.AddStreamMapParameters(video.SourceVideoStreamIndex, video.SourceAudioStreamIndex, embeddedSupported, ref data);
      }
      if (context.Partial)
      {
        data.OutputFilePath = partialTranscodingFile;
        context.TargetFile = partialTranscodingFile;
      }
      else
      {
        data.OutputFilePath = transcodingFile;
        context.TargetFile = transcodingFile;
      }

      if (Logger != null) Logger.Info("MediaConverter: Invoking transcoder to transcode video file '{0}' for transcode '{1}' with arguments '{2}'", video.SourceFile, video.TranscodeId, String.Join(", ", data.OutputArguments.ToArray()));
      context.Start();
      context.AssignStream(ExecuteTranscodingProcess(data, context, waitForBuffer));
      return context;
    }

    private TranscodeContext TranscodeAudioFile(AudioTranscoding audio, double timeStart, double timeDuration, bool waitForBuffer)
    {
      TranscodeContext context = new TranscodeContext { Failed = false };
      context.TargetDuration = audio.SourceDuration;
      if (timeStart == 0)
      {
        timeDuration = 0;
        context.Partial = false;
      }
      else
      {
        context.Partial = true;
      }
      if (audio.TargetAudioContainer == AudioContainer.Unknown)
      {
        audio.TargetAudioContainer = audio.SourceAudioContainer;
      }
      string transcodingFile = Path.Combine(TranscoderCachePath, audio.TranscodeId + ".mpta");
      long partId = DateTime.Now.Ticks - DateTime.Today.Ticks;
      string partialTranscodingFile = Path.Combine(TranscoderCachePath, partId + "." + audio.TranscodeId + ".mpta");

      //Use non-partial context if possible
      if (File.Exists(transcodingFile) == true)
      {
        TranscodeContext existingContext = null;
        if (AssignExistingTranscodeContext(audio.TranscodeId, ref existingContext) == true)
        {
          TouchFile(transcodingFile);
          existingContext.TargetFile = transcodingFile;
          if (existingContext.TranscodedStream == null)
            existingContext.AssignStream(GetReadyFileBuffer(transcodingFile));

          if (existingContext.CurrentDuration.TotalSeconds == 0)
          {
            double bitrate = 0;
            if (audio.TargetAudioBitrate > 0)
            {
              bitrate = audio.TargetAudioBitrate;
            }
            else if (audio.SourceAudioBitrate > 0)
            {
              bitrate = audio.SourceAudioBitrate;
            }
            bitrate *= 1024; //Bitrate in bits/s
            if (bitrate > 0)
            {
              long startByte = Convert.ToInt64((bitrate * timeStart) / 8.0);
              if (existingContext.TranscodedStream.Length > startByte)
              {
                return existingContext;
              }
            }
          }
          else
          {
            if (existingContext.CurrentDuration.TotalSeconds > timeStart)
            {
              return existingContext;
            }
          }
        }
      }

      FFMpegTranscodeData data = new FFMpegTranscodeData(TranscoderCachePath) { TranscodeId = audio.TranscodeId };
      if (string.IsNullOrEmpty(audio.TranscoderBinPath) == false)
      {
        data.TranscoderBinPath = audio.TranscoderBinPath;
      }
      if (string.IsNullOrEmpty(audio.TranscoderArguments) == false)
      {
        data.TranscoderArguments = audio.TranscoderArguments;
        data.InputResourceAccessor = audio.SourceFile;
      }
      else
      {
        _ffMpegCommandline.InitTranscodingParameters(audio.SourceFile, ref data);
        _ffMpegCommandline.AddTranscodingThreadsParameters(true, ref data);

        _ffMpegCommandline.AddTimeParameters(timeStart, timeDuration, audio.SourceDuration.TotalSeconds, ref data);

        _ffMpegCommandline.AddAudioParameters(audio, ref data);

        data.OutputArguments.Add(string.Format("-f {0}", FFMpegGetAudioContainer.GetAudioContainer(audio.TargetAudioContainer)));
        data.OutputArguments.Add("-vn");
      }
      if (context.Partial)
      {
        data.OutputFilePath = partialTranscodingFile;
        context.TargetFile = partialTranscodingFile;
      }
      else
      {
        data.OutputFilePath = transcodingFile;
        context.TargetFile = transcodingFile;
      }
      if (Logger != null) Logger.Debug("MediaConverter: Invoking transcoder to transcode audio file '{0}' for transcode '{1}'", audio.SourceFile, audio.TranscodeId);
      context.Start();
      context.AssignStream(ExecuteTranscodingProcess(data, context, waitForBuffer));
      return context;
    }

    private TranscodeContext TranscodeImageFile(ImageTranscoding image, bool waitForBuffer)
    {
      TranscodeContext context = new TranscodeContext { Failed = false };
      string transcodingFile = Path.Combine(TranscoderCachePath, image.TranscodeId + ".mpti");

      //Use exisitng contaxt if possible
      if (File.Exists(transcodingFile) == true)
      {
        TranscodeContext existingContext = null;
        if (AssignExistingTranscodeContext(image.TranscodeId, ref existingContext) == true)
        {
          TouchFile(transcodingFile);
          existingContext.TargetFile = transcodingFile;
          if (existingContext.TranscodedStream == null)
            existingContext.AssignStream(GetReadyFileBuffer(transcodingFile));
          return existingContext;
        }
      }

      FFMpegTranscodeData data = new FFMpegTranscodeData(TranscoderCachePath) { TranscodeId = image.TranscodeId };
      if (string.IsNullOrEmpty(image.TranscoderBinPath) == false)
      {
        data.TranscoderBinPath = image.TranscoderBinPath;
      }
      if (string.IsNullOrEmpty(image.TranscoderArguments) == false)
      {
        data.TranscoderArguments = image.TranscoderArguments;
        data.InputResourceAccessor = image.SourceFile;
      }
      else
      {
        _ffMpegCommandline.InitTranscodingParameters(image.SourceFile, ref data);
        _ffMpegCommandline.AddTranscodingThreadsParameters(true, ref data);

        _ffMpegCommandline.AddImageParameters(image, ref data);

        data.OutputArguments.Add("-f image2");
      }
      data.OutputFilePath = transcodingFile;
      context.TargetFile = transcodingFile;
      context.Partial = false;

      if (Logger != null) Logger.Debug("MediaConverter: Invoking transcoder to transcode image file '{0}' for transcode '{1}'", image.SourceFile, image.TranscodeId);
      context.Start();
      context.AssignStream(ExecuteTranscodingProcess(data, context, waitForBuffer));
      return context;
    }

    public BufferedStream GetReadyFileBuffer(ILocalFsResourceAccessor lfsra)
    {
      int iTry = 60;
      while (iTry > 0)
      {
        if (lfsra.Exists)
        {
          if (lfsra.Size > 0)
          {
            if (Logger != null) Logger.Debug(string.Format("MediaConverter: Serving ready file '{0}'", lfsra.LocalFileSystemPath));
            // Impersonation
            using (ServiceRegistration.Get<IImpersonationService>().CheckImpersonationFor(lfsra.CanonicalLocalResourcePath))
            {
              BufferedStream stream = new BufferedStream(new FileStream(lfsra.LocalFileSystemPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
              return stream;
            }
          }
        }
        iTry--;
        Thread.Sleep(500);
      }
      if (Logger != null) Logger.Error("MediaConverter: Timed out waiting for ready file '{0}'", lfsra.LocalFileSystemPath);
      return null;
    }

    public BufferedStream GetReadyFileBuffer(string filePath)
    {
      int iTry = 60;
      while (iTry > 0)
      {
        if (File.Exists(filePath) == true)
        {
          long length = 0;
          try
          {
            length = new FileInfo(filePath).Length;
          }
          catch { }
          if (length > 0)
          {
            if (Logger != null) Logger.Debug(string.Format("MediaConverter: Serving ready file '{0}'", filePath));
            BufferedStream stream = new BufferedStream(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            return stream;
          }
        }
        iTry--;
        Thread.Sleep(500);
      }
      if (Logger != null) Logger.Error("MediaConverter: Timed out waiting for ready file '{0}'", filePath);
      return null;
    }

    #endregion

    #region Transcoder

    private Stream GetTranscodedFileBuffer(FFMpegTranscodeData data, TranscodeContext context)
    {
      string filePath = "";
      if (data.SegmentPlaylist != null)
      {
        filePath = Path.Combine(data.WorkPath, data.SegmentPlaylist);
      }
      else
      {
        filePath = Path.Combine(data.WorkPath, data.OutputFilePath);
      }

      int iTry = 60;
      while (iTry > 0 && context.Failed == false && context.Aborted == false)
      {
        if (File.Exists(filePath))
        {
          long length = 0;
          try
          {
            length = new FileInfo(filePath).Length;
          }
          catch { }
          if (length > 0)
          {
            if (Logger != null) Logger.Debug(string.Format("MediaConverter: Serving transcoded file '{0}'", filePath));
            Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            return stream;
          }
        }
        iTry--;
        Thread.Sleep(500);
      }
      if (Logger != null) Logger.Error("MediaConverter: Timed out waiting for transcoded file '{0}'", filePath);
      return null;
    }

    private Stream ExecuteTranscodingProcess(FFMpegTranscodeData data, TranscodeContext context, bool waitForBuffer)
    {
      if (context.Partial == true || Checks.IsTranscodingRunning(data.TranscodeId, ref _runningTranscodes) == false)
      {
        try
        {
          lock (_runningTranscodes)
          {
            if (_runningTranscodes.ContainsKey(data.TranscodeId) == false)
            {
              _runningTranscodes.Add(data.TranscodeId, new List<TranscodeContext>());
            }
            _runningTranscodes[data.TranscodeId].Add(context);
          }
          string name = "MP Transcode - " + data.TranscodeId;
          if(context.Partial)
          {
            name += " - Partial: " + Thread.CurrentThread.ManagedThreadId;
          }
          Thread transcodeThread = new Thread(TranscodeProcessor)
          {
            IsBackground = true,
            Name = name,
            Priority = ThreadPriority.Normal
          };
          FFMpegTranscodeThreadData threadData = new FFMpegTranscodeThreadData()
          {
            TranscodeData = data,
            Context = context
          };
          transcodeThread.Start(threadData);
        }
        catch
        {
          lock (RunningTranscodes)
          {
            _runningTranscodes.Remove(data.TranscodeId);
          }
          _ffMpegEncoderHandler.EndEncoding(data.Encoder, data.TranscodeId);
          context.Running = false;
          context.Failed = true;
          throw;
        }
      }

      if (waitForBuffer == false) return null;
      return GetTranscodedFileBuffer(data, context);
    }

    private void TranscodeProcessor(object args)
    {
      FFMpegTranscodeThreadData data = (FFMpegTranscodeThreadData)args;

      data.Context.TargetFile = Path.Combine(data.TranscodeData.WorkPath, data.TranscodeData.SegmentPlaylist != null ? data.TranscodeData.SegmentPlaylist : data.TranscodeData.OutputFilePath);
      data.Context.SegmentDir = null;
      if (data.TranscodeData.SegmentPlaylist != null)
      {
        data.Context.SegmentDir = data.TranscodeData.WorkPath;
      }

      if (Logger != null) Logger.Debug("MediaConverter: Transcoder '{0}' invoked with command line arguments '{1}'", data.TranscodeData.TranscoderBinPath, data.TranscodeData.TranscoderArguments);
      //Task<ProcessExecutionResult> executionResult = ServiceRegistration.Get<IFFMpegLib>().FFMpegExecuteWithResourceAccessAsync((ILocalFsResourceAccessor)data.Data.InputResourceAccessor, data.Data.TranscoderArguments, ProcessPriorityClass.Normal, ProcessUtils.INFINITE);

      ProcessStartInfo startInfo = new ProcessStartInfo()
      {
        FileName = ServiceRegistration.Get<IFFMpegLib>().FFMpegBinaryPath,
        Arguments = data.TranscodeData.TranscoderArguments,
        WorkingDirectory = data.TranscodeData.WorkPath,
        UseShellExecute = false,
        CreateNoWindow = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true
      };

      int iExitCode = -1;
      using (ServiceRegistration.Get<IImpersonationService>().CheckImpersonationFor(((ILocalFsResourceAccessor)data.TranscodeData.InputResourceAccessor).CanonicalLocalResourcePath))
      {
        using (Process ffmpeg = new Process { StartInfo = startInfo })
        {
          ffmpeg.OutputDataReceived += data.Context.OutputDataReceived;
          ffmpeg.ErrorDataReceived += data.Context.ErrorDataReceived;
          ffmpeg.Start();
          ffmpeg.BeginErrorReadLine();
          ffmpeg.BeginOutputReadLine();

          data.Context.Running = true;
          data.Context.Failed = false;
          //while (executionResult.Status == TaskStatus.Running)
          while (ffmpeg.HasExited == false)
          {
            if (data.Context.Running == false)
            {
              // TODO: Implement process abort
              data.Context.Aborted = true;
              ffmpeg.Kill();
              break;
            }
            Thread.Sleep(5);
          }
          ffmpeg.WaitForExit();
          iExitCode = ffmpeg.ExitCode;
          //iExitCode = executionResult.Result.ExitCode;
          lock (_runningTranscodes)
          {
            _runningTranscodes[data.TranscodeData.TranscodeId].Remove(data.Context);
            if (_runningTranscodes[data.TranscodeData.TranscodeId].Count == 0)
              _runningTranscodes.Remove(data.TranscodeData.TranscodeId);
          }
          _ffMpegEncoderHandler.EndEncoding(data.TranscodeData.Encoder, data.TranscodeData.TranscodeId);
          ffmpeg.Close();
        }
      }
      if (iExitCode > 0)
      {
        data.Context.Failed = true;
      }
      data.Context.Running = false;

      string filePath = data.Context.TargetFile;
      bool isFolder = false;
      if (filePath.EndsWith(".m3u8") == true)
      {
        filePath = data.Context.SegmentDir;
        isFolder = true;
      }
      if (iExitCode > 0 || data.Context.Aborted == true)
      {
        if (iExitCode > 0)
        {
          if (Logger != null) Logger.Debug("MediaConverter: Transcoder command failed with error {1} for file '{0}'", data.TranscodeData.OutputFilePath, iExitCode);
        }
        if (data.Context.Aborted == true)
        {
          data.Context.Stop();
          if (Logger != null) Logger.Debug("MediaConverter: Transcoder command aborted for file '{0}'", data.TranscodeData.OutputFilePath);
        }
        data.Context.DeleteFiles();
      }
      else
      {
        if (isFolder == false)
        {
          TouchFile(filePath);
        }
        else
        {
          TouchDirectory(filePath);
        }
      }
    }

    #endregion
  }
}
