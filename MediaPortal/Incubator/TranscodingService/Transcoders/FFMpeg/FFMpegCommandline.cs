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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.ResourceAccess;
using MediaPortal.Plugins.Transcoding.Service.Transcoders.Base;
using MediaPortal.Plugins.Transcoding.Service.Transcoders.Base.Metadata;
using MediaPortal.Plugins.Transcoding.Service.Transcoders.FFMpeg.Converters;

namespace MediaPortal.Plugins.Transcoding.Service.Transcoders.FFMpeg
{
  internal class FFMpegCommandline : Metadata
  {
    private int _transcoderMaximumThreads;
    private int _transcoderTimeout;
    private string _transcoderCachePath;
    private int _hlsSegmentTimeInSeconds;
    private string _hlsSegmentFileTemplate;
    private bool _supportHardcodedSubs;
    private readonly Dictionary<string, string> _filerPathEncoding = new Dictionary<string, string>()
    {
      {@"\", @"\\"},
      {",", @"\,"},
      {":", @"\:"},
      {";", @"\;"},
      {"'", @"\'"},
      {"[", @"\["},
      {"]", @"\]"}
    };
    private readonly Dictionary<EncodingProfile, string> IntelMpeg2EncodingProfiles = new Dictionary<EncodingProfile, string>()
    {
      { EncodingProfile.Simple, "-profile:v simple" },
      { EncodingProfile.Main, "-profile:v main" },
      { EncodingProfile.High, "-profile:v high" }
    };
    private readonly Dictionary<EncodingPreset, string> IntelMpeg2EncodingPresets = new Dictionary<EncodingPreset, string>()
    {
      { EncodingPreset.Default, "-preset fast" },
      { EncodingPreset.Ultrafast, "-preset fast" },
      { EncodingPreset.Superfast, "-preset fast" },
      { EncodingPreset.Veryfast, "-preset fast" },
      { EncodingPreset.Faster, "-preset fast" },
      { EncodingPreset.Fast, "-preset fast" },
      { EncodingPreset.Medium, "-preset medium" },
      { EncodingPreset.Slow, "-preset slow" },
      { EncodingPreset.Slower, "-preset slow" },
      { EncodingPreset.Veryslow, "-preset slow" }
    };
    private readonly Dictionary<EncodingProfile, string> SWH264EncodingProfiles = new Dictionary<EncodingProfile, string>()
    {
      { EncodingProfile.Baseline, "-profile:v baseline" },
      { EncodingProfile.Main, "-profile:v main" },
      { EncodingProfile.High, "-profile:v high" },
      { EncodingProfile.High422, "-profile:v high422" },
      { EncodingProfile.High444, "-profile:v high444" }
    };
    private readonly Dictionary<EncodingProfile, string> IntelH264EncodingProfiles = new Dictionary<EncodingProfile, string>()
    {
      { EncodingProfile.Baseline, "-profile:v baseline" },
      { EncodingProfile.Main, "-profile:v main" },
      { EncodingProfile.High, "-profile:v high" },
      { EncodingProfile.High422, "-profile:v high" },
      { EncodingProfile.High444, "-profile:v high" }
    };
    private readonly Dictionary<EncodingProfile, string> NvidiaH264EncodingProfiles = new Dictionary<EncodingProfile, string>()
    {
      { EncodingProfile.Baseline, "-profile:v baseline" },
      { EncodingProfile.Main, "-profile:v main" },
      { EncodingProfile.High, "-profile:v high" },
      { EncodingProfile.High422, "-profile:v high" },
      { EncodingProfile.High444, "-profile:v high" }
    };
    private readonly Dictionary<EncodingProfile, string> SWH265EncodingProfiles = new Dictionary<EncodingProfile, string>()
    {
    };
    private readonly Dictionary<EncodingProfile, string> IntelH265EncodingProfiles = new Dictionary<EncodingProfile, string>()
    {
      { EncodingProfile.Main, "-profile:v main" },
      { EncodingProfile.Main10, "-profile:v main10" }
    };
    private readonly Dictionary<EncodingProfile, string> NvidiaH265EncodingProfiles = new Dictionary<EncodingProfile, string>()
    {
    };
    private readonly Dictionary<EncodingPreset, string> SWH264EncodingPresets = new Dictionary<EncodingPreset, string>()
    {
      { EncodingPreset.Default, "-preset veryfast" },
      { EncodingPreset.Ultrafast, "-preset ultrafast" },
      { EncodingPreset.Superfast, "-preset superfast" },
      { EncodingPreset.Veryfast, "-preset veryfast" },
      { EncodingPreset.Faster, "-preset faster" },
      { EncodingPreset.Fast, "-preset fast" },
      { EncodingPreset.Medium, "-preset medium" },
      { EncodingPreset.Slow, "-preset slow" },
      { EncodingPreset.Slower, "-preset slower" },
      { EncodingPreset.Veryslow, "-preset veryslow" },
      { EncodingPreset.Placebo, "-preset placebo" }
    };
    private readonly Dictionary<EncodingPreset, string> IntelH264EncodingPresets = new Dictionary<EncodingPreset, string>()
    {
      { EncodingPreset.Default, "-preset veryfast" },
      { EncodingPreset.Ultrafast, "-preset veryfast" },
      { EncodingPreset.Superfast, "-preset veryfast" },
      { EncodingPreset.Veryfast, "-preset veryfast" },
      { EncodingPreset.Faster, "-preset faster" },
      { EncodingPreset.Fast, "-preset fast" },
      { EncodingPreset.Medium, "-preset medium" },
      { EncodingPreset.Slow, "-preset slow" },
      { EncodingPreset.Slower, "-preset slower" },
      { EncodingPreset.Veryslow, "-preset veryslow" }
    };
    private readonly Dictionary<EncodingPreset, string> NvidiaH264EncodingPresets = new Dictionary<EncodingPreset, string>()
    {
      { EncodingPreset.Default, "-preset hp" },
      { EncodingPreset.Ultrafast, "-preset hp" },
      { EncodingPreset.Superfast, "-preset hp" },
      { EncodingPreset.Veryfast, "-preset hp" },
      { EncodingPreset.Faster, "-preset hp" },
      { EncodingPreset.Fast, "-preset fast" },
      { EncodingPreset.Medium, "-preset medium" },
      { EncodingPreset.Slow, "-preset slow" },
      { EncodingPreset.Slower, "-preset hq" },
      { EncodingPreset.Veryslow, "-preset hq" }
    };
    private readonly Dictionary<EncodingPreset, string> SWH265EncodingPresets = new Dictionary<EncodingPreset, string>()
    {
      { EncodingPreset.Default, "-preset veryfast" },
      { EncodingPreset.Ultrafast, "-preset ultrafast" },
      { EncodingPreset.Superfast, "-preset superfast" },
      { EncodingPreset.Veryfast, "-preset veryfast" },
      { EncodingPreset.Faster, "-preset faster" },
      { EncodingPreset.Fast, "-preset fast" },
      { EncodingPreset.Medium, "-preset medium" },
      { EncodingPreset.Slow, "-preset slow" },
      { EncodingPreset.Slower, "-preset slower" },
      { EncodingPreset.Veryslow, "-preset veryslow" },
      { EncodingPreset.Placebo, "-preset placebo" }
    };
    private readonly Dictionary<EncodingPreset, string> IntelH265EncodingPresets = new Dictionary<EncodingPreset, string>()
    {
      { EncodingPreset.Default, "-preset fast" },
      { EncodingPreset.Ultrafast, "-preset fast" },
      { EncodingPreset.Superfast, "-preset fast" },
      { EncodingPreset.Veryfast, "-preset fast" },
      { EncodingPreset.Faster, "-preset fast" },
      { EncodingPreset.Fast, "-preset fast" },
      { EncodingPreset.Medium, "-preset medium" },
      { EncodingPreset.Slow, "-preset slow" },
      { EncodingPreset.Slower, "-preset slow" },
      { EncodingPreset.Veryslow, "-preset slow" }
    };
    private readonly Dictionary<EncodingPreset, string> NvidiaH265EncodingPresets = new Dictionary<EncodingPreset, string>()
    {
      { EncodingPreset.Default, "-preset hp" },
      { EncodingPreset.Ultrafast, "-preset hp" },
      { EncodingPreset.Superfast, "-preset hp" },
      { EncodingPreset.Veryfast, "-preset hp" },
      { EncodingPreset.Faster, "-preset hp" },
      { EncodingPreset.Fast, "-preset fast" },
      { EncodingPreset.Medium, "-preset medium" },
      { EncodingPreset.Slow, "-preset slow" },
      { EncodingPreset.Slower, "-preset hq" },
      { EncodingPreset.Veryslow, "-preset hq" }
    };
    private readonly Dictionary<QualityMode, string> VideoQualityModes = new Dictionary<QualityMode, string>()
    {
      { QualityMode.Default, "25" },
      { QualityMode.Best, "10" },
      { QualityMode.Normal, "25" },
      { QualityMode.Low, "35" }
    };
    private readonly Dictionary<QualityMode, string> VideoScaleModes = new Dictionary<QualityMode, string>()
    {
      { QualityMode.Default, "2" },
      { QualityMode.Best, "1" },
      { QualityMode.Normal, "2" },
      { QualityMode.Low, "10" }
    };
    private readonly Dictionary<QualityMode, string> ImageScaleModes = new Dictionary<QualityMode, string>()
    {
      { QualityMode.Default, "2" },
      { QualityMode.Best, "0" },
      { QualityMode.Normal, "2" },
      { QualityMode.Low, "10" }
    };
    private readonly Dictionary<Coder, string> Coders = new Dictionary<Coder, string>()
    {
      { Coder.Default, "" },
      { Coder.Arithmic, "-coder ac" },
      { Coder.Deflate, "-coder deflate" },
      { Coder.None, "" },
      { Coder.Raw, "-coder raw" },
      { Coder.RunLength, "-coder rle" },
      { Coder.VariableLength, "-coder vlc" }
    };


    internal FFMpegCommandline(MediaConverter mediaConverter)
    {
      _transcoderMaximumThreads = mediaConverter.TranscoderMaximumThreads;
      _transcoderTimeout = mediaConverter.TranscoderTimeout;
      _transcoderCachePath = mediaConverter.TranscoderCachePath;
      _hlsSegmentTimeInSeconds = mediaConverter.HLSSegmentTimeInSeconds;
      _hlsSegmentFileTemplate = mediaConverter.HLSSegmentFileTemplate;
      _supportHardcodedSubs = mediaConverter._supportHardcodedSubs;
    }

    internal void InitTranscodingParameters(IResourceAccessor sourceFile, ref FFMpegTranscodeData data)
    {
      data.InputResourceAccessor = sourceFile;
      AddInputOptions(ref data);
      data.OutputArguments.Add("-y");
    }

    private void AddInputOptions(ref FFMpegTranscodeData data)
    {
      Logger.Debug("Media Converter: AddInputOptions() is NetworkResource: {0}", data.InputResourceAccessor.ParentProvider.Metadata.NetworkResource);
      if (data.InputResourceAccessor.ParentProvider.Metadata.NetworkResource)
        if (((INetworkResourceAccessor)data.InputResourceAccessor).URL.StartsWith("rtsp://", StringComparison.InvariantCultureIgnoreCase))
        {
          data.GlobalArguments.Add("-rtsp_transport +tcp+udp");
          data.GlobalArguments.Add("-analyzeduration 10000000");
        }
    }

    internal void AddTranscodingThreadsParameters(bool useOutputThreads, ref FFMpegTranscodeData data)
    {
      data.InputArguments.Add(string.Format("-threads {0}", _transcoderMaximumThreads));
      if (useOutputThreads)
      {
        data.OutputArguments.Add(string.Format("-threads {0}", _transcoderMaximumThreads));
      }
    }

    internal void AddTargetVideoFormatAndOutputFileParameters(VideoTranscoding video, string transcodingFile, ref FFMpegTranscodeData data)
    {
      if (video.TargetVideoContainer == VideoContainer.Hls)
      {
        string pathName = Path.Combine(_transcoderCachePath, Path.GetFileNameWithoutExtension(transcodingFile).Replace(".", "_") + "_mptf");
        if (Directory.Exists(pathName) == false)
        {
          Directory.CreateDirectory(pathName);
        }
        data.WorkPath = pathName;
        data.SegmentPlaylist = "playlist.m3u8";

        //Segment muxer
        //data.OutputArguments.Add(string.Format("-f {0}", GetVideoContainer(video.TargetVideoContainer)));
        //data.OutputArguments.Add(string.Format("-segment_format {0}", GetVideoContainer(VideoContainer.Mpeg2Ts)));
        //data.OutputArguments.Add(string.Format("-segment_time {0}", HLSSegmentTimeInSeconds));
        //data.OutputArguments.Add("-segment_list_flags live");
        //data.OutputArguments.Add("-segment_list_type hls");
        //data.OutputArguments.Add("-segment_list_size 0");
        //data.OutputArguments.Add(string.Format("-segment_list {0}", "\"" + data.SegmentPlaylist + "\""));
        //data.OutputFilePath = HLSSegmentFileTemplate;

        //HLS muxer
        data.OutputArguments.Add("-hls_list_size 0");
        data.OutputArguments.Add("-hls_allow_cache 0");
        data.OutputArguments.Add(string.Format("-hls_time {0}", _hlsSegmentTimeInSeconds));
        data.OutputArguments.Add(string.Format("-hls_segment_filename {0}", "\"" + _hlsSegmentFileTemplate + "\""));
        data.OutputFilePath = data.SegmentPlaylist;
      }
      else
      {
        data.OutputArguments.Add(string.Format("-f {0}", FFMpegGetVideoContainer.GetVideoContainer(video.TargetVideoContainer)));
        data.OutputFilePath = transcodingFile;
      }

      if (video.Movflags != null)
      {
        data.OutputArguments.Add(string.Format("-movflags {0}", video.Movflags));
      }
    }

    internal void AddStreamMapParameters(int videoStreamIndex, int audioStreamIndex, bool embeddedSubtitle, ref FFMpegTranscodeData data)
    {
      if (videoStreamIndex != -1)
      {
        data.OutputArguments.Add(string.Format("-map 0:{0}", videoStreamIndex));
      }
      if (audioStreamIndex != -1)
      {
        data.OutputArguments.Add(string.Format("-map 0:{0}", audioStreamIndex));
      }
      if (embeddedSubtitle)
      {
        data.OutputArguments.Add("-map 1:0");
      }
    }

    internal string ExtractSubtitleFile(VideoTranscoding video, SubtitleStream subtitle, string subtitleEncoding, string targetFilePath)
    {
      string subtitleEncoder = "copy";
      SubtitleCodec targetCodec = video.TargetSubtitleCodec;
      if (targetCodec == SubtitleCodec.Unknown)
      {
        targetCodec = subtitle.Codec;
      }
      if (targetCodec == SubtitleCodec.Unknown)
      {
        targetCodec = SubtitleCodec.Ass;
      }
      if (subtitle.Codec != targetCodec)
      {
        subtitleEncoder = FFMpegGetSubtitleContainer.GetSubtitleContainer(targetCodec);
      }
      string subtitleFormat = FFMpegGetSubtitleContainer.GetSubtitleContainer(subtitle.Codec);
      FFMpegTranscodeData data = new FFMpegTranscodeData(_transcoderCachePath);
      InitTranscodingParameters(video.SourceFile, ref data);
      AddSubtitleExtractionParameters(subtitle, subtitleEncoding, subtitleEncoder, subtitleFormat, ref data);
      data.OutputFilePath = targetFilePath;

      if (Logger != null) Logger.Debug("MediaConverter: Invoking transcoder to extract subtitle from file '{0}'", video.SourceFile);
      FFMpegFileProcessor.FileProcessor(ref data, _transcoderTimeout);
      if (File.Exists(targetFilePath) == false)
      {
        if (Logger != null) Logger.Error("MediaConverter: Failed to extract subtitle from file '{0}'", video.SourceFile);
        return null;
      }
      return targetFilePath;
    }

    internal void AddSubtitleEmbeddingParameters(Subtitle subtitle, SubtitleCodec codec, ref FFMpegTranscodeData data)
    {
      if (codec == SubtitleCodec.Unknown) return;
      if (subtitle == null) return;

      data.InputSubtitleFilePath = subtitle.SourceFile;

      string subtitleFormat = FFMpegGetSubtitleContainer.GetSubtitleContainer(subtitle.Codec);
      data.InputSubtitleArguments.Add(string.Format("-f {0}", subtitleFormat));
      string subtitleEncoder = FFMpegGetSubtitleContainer.GetSubtitleContainer(codec);
      data.OutputArguments.Add(string.Format("-c:s {0}", subtitleEncoder));
      if (string.IsNullOrEmpty(subtitle.Language) == false)
      {
        string languageName = null;
        CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures);
        foreach (CultureInfo culture in cultures)
        {
          if (culture.TwoLetterISOLanguageName.ToUpperInvariant() == subtitle.Language)
          {
            languageName = culture.ThreeLetterISOLanguageName;
            break;
          }
        }
        if (string.IsNullOrEmpty(languageName) == false)
        {
          data.OutputArguments.Add(string.Format("-metadata:s:s:0 language={0}", languageName.ToLowerInvariant()));
        }
      }
    }

