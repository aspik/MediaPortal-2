#region Copyright (C) 2007-2015 Team MediaPortal

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
using System.Globalization;
using System.Linq;
using MediaPortal.Common;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.Messaging;
using MediaPortal.Common.Services.Settings;
using MediaPortal.Common.Settings;
using MediaPortal.Extensions.OnlineLibraries.Libraries.Trakt;
using MediaPortal.Extensions.OnlineLibraries.Libraries.Trakt.Authentication;
using MediaPortal.Extensions.OnlineLibraries.Libraries.Trakt.DataStructures;
using MediaPortal.UI.Presentation.Players;
using MediaPortal.UI.Presentation.Players.ResumeState;
using MediaPortal.UI.Services.Players;
using TraktSettings = MediaPortal.UiComponents.Trakt.Settings.TraktSettings;

namespace MediaPortal.UiComponents.Trakt.Service
{
  public class TraktHandler : IDisposable
  {
    private AsynchronousMessageQueue _messageQueue;
    private readonly SettingsChangeWatcher<TraktSettings> _settings = new SettingsChangeWatcher<TraktSettings>();
    private TraktScrobbleMovie _dataMovie = new TraktScrobbleMovie();
    private TraktScrobbleEpisode _dataEpisode = new TraktScrobbleEpisode();
    private TimeSpan _duration;
    private double _progress;

    public TraktHandler()
    {
      _settings.SettingsChanged += ConfigureHandler;
      ConfigureHandler();
    }

    private void ConfigureHandler(object sender, EventArgs e)
    {
      ConfigureHandler();
    }

    private void ConfigureHandler()
    {
      if (_settings.Settings.EnableTrakt)
      {
        SubscribeToMessages();
      }
      else
      {
        UnsubscribeFromMessages();
      }
    }

    void SubscribeToMessages()
    {
      if (_messageQueue != null)
        return;
      _messageQueue = new AsynchronousMessageQueue(this, new string[]
        {
           PlayerManagerMessaging.CHANNEL,
        });
      _messageQueue.MessageReceived += OnMessageReceived;
      _messageQueue.Start();
    }

    void UnsubscribeFromMessages()
    {
      if (_messageQueue == null)
        return;
      _messageQueue.Shutdown();
      _messageQueue = null;
    }

    void OnMessageReceived(AsynchronousMessageQueue queue, SystemMessage message)
    {
      if (message.ChannelName == PlayerManagerMessaging.CHANNEL)
      {
        // React to player changes
        PlayerManagerMessaging.MessageType messageType = (PlayerManagerMessaging.MessageType)message.MessageType;
        switch (messageType)
        {
          case PlayerManagerMessaging.MessageType.PlayerResumeState:
            IResumeState resumeState = (IResumeState)message.MessageData[PlayerManagerMessaging.KEY_RESUME_STATE];
            PositionResumeState positionResume = resumeState as PositionResumeState;
            TimeSpan resumePosition = positionResume.ResumePosition;
            _progress = Math.Min((int)(resumePosition.TotalSeconds * 100 / _duration.TotalSeconds), 100);
            break;
          case PlayerManagerMessaging.MessageType.PlayerError:
          case PlayerManagerMessaging.MessageType.PlayerEnded:
          case PlayerManagerMessaging.MessageType.PlayerStopped:
            StopScrobble();
            break;
          case PlayerManagerMessaging.MessageType.PlayerStarted:
            var psc = (IPlayerSlotController)message.MessageData[PlayerManagerMessaging.PLAYER_SLOT_CONTROLLER];
            CreateScrobbleData(psc);
            StartScrobble();
            break;
        }
      }
    }

    private void CreateScrobbleData(IPlayerSlotController psc)
    {
      IPlayerContext pc = PlayerContext.GetPlayerContext(psc);
      IMediaPlaybackControl pmc = pc.CurrentPlayer as IMediaPlaybackControl;
      if (pmc == null)
      {
        return;
      }

      _duration = pmc.Duration;
      bool isMovie = pc.CurrentMediaItem.Aspects.ContainsKey(MovieAspect.ASPECT_ID);
      bool isSeries = pc.CurrentMediaItem.Aspects.ContainsKey(SeriesAspect.ASPECT_ID);

      if (isMovie)
      {
        _dataMovie = CreateMovieData(pc);
        _dataMovie.AppDate = DateTime.Now.ToString(CultureInfo.InvariantCulture);
        _dataMovie.AppVersion = "0.2.0";
      }

      if (isSeries)
      {
        _dataEpisode = CreateEpisodeData(pc);
        _dataMovie.AppDate = DateTime.Now.ToString(CultureInfo.InvariantCulture);
        _dataMovie.AppVersion = "0.2.0";
      }

    }

    private TraktScrobbleMovie CreateMovieData(IPlayerContext pc)
    {
      var movieScrobbleData = new TraktScrobbleMovie
      {
        Movie = new TraktMovie
        {
          Ids = new TraktMovieId { Imdb = GetMovieImdb(pc.CurrentMediaItem), Tmdb = GetMovieTmdb(pc.CurrentMediaItem) },
          Title = GetMovieTitle(pc.CurrentMediaItem),
          Year = GetVideoYear(pc.CurrentMediaItem)
        }
      };
      return movieScrobbleData;
    }

