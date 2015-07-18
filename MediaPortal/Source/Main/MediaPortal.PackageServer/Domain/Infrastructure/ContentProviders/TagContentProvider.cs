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
using System.Reflection;
using MediaPortal.PackageServer.Domain.Entities;
using MediaPortal.PackageServer.Domain.Entities.Enumerations;
using MediaPortal.PackageServer.Domain.Entities.Helpers;
using MediaPortal.PackageServer.Domain.Infrastructure.Context;

namespace MediaPortal.PackageServer.Domain.Infrastructure.ContentProviders
{
  internal class TagContentProvider : AbstractContentProvider
  {
    public TagContentProvider() : base(29)
    {
    }

    public override void CreateContent(DataContext context)
    {
      var tagProviders = typeof(Tags).GetProperties(BindingFlags.Static | BindingFlags.Public);
      foreach (var tagProviderProperty in tagProviders)
      {
        var tagType = (TagType)Enum.Parse(typeof(TagType), tagProviderProperty.Name);
        var tagProvider = tagProviderProperty.GetValue(null);
        foreach (var tag in tagProvider.GetType().GetProperties())
        {
          AddTag(context, tagType, (string)tag.GetValue(tagProvider));
        }
      }
      context.SaveChanges();
    }

    private void AddTag(DataContext context, TagType type, string name)
    {
      var tag = new Tag
      {
        Type = type,
        Name = name,
      };
      context.Tags.Add(tag);
    }
  }
}