    private void AddSubtitleExtractionParameters(SubtitleStream subtitle, string subtitleEncoding, string subtitleEncoder, string subtitleFormat, ref FFMpegTranscodeData data)
    {
      if (string.IsNullOrEmpty(subtitleEncoding) == false)
      {
        data.OutputArguments.Add(string.Format("-sub_charenc {0}", subtitleEncoding));
      }
      data.OutputArguments.Add("-vn");
      data.OutputArguments.Add("-an");
      data.OutputArguments.Add(string.Format("-map 0:{0}", subtitle.StreamIndex));
      data.OutputArguments.Add(string.Format("-c:s {0}", subtitleEncoder));
      data.OutputArguments.Add(string.Format("-f {0}", subtitleFormat));
    }

    private void AddImageFilterParameters(ImageTranscoding image, ref FFMpegTranscodeData data)
    {
      int iHeight = image.SourceHeight;
      int iWidth = image.SourceWidth;
      if (iHeight > image.TargetHeight && image.TargetHeight > 0)
      {
        double scale = (double)image.SourceWidth / (double)image.SourceHeight;
        iHeight = image.TargetHeight;
        iWidth = Convert.ToInt32(scale * (double)iHeight);
      }
      if (iWidth > image.TargetWidth && image.TargetWidth > 0)
      {
        double scale = (double)image.SourceHeight / (double)image.SourceWidth;
        iWidth = image.TargetWidth;
        iHeight = Convert.ToInt32(scale * (double)iWidth);
      }

      if (image.TargetAutoRotate == true)
      {
        if (image.SourceOrientation > 4)
        {
          int iTemp = iWidth;
          iWidth = iHeight;
          iHeight = iTemp;
        }

        if (image.SourceOrientation > 1)
        {
          if (image.SourceOrientation == 2)
          {
            data.OutputFilter.Add("hflip");
          }
          else if (image.SourceOrientation == 3)
          {
            data.OutputFilter.Add("hflip");
            data.OutputFilter.Add("vflip");
          }
          else if (image.SourceOrientation == 4)
          {
            data.OutputFilter.Add("vflip");
          }
          else if (image.SourceOrientation == 5)
          {
            data.OutputFilter.Add("transpose=0");
          }
          else if (image.SourceOrientation == 6)
          {
            data.OutputFilter.Add("transpose=1");
          }
          else if (image.SourceOrientation == 7)
          {
            data.OutputFilter.Add("transpose=2");
            data.OutputFilter.Add("hflip");
          }
          else if (image.SourceOrientation == 8)
          {
            data.OutputFilter.Add("transpose=2");
          }
        }
      }
      data.OutputFilter.Add(string.Format("scale={0}:{1}", iWidth, iHeight));
    }

