﻿using System.Globalization;
using System.IO;
using System.Text;

namespace MediaPortal.Plugins.Transcoding.Service
{
  public class SubtitleAnalyzer
  {
    public static string GetEncoding(string subtitleSource, string subtitleLanguage, string defaultEncoding)
    {
      if (string.IsNullOrEmpty(subtitleSource))
      {
        return null;
      }

      byte[] buffer = File.ReadAllBytes(subtitleSource);

      //Use byte order mark if any
      if (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0XFE && buffer[3] == 0XFF)
        return "UTF-32";
      else if (buffer[0] == 0XFF && buffer[1] == 0XFE && buffer[2] == 0x00 && buffer[3] == 0x00)
        return "UTF-32";
      else if (buffer[0] == 0XFE && buffer[1] == 0XFF)
        return "UNICODEBIG";
      else if (buffer[0] == 0XFF && buffer[1] == 0XFE)
        return "UNICODELITTLE";
      else if (buffer[0] == 0XEF && buffer[1] == 0XBB && buffer[2] == 0XBF)
        return "UTF-8";
      else if (buffer[0] == 0X2B && buffer[1] == 0X2F && buffer[2] == 0x76)
        return "UTF-7";

      //Detect encoding from language
      if (string.IsNullOrEmpty(subtitleLanguage) == false)
      {
        CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures);
        foreach (CultureInfo culture in cultures)
        {
          if (culture.TwoLetterISOLanguageName.ToUpperInvariant() == subtitleLanguage.ToUpperInvariant())
          {
            return Encoding.GetEncoding(culture.TextInfo.ANSICodePage).BodyName.ToUpperInvariant();
          }
        }
      }

      //Detect encoding from file
      Ude.CharsetDetector cdet = new Ude.CharsetDetector();
      cdet.Feed(buffer, 0, buffer.Length);
      cdet.DataEnd();
      if (cdet.Charset != null && cdet.Confidence >= 0.1)
      {
        return Encoding.GetEncoding(cdet.Charset).BodyName.ToUpperInvariant();
      }

      if (string.IsNullOrEmpty(defaultEncoding))
      {
        //Use windows encoding
        return Encoding.Default.BodyName.ToUpperInvariant();
      }
      return defaultEncoding;
    }

    public static string GetLanguage(string subtitleSource, string defaultEncoding, string defaultLanguage)
    {
      if (string.IsNullOrEmpty(subtitleSource))
      {
        return null;
      }

      CultureInfo[] cultures = CultureInfo.GetCultures(CultureTypes.NeutralCultures);

      //Language from file name
      string[] tags = subtitleSource.Split('.');
      if (tags.Length > 2)
      {
        foreach (CultureInfo culture in cultures)
        {
          string languageName = culture.EnglishName;
          if (culture.IsNeutralCulture == false)
          {
            languageName = culture.Parent.EnglishName;
          }
          if (languageName.ToUpperInvariant() == tags[tags.Length - 2].ToUpperInvariant() ||
            culture.ThreeLetterISOLanguageName.ToUpperInvariant() == tags[tags.Length - 2].ToUpperInvariant() ||
            culture.ThreeLetterWindowsLanguageName.ToUpperInvariant() == tags[tags.Length - 2].ToUpperInvariant() ||
            culture.TwoLetterISOLanguageName.ToUpperInvariant() == tags[tags.Length - 2].ToUpperInvariant())
          {
            return culture.TwoLetterISOLanguageName.ToUpperInvariant();
          }
        }
      }

      if (string.IsNullOrEmpty(defaultEncoding) == false)
      {
        //Languge from file encoding
        string encoding = GetEncoding(subtitleSource, null, defaultEncoding);
        if (encoding != null)
        {
          switch (encoding.ToUpperInvariant())
          {
            case "US-ASCII":
              return "EN";

            case "WINDOWS-1253":
              return "EL";
            case "ISO-8859-7":
              return "EL";

            case "WINDOWS-1254":
              return "TR";

            case "WINDOWS-1255":
              return "HE";
            case "ISO-8859-8":
              return "HE";

            case "WINDOWS-1256":
              return "AR";
            case "ISO-8859-6":
              return "AR";

            case "WINDOWS-1258":
              return "VI";
            case "VISCII":
              return "VI";

            case "WINDOWS-31J":
              return "JA";
            case "EUC-JP":
              return "JA";
            case "Shift_JIS":
              return "JA";
            case "ISO-2022-JP":
              return "JA";

            case "X-MSWIN-936":
              return "ZH";
            case "GB18030":
              return "ZH";
            case "X-EUC-CN":
              return "ZH";
            case "GBK":
              return "ZH";
            case "GB2312":
              return "ZH";
            case "X-WINDOWS-950":
              return "ZH";
            case "X-MS950-HKSCS":
              return "ZH";
            case "X-EUC-TW":
              return "ZH";
            case "BIG5":
              return "ZH";
            case "BIG5-HKSCS":
              return "ZH";

            case "EUC-KR":
              return "KO";
            case "ISO-2022-KR":
              return "KO";

            case "TIS-620":
              return "TH";
            case "ISO-8859-11":
              return "TH";

            case "KOI8-R":
              return "RU";
            case "KOI7":
              return "RU";

            case "KOI8-U":
              return "UK";
          }
        }
      }

      if (string.IsNullOrEmpty(defaultLanguage))
      {
        //English as default
        return "EN";
      }
      return defaultLanguage;
    }
  }
}