    private TraktScrobbleEpisode CreateEpisodeData(IPlayerContext pc)
    {
      var episodeScrobbleData = new TraktScrobbleEpisode
      {
        Episode = new TraktEpisode
        {
          Ids = new TraktEpisodeId
          {
            Tvdb = GetSeriesTvdbId(pc.CurrentMediaItem),
            Imdb = GetSeriesImdbId(pc.CurrentMediaItem)
          },
          Title = GetSeriesTitle(pc.CurrentMediaItem),
          Season = GetSeasonIndex(pc.CurrentMediaItem),
          Number = GetEpisodeIndex(pc.CurrentMediaItem)
        },
        Show = new TraktShow
        {
          Ids = new TraktShowId
          {
            Tvdb = GetSeriesTvdbId(pc.CurrentMediaItem),
            Imdb = GetSeriesImdbId(pc.CurrentMediaItem)
          },
          Title = GetSeriesTitle(pc.CurrentMediaItem),
          Year = GetVideoYear(pc.CurrentMediaItem)
        }
      };

      return episodeScrobbleData;
    }

    private void StartScrobble()
    {
      ISettingsManager settingsManager = ServiceRegistration.Get<ISettingsManager>();
      TraktSettings settings = settingsManager.Load<TraktSettings>();

      if (string.IsNullOrEmpty(settings.TraktOAuthToken))
      {
        TraktLogger.Error("0Auth Token not available");
        return;
      }

      if (!Login())
      {
        return;
      }

      if (_dataMovie.Movie != null)
      {
        _dataMovie.Progress = 0;
        var response = TraktAPI.StartMovieScrobble(_dataMovie);
        TraktLogger.LogTraktResponse(response);
        return;
      }
      if (_dataEpisode != null)
      {
        _dataEpisode.Progress = 0;
        var response = TraktAPI.StartEpisodeScrobble(_dataEpisode);
        TraktLogger.LogTraktResponse(response);
        return;
      }
      TraktLogger.Info("Can't start scrobble, scrobbledata not available");
    }

    private void StopScrobble()
    {
      if (_dataMovie.Movie != null)
      {
        _dataMovie.Progress = _progress;
        var response = TraktAPI.StopMovieScrobble(_dataMovie);
        TraktLogger.LogTraktResponse(response);
        return;
      }
      if (_dataEpisode != null)
      {
        _dataEpisode.Progress = _progress;
        var response = TraktAPI.StopEpisodeScrobble(_dataEpisode);
        TraktLogger.LogTraktResponse(response);
        return;
      }
      TraktLogger.Info("Can't post stop scrobble, scrobbledata lost");
    }

    private bool Login()
    {
      ISettingsManager settingsManager = ServiceRegistration.Get<ISettingsManager>();
      TraktSettings settings = settingsManager.Load<TraktSettings>();

      TraktLogger.Info("Exchanging refresh-token for access-token");
      var response = TraktAuth.GetOAuthToken(settings.TraktOAuthToken);
      if (response == null || string.IsNullOrEmpty(response.AccessToken))
      {
        TraktLogger.Error("Unable to login to trakt");
        return false;
      }
      settings.TraktOAuthToken = response.RefreshToken;
      settingsManager.Save(settings);
      TraktLogger.Info("Successfully logged in");

      return true;
    }

    private string GetMovieImdb(MediaItem currMediaItem)
    {
      string value;
      return MediaItemAspect.TryGetAttribute(currMediaItem.Aspects, MovieAspect.ATTR_IMDB_ID, out value) ? value : null;
    }

    private int GetMovieTmdb(MediaItem currMediaItem)
    {
      int iValue;
      return MediaItemAspect.TryGetAttribute(currMediaItem.Aspects, MovieAspect.ATTR_TMDB_ID, out iValue) ? iValue : 0;
    }

    private int GetVideoYear(MediaItem currMediaItem)
    {
      DateTime dtValue;
      if (MediaItemAspect.TryGetAttribute(currMediaItem.Aspects, MediaAspect.ATTR_RECORDINGTIME, out dtValue))
        return dtValue.Year;

      return 0;
    }

    private string GetSeriesTitle(MediaItem currMediaItem)
    {
      string value;
      return MediaItemAspect.TryGetAttribute(currMediaItem.Aspects, SeriesAspect.ATTR_SERIESNAME, out value) ? value : null;
    }

    private int GetSeriesTvdbId(MediaItem currMediaItem)
    {
      int value;
      return MediaItemAspect.TryGetAttribute(currMediaItem.Aspects, SeriesAspect.ATTR_TVDB_ID, out value) ? value : 0;
    }

    private int GetSeasonIndex(MediaItem currMediaItem)
    {
      int value;
      return MediaItemAspect.TryGetAttribute(currMediaItem.Aspects, SeriesAspect.ATTR_SEASON, out value) ? value : 0;
    }

    private int GetEpisodeIndex(MediaItem currMediaItem)
    {
      List<int> intList;
      if (MediaItemAspect.TryGetAttribute(currMediaItem.Aspects, SeriesAspect.ATTR_EPISODE, out intList) && intList.Any())
        return intList.First(); // TODO: multi episode files?!

      return intList.FirstOrDefault();
    }

    private string GetSeriesImdbId(MediaItem currMediaItem)
    {
      string value;
      return MediaItemAspect.TryGetAttribute(currMediaItem.Aspects, SeriesAspect.ATTR_IMDB_ID, out value) ? value : null;
    }

    private string GetMovieTitle(MediaItem currMediaItem)
    {
      string value;
      return MediaItemAspect.TryGetAttribute(currMediaItem.Aspects, MovieAspect.ATTR_MOVIE_NAME, out value) ? value : null;
    }

    public void Dispose()
    {
      _settings.Dispose();
      UnsubscribeFromMessages();
    }
  }
}