    internal void AddAudioParameters(AudioTranscoding audio, ref FFMpegTranscodeData data)
    {
      if (Checks.IsAudioStreamChanged(audio) == false)
      {
        data.OutputArguments.Add("-c:a copy");
      }
      else
      {
        data.OutputArguments.Add(string.Format("-c:a {0}", FFMpegGetAudioCodec.GetAudioCodec(audio.TargetAudioCodec)));
        long frequency = Validators.GetAudioFrequency(audio.SourceAudioCodec, audio.TargetAudioCodec, audio.SourceAudioFrequency, audio.TargetAudioFrequency);
        if (frequency > 0)
        {
          data.OutputArguments.Add(string.Format("-ar {0}", frequency));
        }
        if (audio.TargetAudioContainer != AudioContainer.Lpcm)
        {
          long audioBitrate = Validators.GetAudioBitrate(audio.SourceAudioBitrate, audio.TargetAudioBitrate);
          data.OutputArguments.Add(string.Format("-b:a {0}k", audioBitrate));
        }
      }
      if (audio.TargetAudioContainer == AudioContainer.Mp3)
      {
        data.OutputArguments.Add("-id3v2_version 3");
      }
      AddAudioChannelsNumberParameters(audio, ref data);

      string coder = null;
      Coders.TryGetValue(audio.TargetCoder, out coder);
      if(string.IsNullOrEmpty(coder) == false)
      {
        data.OutputArguments.Add(coder);
      }
    }

