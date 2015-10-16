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
using MediaPortal.Common.MediaManagement;

namespace MediaPortal.Plugins.Transcoding.Aspects
{
  public class TranscodeItemAudioAspect
  {
    /// <summary>
    /// Media item aspect id of the recording aspect.
    /// </summary>
    public static readonly Guid ASPECT_ID = new Guid("B387AE89-803E-494E-87AC-D763DE09EAC0");

    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_CONTAINER =
      MediaItemAspectMetadata.CreateStringAttributeSpecification("Container", 20, Cardinality.Inline, false);

    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_STREAM =
        MediaItemAspectMetadata.CreateAttributeSpecification("Stream", typeof(int), Cardinality.Inline, false);

    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_CODEC =
      MediaItemAspectMetadata.CreateStringAttributeSpecification("Codec", 20, Cardinality.Inline, false);

    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_CHANNELS =
        MediaItemAspectMetadata.CreateAttributeSpecification("Channels", typeof(int), Cardinality.Inline, false);

    public static readonly MediaItemAspectMetadata.AttributeSpecification ATTR_FREQUENCY =
        MediaItemAspectMetadata.CreateAttributeSpecification("Frequency", typeof(long), Cardinality.Inline, false);

    public static readonly MediaItemAspectMetadata Metadata = new MediaItemAspectMetadata(
        ASPECT_ID, "TranscodeItemAudio", new[] {
            ATTR_CONTAINER,
            ATTR_STREAM,
            ATTR_CODEC,
            ATTR_CHANNELS,
            ATTR_FREQUENCY
        });
  }
}
