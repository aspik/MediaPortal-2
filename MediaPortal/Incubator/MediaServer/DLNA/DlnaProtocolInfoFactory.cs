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

using MediaPortal.Common;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;

namespace MediaPortal.Extensions.MediaServer.DLNA
{
    public class DlnaProtocolInfoFactory
    {
        public DlnaProtocolInfo GetProtocolInfo(MediaItem item)
        {
            string mediaType = GetDLNAMimeType(item);
            if (mediaType == null)
                return null;

            var info = new DlnaProtocolInfo
            {
                Protocol = "http-get",
                Network = "*",
                MediaType = mediaType,
                AdditionalInfo = new DlnaForthField()
            };

            ConfigureProfile(info.AdditionalInfo, item, info.MediaType);
            return info;
        }

    private static string ConfigureProfile(DlnaForthField dlnaField, MediaItem item, string mediaType)
    {
      //TODO: much better type resolution
      switch (mediaType)
      {
        case "audio/mpeg":
        case "audio/ogg":
        case "audio/wav":
          dlnaField.ProfileParameter.ProfileName = DlnaProfiles.Mp3;
          dlnaField.OperationsParameter.Show = true;
          dlnaField.OperationsParameter.TimeSeekRangeSupport = true;
          dlnaField.OperationsParameter.ByteSeekRangeSupport = true;
          dlnaField.FlagsParameter.SenderPaced = true;
          dlnaField.FlagsParameter.ByteBasedSeek = true;
          dlnaField.FlagsParameter.StreamingMode = true;
          dlnaField.FlagsParameter.InteractiveMode = false;
          dlnaField.FlagsParameter.BackgroundMode = true;
          break;
		  
        case "video/mpeg":
        case "video/vnd.dlna.mpeg-tts":
          dlnaField.ProfileParameter.ProfileName = DlnaProfiles.MpegPsPal;
          dlnaField.FlagsParameter.StreamingMode = true;
          dlnaField.FlagsParameter.InteractiveMode = false;
          dlnaField.FlagsParameter.BackgroundMode = true;
          break;
		  
        case "image/jpeg":
          dlnaField.ProfileParameter.ProfileName = DlnaProfiles.JpegLarge;
          dlnaField.FlagsParameter.StreamingMode = false;
          dlnaField.FlagsParameter.InteractiveMode = true;
          dlnaField.FlagsParameter.BackgroundMode = true;
          break;

        default:
          ServiceRegistration.Get<ILogger>().Warn("No DLNA profile for MIME type {0}", mediaType);
          break;
      }

      return null;
    }

        public static DlnaProtocolInfo GetProfileInfo(MediaItem item)
        {
            // Why create a factory each time?
            var factory = new DlnaProtocolInfoFactory();
            return factory.GetProtocolInfo(item);
        }

        public static string GetDLNAMimeType(MediaItem item)
        {
            string mimeType;
            if (!MediaItemAspect.TryGetAttribute(item.Aspects, MediaAspect.ATTR_MIME_TYPE, out mimeType))
            {
                Logger.Warn("No MIME type for {0}", item.MediaItemId);
                return null;
            }

            // TODO: Add other types here
            switch (mimeType)
            {
                case "audio/mp3":
                    return "audio/mpeg";

                case "audio/ogg":
                    return "audio/ogg";

                case "audio/wav":
                    return "audio/wav";

                case "video/mpeg":
                    return "video/mpeg";

                case "video/vnd.dlna.mpeg-tts":
                    return "video/vnd.dlna.mpeg-tts";

                case "image/png":
                    return "image/png";

                case "image/jpeg":
                case "image/pjpeg":
                    return "image/jpeg";
            }

            Logger.Warn("No DLNA media type for MIME type {0}", mimeType);
            return null;
        }

        internal static ILogger Logger
        {
          get { return ServiceRegistration.Get<ILogger>(); }
        }
    }
}