    internal void AddImageParameters(ImageTranscoding image, ref FFMpegTranscodeData data)
    {
      if (Checks.IsImageStreamChanged(image) == false)
      {
        data.OutputArguments.Add("-c:v copy");
      }
      else
      {
        AddImageFilterParameters(image, ref data);
        if (image.TargetPixelFormat != PixelFormat.Unknown)
        {
          data.OutputArguments.Add(string.Format("-pix_fmt {0}", FFMpegGetPixelFormat.GetPixelFormat(image.TargetPixelFormat)));
        }
        string scale = null;
        ImageScaleModes.TryGetValue(image.TargetImageQuality, out scale);
        if (image.TargetImageQuality == QualityMode.Custom)
        {
          data.OutputArguments.Add(string.Format("-q:v {0}", image.TargetImageQualityFactor));
        }
        else if (string.IsNullOrEmpty(scale) == false)
        {
          data.OutputArguments.Add(string.Format("-q:v {0}", scale));
        }

        if (image.TargetImageCodec != ImageContainer.Unknown)
        {
          data.OutputArguments.Add(string.Format("-c:v {0}", FFMpegGetImageCodec.GetImageCodec(image.TargetImageCodec)));
        }
      }

      string coder = null;
      Coders.TryGetValue(image.TargetCoder, out coder);
      if (string.IsNullOrEmpty(coder) == false)
      {
        data.OutputArguments.Add(coder);
      }
    }

    internal void AddVideoParameters(VideoTranscoding video, string transcodeId, Subtitle subtitle, ref FFMpegTranscodeData data, ref List<string> intelTranscodes, ref List<string> nvidiaTranscodes)
    {
      if (video.TargetVideoCodec == VideoCodec.Unknown)
      {
        video.TargetVideoCodec = video.SourceVideoCodec;
      }
      if (video.TargetVideoAspectRatio <= 0)
      {
        if (video.SourceVideoHeight > 0 && video.SourceVideoWidth > 0)
        {
          video.TargetVideoAspectRatio = (float)video.SourceVideoWidth / (float)video.SourceVideoHeight;
        }
        else
        {
          video.TargetVideoAspectRatio = 16.0F / 9.0F;
        }
      }
      if (video.SourceVideoPixelAspectRatio <= 0)
      {
        video.SourceVideoPixelAspectRatio = 1.0F;
      }
      if (video.TargetVideoMaxHeight <= 0)
      {
        video.TargetVideoMaxHeight = 1080;
      }
      bool isIntel = Checks.IsIntelHWTranscode(video.TranscodeId, intelTranscodes);
      bool isNvidia = Checks.IsNvidiaHWTranscode(video.TranscodeId, nvidiaTranscodes);
      bool vCodecCopy = false;
      if (Checks.IsVideoStreamChanged(video, _supportHardcodedSubs) == false)
      {
        vCodecCopy = true;
        data.OutputArguments.Add("-c:v copy");
        data.GlobalArguments.Add("-fflags +genpts");
      }
      else
      {
        data.OutputArguments.Add(string.Format("-c:v {0}", FFMpegGetVideoCodec.GetVideoCodec(video.TargetVideoCodec, transcodeId, intelTranscodes, nvidiaTranscodes)));

        if (video.TargetPixelFormat == PixelFormat.Unknown)
        {
          video.TargetPixelFormat = PixelFormat.Yuv420;
        }
        data.OutputArguments.Add(string.Format("-pix_fmt {0}", FFMpegGetPixelFormat.GetPixelFormat(video.TargetPixelFormat)));

        if (video.TargetVideoCodec == VideoCodec.H265)
        {
          string profile = null;
          if (isIntel)
          {
            IntelH265EncodingProfiles.TryGetValue(video.TargetProfile, out profile);
          }
          else if (isNvidia)
          {
            NvidiaH265EncodingProfiles.TryGetValue(video.TargetProfile, out profile);
          }
          else
          {
            SWH265EncodingProfiles.TryGetValue(video.TargetProfile, out profile);
          }
          if (string.IsNullOrEmpty(profile) == false)
          {
            data.OutputArguments.Add(profile);
          }

          string preset = null;
          if (isIntel)
          {
            IntelH265EncodingPresets.TryGetValue(video.TargetPreset, out preset);
          }
          else if (isNvidia)
          {
            NvidiaH265EncodingPresets.TryGetValue(video.TargetPreset, out preset);
          }
          else
          {
            SWH265EncodingPresets.TryGetValue(video.TargetPreset, out preset);
          }
          if (string.IsNullOrEmpty(preset) == false)
          {
            data.OutputArguments.Add(preset);
          }

          AddVideoBitrateParameters(video, ref data, ref nvidiaTranscodes);

          string quality = null;
          VideoQualityModes.TryGetValue(video.TargetVideoQuality, out quality);
          if (video.TargetVideoQuality == QualityMode.Custom)
          {
            data.OutputArguments.Add(string.Format("-crf {0}", video.TargetQualityFactor));
          }
          else if (string.IsNullOrEmpty(quality) == false)
          {
            data.OutputArguments.Add(string.Format("-crf {0}", quality));
          }

          if (!isIntel && !isNvidia)
          {
            data.OutputArguments.Add("-x265-params");
            string args = "";
            if (video.TargetVideoQuality == QualityMode.Custom)
            {
              args += string.Format("-crf={0}", video.TargetQualityFactor);
            }
            else if (string.IsNullOrEmpty(quality) == false)
            {
              args += string.Format("-crf={0}", quality);
            }
            if (video.SourceFrameRate > 0)
            {
              args += string.Format(":fps={0}", Validators.GetValidFramerate(video.SourceFrameRate));
            }
            if (video.TargetLevel > 0)
            {
              args += string.Format(":level={0}", video.TargetLevel.ToString("0.0", CultureInfo.InvariantCulture));
            }
            data.OutputArguments.Add(args);
          }
        }
        else if (video.TargetVideoCodec == VideoCodec.H264)
        {
          if (video.TargetProfile == EncodingProfile.High && video.TargetPixelFormat == PixelFormat.Yuv422)
          {
            video.TargetProfile = EncodingProfile.High422;
          }
          else if (video.TargetProfile == EncodingProfile.High && video.TargetPixelFormat == PixelFormat.Yuv444)
          {
            video.TargetProfile = EncodingProfile.High444;
          }

          string profile = null;
          if (isIntel)
          {
            IntelH264EncodingProfiles.TryGetValue(video.TargetProfile, out profile);
          }
          else if (isNvidia)
          {
            NvidiaH264EncodingProfiles.TryGetValue(video.TargetProfile, out profile);
          }
          else
          {
            SWH264EncodingProfiles.TryGetValue(video.TargetProfile, out profile);
          }
          if (string.IsNullOrEmpty(profile) == false)
          {
            data.OutputArguments.Add(profile);
          }

          if (video.TargetLevel > 0)
          {
            data.OutputArguments.Add(string.Format("-level {0}", video.TargetLevel.ToString("0.0", CultureInfo.InvariantCulture)));
          }

          string preset = null;
          if (isIntel)
          {
            IntelH264EncodingPresets.TryGetValue(video.TargetPreset, out preset);
          }
          else if (isNvidia)
          {
            NvidiaH264EncodingPresets.TryGetValue(video.TargetPreset, out preset);
          }
          else
          {
            SWH264EncodingPresets.TryGetValue(video.TargetPreset, out preset);
          }
          if (string.IsNullOrEmpty(preset) == false)
          {
            data.OutputArguments.Add(preset);
          }

          AddVideoBitrateParameters(video, ref data, ref nvidiaTranscodes);

          string quality = null;
          VideoQualityModes.TryGetValue(video.TargetVideoQuality, out quality);
          if (video.TargetVideoQuality == QualityMode.Custom)
          {
            data.OutputArguments.Add(string.Format("-crf {0}", video.TargetQualityFactor));
          }
          else if (string.IsNullOrEmpty(quality) == false)
          {
            data.OutputArguments.Add(string.Format("-crf {0}", quality));
          }
        }
        else
        {
          if (video.TargetVideoCodec == VideoCodec.Mpeg2)
          {
            if (isIntel)
            {
              string profile = null;
              IntelMpeg2EncodingProfiles.TryGetValue(video.TargetProfile, out profile);
              if (string.IsNullOrEmpty(profile) == false)
              {
                data.OutputArguments.Add(profile);
              }

              string preset = null;
              IntelMpeg2EncodingPresets.TryGetValue(video.TargetPreset, out preset);
              if (string.IsNullOrEmpty(preset) == false)
              {
                data.OutputArguments.Add(preset);
              }
            }
          }
          if (AddVideoBitrateParameters(video, ref data, ref nvidiaTranscodes) == false)
          {
            string scale = null;
            VideoScaleModes.TryGetValue(video.TargetVideoQuality, out scale);
            if (video.TargetVideoQuality == QualityMode.Custom)
            {
              data.OutputArguments.Add(string.Format("-qscale:v {0}", video.TargetVideoQualityFactor));
            }
            else if (string.IsNullOrEmpty(scale) == false)
            {
              data.OutputArguments.Add(string.Format("-qscale:v {0}", scale));
            }
          }
        }

        AddVideoFiltersParameters(video, subtitle, ref data);
        if (video.SourceFrameRate > 0)
        {
          data.OutputArguments.Add(string.Format("-r {0}", Validators.GetValidFramerate(video.SourceFrameRate)));
        }
        data.OutputArguments.Add("-g 15");
      }
      if (vCodecCopy && video.SourceVideoCodec == VideoCodec.H264 && !Checks.IsMPEGTSContainer(video.SourceVideoContainer) && Checks.IsMPEGTSContainer(video.TargetVideoContainer))
      {
        data.OutputArguments.Add("-bsf:v h264_mp4toannexb");
      }
      else if (!vCodecCopy && video.TargetVideoCodec == VideoCodec.H264 && Checks.IsMPEGTSContainer(video.TargetVideoContainer))
      {
        data.OutputArguments.Add("-bsf:v h264_mp4toannexb");
        data.OutputArguments.Add("-flags -global_header");
      }
      if (video.TargetVideoContainer == VideoContainer.M2Ts)
      {
        data.OutputArguments.Add("-mpegts_m2ts_mode 1");
      }

      string coder = null;
      Coders.TryGetValue(video.TargetCoder, out coder);
      if (string.IsNullOrEmpty(coder) == false)
      {
        data.OutputArguments.Add(coder);
      }
    }

    private bool AddVideoBitrateParameters(VideoTranscoding video, ref FFMpegTranscodeData data, ref List<string> nvidiaTranscodes)
    {
      if (video.TargetVideoBitrate > 0)
      {
        if (video.TargetVideoCodec == VideoCodec.H264 || video.TargetVideoCodec == VideoCodec.H265)
        {
          if (Checks.IsNvidiaHWTranscode(video.TranscodeId, nvidiaTranscodes))
          {
            data.OutputArguments.Add("-cbr 1");
          }
        }
        data.OutputArguments.Add(string.Format("-b:v {0}", video.TargetVideoBitrate + "k"));
        data.OutputArguments.Add(string.Format("-maxrate:v {0}", video.TargetVideoBitrate + "k"));
        data.OutputArguments.Add(string.Format("-bufsize:v {0}", video.TargetVideoBitrate + "k"));

        return true;
      }
      return false;
    }

    private void AddVideoFiltersParameters(VideoTranscoding video, Subtitle subtitle, ref FFMpegTranscodeData data)
    {
      bool sourceSquarePixels = Checks.IsSquarePixel(video.SourceVideoPixelAspectRatio);
      Size newSize = new Size(video.SourceVideoWidth, video.SourceVideoHeight);
      Size newContentSize = new Size(video.SourceVideoWidth, video.SourceVideoHeight);
      float newPixelAspectRatio = video.SourceVideoPixelAspectRatio;
      bool pixelARChanged = false;
      bool videoARChanged = false;
      bool videoHeightChanged = false;

      GetVideoDimensions(video, out newSize, out newContentSize, out newPixelAspectRatio, out pixelARChanged, out videoARChanged, out videoHeightChanged);

      if (videoARChanged || pixelARChanged || videoHeightChanged)
      {
        if (videoHeightChanged || pixelARChanged)
        {
          data.OutputFilter.Add(string.Format("scale={0}:{1}", newContentSize.Width, newContentSize.Height));
        }
        if (videoARChanged)
        {
          int posX = Convert.ToInt32(Math.Abs(newSize.Width - newContentSize.Width) / 2);
          int posY = Convert.ToInt32(Math.Abs(newSize.Height - newContentSize.Height) / 2);
          data.OutputFilter.Add(string.Format("pad={0}:{1}:{2}:{3}:black", newSize.Width, newSize.Height, posX, posY));
          data.OutputFilter.Add(string.Format("setdar={0}/{1}", newSize.Width, newSize.Height));
        }
        if (pixelARChanged)
        {
          data.OutputFilter.Add("setsar=1");
        }
        else if (sourceSquarePixels == false)
        {
          data.OutputFilter.Add("setsar=" + video.SourceVideoPixelAspectRatio.ToString("0.00", CultureInfo.InvariantCulture));
        }
      }

      if (subtitle != null && subtitle.SourceFile != null && _supportHardcodedSubs == true && video.TargetSubtitleSupport == SubtitleSupport.HardCoded)
      {
        string encoding = "UTF-8";
        if (string.IsNullOrEmpty(subtitle.CharacterEncoding) == false)
        {
          encoding = subtitle.CharacterEncoding;
        }
        data.OutputFilter.Add(string.Format("subtitles=filename='{0}':original_size={1}x{2}:charenc='{3}'", EncodeFilePath(subtitle.SourceFile), newSize.Width, newSize.Height, encoding));
      }
    }

    private string EncodeFilePath(string filePath)
    {
      return _filerPathEncoding.Aggregate(filePath, (current, enc) => current.Replace(enc.Key, enc.Value));
    }

    internal void AddVideoAudioParameters(VideoTranscoding video, ref FFMpegTranscodeData data)
    {
      if (video.SourceAudioCodec == AudioCodec.Unknown)
      {
        data.OutputArguments.Add("-an");
        return;
      }
      if (Checks.IsAudioStreamChanged(video) == false)
      {
        data.OutputArguments.Add("-c:a copy");
      }
      else
      {
        data.OutputArguments.Add(string.Format("-c:a {0}", FFMpegGetAudioCodec.GetAudioCodec(video.TargetAudioCodec)));
        //if (video.TargetAudioCodec == AudioCodec.Aac || video.TargetAudioCodec == AudioCodec.Dts) //aac encoder not libvo_aacenc is experimental
        if (video.TargetAudioCodec == AudioCodec.Dts)
        {
          data.OutputArguments.Add("-strict experimental");
        }
        long frequency = Validators.GetAudioFrequency(video.SourceAudioCodec, video.TargetAudioCodec, video.SourceAudioFrequency, video.TargetAudioFrequency);
        if (frequency != -1)
        {
          data.OutputArguments.Add(string.Format("-ar {0}", frequency));
        }
        if (video.TargetAudioCodec != AudioCodec.Lpcm)
        {
          data.OutputArguments.Add(string.Format("-b:a {0}k", Validators.GetAudioBitrate(video.SourceAudioBitrate, video.TargetAudioBitrate)));
        }
        AddAudioChannelsNumberParameters(video, ref data);
      }
    }

    private void AddAudioChannelsNumberParameters(BaseTranscoding media, ref FFMpegTranscodeData data)
    {
      int channels = -1;
      if (media is VideoTranscoding)
      {
        VideoTranscoding video = (VideoTranscoding)media;
        channels = Validators.GetAudioNumberOfChannels(video.SourceAudioCodec, video.TargetAudioCodec, video.SourceAudioChannels, video.TargetForceAudioStereo);
      }
      if (media is AudioTranscoding)
      {
        AudioTranscoding audio = (AudioTranscoding)media;
        channels = Validators.GetAudioNumberOfChannels(audio.SourceAudioCodec, audio.TargetAudioCodec, audio.SourceAudioChannels, audio.TargetForceAudioStereo);
      }
      if (channels > 0)
      {
        data.OutputArguments.Add(string.Format("-ac {0}", channels));
      }
    }

    internal static ILogger Logger
    {
      get { return ServiceRegistration.Get<ILogger>(); }
    }
  }
}
