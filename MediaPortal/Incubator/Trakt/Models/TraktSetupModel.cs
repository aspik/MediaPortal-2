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
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using MediaPortal.Common;
using MediaPortal.Common.General;
using MediaPortal.Common.Logging;
using MediaPortal.Common.MediaManagement;
using MediaPortal.Common.MediaManagement.DefaultItemAspects;
using MediaPortal.Common.MediaManagement.MLQueries;
using MediaPortal.Common.Settings;
using MediaPortal.Common.Threading;
using MediaPortal.Extensions.OnlineLibraries.Libraries.Trakt;
using MediaPortal.Extensions.OnlineLibraries.Libraries.Trakt.DataStructures;
using MediaPortal.Extensions.OnlineLibraries.Libraries.Trakt.Extension;
using MediaPortal.UiComponents.Trakt.Service;
using MediaPortal.UI.Presentation.Models;
using MediaPortal.UI.Presentation.Workflow;
using MediaPortal.UI.ServerCommunication;
using MediaPortal.Utilities;
using TraktSettings = MediaPortal.UiComponents.Trakt.Settings.TraktSettings;
using MediaPortal.Extensions.OnlineLibraries.Libraries.Trakt.Enums;

namespace MediaPortal.UiComponents.Trakt.Models
{
  public class TraktSetupModel : IWorkflowModel
  {
    #region Consts

    public const string TRAKT_SETUP_MODEL_ID_STR = "65E4F7CA-3C9C-4538-966D-2A896BFEF4D3";
    public const string SERIES_BANNER_URL = "http://trakt.tv/user/{0}/widgets/watched/episode-thin-banner@2x.jpg";
    public const string MOVIES_BANNER_URL = "http://trakt.tv/user/{0}/widgets/watched/movie-thin-banner@2x.jpg";

    public static readonly Guid TRAKT_SETUP_MODEL_ID = new Guid(TRAKT_SETUP_MODEL_ID_STR);

    private const string ApplicationId = "49e6907e6221d3c7e866f9d4d890c6755590cf4aa92163e8490a17753b905e57";
    private const string ApplicationIdStaging = "adafedb5cd065e6abeb9521b8b64bc66adb010a7c08128811bf32c989f35b77a ";

    #endregion

    #region Protected fields

    protected readonly AbstractProperty _isEnabledProperty = new WProperty(typeof(bool), false);
    protected readonly AbstractProperty _isSynchronizingProperty = new WProperty(typeof(bool), false);
    protected readonly AbstractProperty _usermameProperty = new WProperty(typeof(string), null);
    protected readonly AbstractProperty _passwordProperty = new WProperty(typeof(string), null);
    protected readonly AbstractProperty _testStatusProperty = new WProperty(typeof(string), string.Empty);
    protected readonly AbstractProperty _seriesBannerProperty = new WProperty(typeof(string), string.Empty);
    protected readonly AbstractProperty _moviesBannerProperty = new WProperty(typeof(string), string.Empty);

    #endregion

    #region Public properties - Bindable Data

    public AbstractProperty IsEnabledProperty
    {
      get { return _isEnabledProperty; }
    }

    public bool IsEnabled
    {
      get { return (bool)_isEnabledProperty.GetValue(); }
      set { _isEnabledProperty.SetValue(value); }
    }

    public AbstractProperty IsSynchronizingProperty
    {
      get { return _isSynchronizingProperty; }
    }

    public bool IsSynchronizing
    {
      get { return (bool)_isSynchronizingProperty.GetValue(); }
      set { _isSynchronizingProperty.SetValue(value); }
    }

    public AbstractProperty UsernameProperty
    {
      get { return _usermameProperty; }
    }

    public string Username
    {
      get { return (string)_usermameProperty.GetValue(); }
      set { _usermameProperty.SetValue(value); }
    }

    public AbstractProperty PasswordProperty
    {
      get { return _passwordProperty; }
    }

    public string Password
    {
      get { return (string)_passwordProperty.GetValue(); }
      set { _passwordProperty.SetValue(value); }
    }

    public AbstractProperty TestStatusProperty
    {
      get { return _testStatusProperty; }
    }

    public string TestStatus
    {
      get { return (string)_testStatusProperty.GetValue(); }
      set { _testStatusProperty.SetValue(value); }
    }

    public AbstractProperty SeriesBannerProperty
    {
      get { return _seriesBannerProperty; }
    }

    public string SeriesBanner
    {
      get { return (string)_seriesBannerProperty.GetValue(); }
      set { _seriesBannerProperty.SetValue(value); }
    }

    public AbstractProperty MoviesBannerProperty
    {
      get { return _moviesBannerProperty; }
    }

    public string MoviesBanner
    {
      get { return (string)_moviesBannerProperty.GetValue(); }
      set { _moviesBannerProperty.SetValue(value); }
    }

    #endregion

    #region Public methods - Commands

    /// <summary>
    /// Saves the current state to the settings file.
    /// </summary>
    public void SaveSettings()
    {
      ISettingsManager settingsManager = ServiceRegistration.Get<ISettingsManager>();
      TraktSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<TraktSettings>();
      settings.EnableTrakt = IsEnabled;
      settings.Username = Username;
      settings.Password = Password;
   //   settings.Authentication = new TraktAuthentication { Username = Username, Password = Password };
      // Save
      settingsManager.Save(settings);
    }

    /// <summary>
    /// Uses the current accound information and tries to validate them at trakt.
    /// </summary>
    public void TestAccount()
    {

     var account = new TraktAuthentication
      {
        Username = this.Username,
        Password = this.Password
      };

      TraktUserToken response = null;
      response = TraktAPI.Login(account.ToJSON());

      if (response == null || string.IsNullOrEmpty(response.Token))
      {
        // erro

      }
      else
      {
        // Save User Token
        TraktAPI.UserToken = response.Token;

        TraktActivity ac = new TraktActivity();
        TraktUserStatistics stat = TraktAPI.GetUserStatistics(Username);
        TraktUserSummary sum = TraktAPI.GetUserProfile(Username);
        
      }

       

      //try
      //{
      //  TraktResponse result = TraktAPI.TestAccount(new TraktAccount { Username = Username, Password = Password });
      //  if (!string.IsNullOrWhiteSpace(result.Error))
      //    TestStatus = result.Error;
      //  else if (!string.IsNullOrWhiteSpace(result.Message))
      //    TestStatus = result.Message;
      //  else
      //    TestStatus = string.Empty;
      //  BuildBannerUrls();
      //}
      //catch (Exception ex)
      //{
      //  TestStatus = "Error";
      //  ServiceRegistration.Get<ILogger>().Error("Trakt.tv: Exception while testing account.", ex);
      //}
    }

    public void SyncMediaToTrakt()
    {
     // SyncTest();

      SyncMovies();

      //if (!IsSynchronizing)
      //{
      //  IsSynchronizing = true;
      //  IThreadPool threadPool = ServiceRegistration.Get<IThreadPool>();
      //  threadPool.Add(SyncMediaToTrakt_Async, ThreadPriority.BelowNormal);
      //}
    }

    public void SyncMediaToTrakt_Async()
    {
      if (SyncMovies() && SyncSeries())
      {
        TestStatus = "[Trakt.SyncFinished]";
      }
      IsSynchronizing = false;
      BuildBannerUrls();
    }

    public bool SyncTest()
    {
      try
      {
        TestStatus = "[Trakt.SyncMovies]";
        Guid[] types = { MediaAspect.ASPECT_ID, MovieAspect.ASPECT_ID, VideoAspect.ASPECT_ID, ImporterAspect.ASPECT_ID };
        var contentDirectory = ServiceRegistration.Get<IServerConnectionManager>().ContentDirectory;
        if (contentDirectory == null)
        {
          TestStatus = "[Trakt.MediaLibraryNotConnected]";
          return false;
        }
        var collectedMovies = contentDirectory.Search(new MediaItemQuery(types, null, null), true);

        var syncCollectedMovies = new List<TraktSyncMovieCollected>();
        //  TraktLogger.Info("Finding movies to add to trakt.tv collection");
        syncCollectedMovies.Add(new TraktSyncMovieCollected()
        {
          Ids = new TraktMovieId() { Imdb = ToMovie(collectedMovies[1]).Ids.Imdb, Tmdb = ToMovie(collectedMovies[1]).Ids.Tmdb },
         // Title = ToMovie(collectedMovies[0]).Title,
        //  Year = ToMovie(collectedMovies[0]).Year,
          AudioCodec = GetMovieAudioCodec(collectedMovies[0]),
          AudioChannels = "5.1",
          MediaType = "digital",
          Resolution = "hd_720p",
          CollectedAt = "2015-10-08T16:20:35Z",
          Is3D = false

        });



        int pageSize = 100;
        int pages = (int)Math.Ceiling((double)syncCollectedMovies.Count / pageSize);

        for (int i = 0; i < pages; i++)
        {

          var pagedMovies = syncCollectedMovies.Skip(i * pageSize).Take(pageSize).ToList();
          var response = TraktAPI.AddMoviesToCollecton(new TraktSyncMoviesCollected { Movies = pagedMovies });

        }
        



      



          
          
        
        return true;
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("Trakt.tv: Exception while synchronizing media library.", ex);
      }
      return false;
    }

    public bool SyncMovies()
    {
      try
      {
        TestStatus = "[Trakt.SyncMovies]";
        Guid[] types = { MediaAspect.ASPECT_ID, MovieAspect.ASPECT_ID, VideoAspect.ASPECT_ID };
        var contentDirectory = ServiceRegistration.Get<IServerConnectionManager>().ContentDirectory;
        if (contentDirectory == null)
        {
          TestStatus = "[Trakt.MediaLibraryNotConnected]";
          return false;
        }
        var collectedMovies = contentDirectory.Search(new MediaItemQuery(types, null, null), true);

        var syncCollectedMovies = new List<TraktSyncMovieCollected>();
        //  TraktLogger.Info("Finding movies to add to trakt.tv collection");
        //syncCollectedMovies.Add(new TraktSyncMovieCollected
        //{
        //  Ids = new TraktMovieId { Imdb = ToMovie(collectedMovies[0]).Ids.Imdb, Tmdb = ToMovie(collectedMovies[0]).Ids.Tmdb },
        //  Title = ToMovie(collectedMovies[0]).Title,
        //  Year = ToMovie(collectedMovies[0]).Year,
        //});

        syncCollectedMovies = (from movie in collectedMovies
                                 //  where !traktCollectedMovies.ToList().Exists(c => MovieMatch(movie, c.Movie))
                               select new TraktSyncMovieCollected
                               {
                                 Ids = new TraktMovieId { Imdb = GetImdbId(movie), Tmdb = GetTmdbId(movie) },
                                 CollectedAt = GetDateAddedToDb(movie),
                                 MediaType = GetMovieMediaType(movie),
                                 Resolution = GetMovieResolution(movie),
                                 AudioCodec = GetMovieAudioCodec(movie),
                                 AudioChannels = GetMovieChannelsNumber(movie),
                                 Is3D = false // IsMovie3D(movie)
                               }).ToList();

        //TraktLogger.Info("Finding movies to add to trakt.tv collection");
        //var syncCollectedMovies = movies.Select(movie => new TraktSyncMovieCollected
        //{
        //  Ids = new TraktMovieId { Imdb = ToMovie(movie).Ids.Imdb, Tmdb = ToMovie(movie).Ids.Tmdb },
        //  Title = ToMovie(movie).Title,
        //  Year = ToMovie(movie).Year,
        //  //TODO
        //  //add "AudioChannels", "AudiCodec", etc...
        //}).ToList();

        //    TraktLogger.Info("Adding {0} movies to trakt.tv watched history", syncCollectedMovies.Count);

        if (syncCollectedMovies.Count > 0)
        {
          //update cache
        //  TraktCache.AddMoviesToCollection(syncCollectedMovies);
          int pageSize = 100;
          int pages = (int)Math.Ceiling((double)syncCollectedMovies.Count / pageSize);
          for (int i = 0; i < pages; i++)
          {
         //   TraktLogger.Info("Adding movies [{0}/{1}] to trakt.tv collection", i + 1, pages);

            var pagedMovies = syncCollectedMovies.Skip(i * pageSize).Take(pageSize).ToList();

            //   pagedMovies.ForEach(s => TraktLogger.Info("Adding movie to trakt.tv collection. Title = '{0}', Year = '{1}', IMDb ID = '{2}', TMDb ID = '{3}', Date Added = '{4}', MediaType = '{5}', Resolution = '{6}', Audio Codec = '{7}', Audio Channels = '{8}'",
            //                                    s.Title, s.Year.HasValue ? s.Year.ToString() : "<empty>", s.Ids.Imdb ?? "<empty>", s.Ids.Tmdb.HasValue ? s.Ids.Tmdb.ToString() : "<empty>",
            //                                   s.CollectedAt, s.MediaType ?? "<empty>", s.Resolution ?? "<empty>", s.AudioCodec ?? "<empty>", s.AudioChannels ?? "<empty>"));

            //// remove title/year such that match against online ID only
            if (Extensions.OnlineLibraries.Libraries.Trakt.TraktSettings.SkipMoviesWithNoIdsOnSync)
            {
              pagedMovies.ForEach(m =>
              {
                m.Title = null;
                m.Year = null;
              });
            }

            var response = TraktAPI.AddMoviesToCollecton(new TraktSyncMoviesCollected { Movies = pagedMovies });
         //   TraktLogger.LogTraktResponse(response);

            // remove movies from cache which didn't succeed
            if (response != null && response.NotFound != null && response.NotFound.Movies.Count > 0)
            {
            //  TraktCache.RemoveMoviesFromCollection(response.NotFound.Movies);
            }
          }
        }

        //var syncWatchedMovies = collectedMovies.Where(IsWatched).Select(movie => new TraktSyncMovieWatched
        //{
        //  Ids = new TraktMovieId { Imdb = ToMovie(movie).Ids.Imdb, Tmdb = ToMovie(movie).Ids.Tmdb },
        //  Title = ToMovie(movie).Title,
        //  Year = ToMovie(movie).Year
        //  // TODO
        //  // add "watched add" 
        //}).ToList();

        //if (syncWatchedMovies.Count > 0)
        //{
        //  // update internal cache
        //  TraktCache.AddMoviesToWatchHistory(syncWatchedMovies);
        //  int pageSize = Extensions.OnlineLibraries.Libraries.Trakt.TraktSettings.SyncBatchSize;
        //  int pages = (int)Math.Ceiling((double)syncWatchedMovies.Count / pageSize);
        //  for (int i = 0; i < pages; i++)
        //  {
        //    TraktLogger.Info("Adding movies [{0}/{1}] to trakt.tv watched history", i + 1, pages);

        //    var pagedMovies = syncWatchedMovies.Skip(i * pageSize).Take(pageSize).ToList();

        //    pagedMovies.ForEach(s => TraktLogger.Info("Adding movie to trakt.tv watched history. Title = '{0}', Year = '{1}', IMDb ID = '{2}', TMDb ID = '{3}', Date Watched = '{4}'",
        //                                                                     s.Title, s.Year.HasValue ? s.Year.ToString() : "<empty>", s.Ids.Imdb ?? "<empty>", s.Ids.Tmdb.HasValue ? s.Ids.Tmdb.ToString() : "<empty>", s.WatchedAt));

        //    // remove title/year such that match against online ID only
        //    if (Extensions.OnlineLibraries.Libraries.Trakt.TraktSettings.SkipMoviesWithNoIdsOnSync)
        //    {
        //      pagedMovies.ForEach(m =>
        //      {
        //        m.Title = null;
        //        m.Year = null;
        //      });
        //    }

        //    var response = TraktAPI.AddMoviesToWatchedHistory(new TraktSyncMoviesWatched { Movies = pagedMovies });

        //    TraktLogger.LogTraktResponse<TraktSyncResponse>(response);

        //    // remove movies from cache which didn't succeed
        //    if (response != null && response.NotFound != null && response.NotFound.Movies.Count > 0)
        //    {
        //      TraktCache.RemoveMoviesFromWatchHistory(response.NotFound.Movies);
        //    }
        //  }
        //}
        return true;
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("Trakt.tv: Exception while synchronizing media library.", ex);
      }
      return false;
    }

    public bool SyncSeries()
    {

      TraktLogger.Info("Series Library Starting Sync");

      // store list of series ids so we can update the episode counts
      // of any series that syncback watched flags
      var seriesToUpdateEpisodeCounts = new HashSet<int>();

      #region Get online data from cache

      #region UnWatched / Watched

      List<TraktCache.EpisodeWatched> traktWatchedEpisodes = null;

      // get all episodes on trakt that are marked as 'unseen'
      var traktUnWatchedEpisodes = TraktCache.GetUnWatchedEpisodesFromTrakt().ToNullableList();
      if (traktUnWatchedEpisodes == null)
      {
        TraktLogger.Error("Error getting tv shows unwatched from trakt.tv server, unwatched and watched sync will be skipped");
      }
      else
      {
        TraktLogger.Info("Found {0} unwatched tv episodes in trakt.tv library", traktUnWatchedEpisodes.Count());

        // now get all episodes on trakt that are marked as 'seen' or 'watched' (this will be cached already when working out unwatched)
        traktWatchedEpisodes = TraktCache.GetWatchedEpisodesFromTrakt().ToNullableList();
        if (traktWatchedEpisodes == null)
        {
          TraktLogger.Error("Error getting tv shows watched from trakt.tv server, watched sync will be skipped");
        }
        else
        {
          TraktLogger.Info("Found {0} watched tv episodes in trakt.tv library", traktWatchedEpisodes.Count());
        }
      }

      #endregion

      #region Collection

      // get all episodes on trakt that are marked as in 'collection'
      var traktCollectedEpisodes = TraktCache.GetCollectedEpisodesFromTrakt().ToNullableList();
      if (traktCollectedEpisodes == null)
      {
        TraktLogger.Error("Error getting tv episode collection from trakt.tv server");
      }
      else
      {
        TraktLogger.Info("Found {0} tv episodes in trakt.tv collection", traktCollectedEpisodes.Count());
      }

      #endregion

      #region Ratings

      #region Episodes

      var traktRatedEpisodes = TraktCache.GetRatedEpisodesFromTrakt().ToNullableList();
      if (traktRatedEpisodes == null)
      {
        TraktLogger.Error("Error getting rated episodes from trakt.tv server");
      }
      else
      {
        TraktLogger.Info("Found {0} rated tv episodes in trakt.tv library", traktRatedEpisodes.Count());
      }

      #endregion

      #region Shows

      var traktRatedShows = TraktCache.GetRatedShowsFromTrakt().ToNullableList();
      if (traktRatedShows == null)
      {
        TraktLogger.Error("Error getting rated shows from trakt.tv server");
      }
      else
      {
        TraktLogger.Info("Found {0} rated tv shows in trakt.tv library", traktRatedShows.Count());
      }

      #endregion

      #region Seasons

      var traktRatedSeasons = TraktCache.GetRatedSeasonsFromTrakt().ToNullableList();
      if (traktRatedSeasons == null)
      {
        TraktLogger.Error("Error getting rated seasons from trakt.tv server");
      }
      else
      {
        TraktLogger.Info("Found {0} rated tv seasons in trakt.tv library", traktRatedSeasons.Count());
      }

      #endregion

      #endregion

      #region Watchlist

      #region Shows

      var traktWatchlistedShows = TraktCache.GetWatchlistedShowsFromTrakt();
      if (traktWatchlistedShows == null)
      {
        TraktLogger.Error("Error getting watchlisted shows from trakt.tv server");
      }
      else
      {
        TraktLogger.Info("Found {0} watchlisted tv shows in trakt.tv library", traktWatchlistedShows.Count());
      }

      #endregion

      #region Seasons

      var traktWatchlistedSeasons = TraktCache.GetWatchlistedSeasonsFromTrakt();
      if (traktWatchlistedSeasons == null)
      {
        TraktLogger.Error("Error getting watchlisted seasons from trakt.tv server");
      }
      else
      {
        TraktLogger.Info("Found {0} watchlisted tv seasons in trakt.tv library", traktWatchlistedSeasons.Count());
      }

      #endregion

      #region Episodes

      var traktWatchlistedEpisodes = TraktCache.GetWatchlistedEpisodesFromTrakt();
      if (traktWatchlistedEpisodes == null)
      {
        TraktLogger.Error("Error getting watchlisted episodes from trakt.tv server");
      }
      else
      {
        TraktLogger.Info("Found {0} watchlisted tv episodes in trakt.tv library", traktWatchlistedEpisodes.Count());
      }

      #endregion

      #endregion

      #endregion

      try
      {
        TestStatus = "[Trakt.SyncSeries]";
        Guid[] types = { MediaAspect.ASPECT_ID, SeriesAspect.ASPECT_ID };

        MediaItemQuery mediaItemQuery = new MediaItemQuery(types, null, null);
        var contentDirectory = ServiceRegistration.Get<IServerConnectionManager>().ContentDirectory;
        if (contentDirectory == null)
        {
          TestStatus = "[Trakt.MediaLibraryNotConnected]";
          return false;
        }

        var localEpisodes = contentDirectory.Search(mediaItemQuery, true);
       // int episodeCount = localEpisodes.Count;

        var syncCollectedShow = GetCollectedShowsForSyncEx(localEpisodes, traktCollectedEpisodes);

        //  var episodes = contentDirectory.Search(mediaItemQuery, true);
        int showCount = 0;
        int iSyncCounter = 0;
        showCount = syncCollectedShow.Shows.Count;
        foreach (var show in syncCollectedShow.Shows)
        {
          int showEpisodeCount = show.Seasons.Sum(s => s.Episodes.Count());
          TraktLogger.Info("Adding tv show [{0}/{1}] to trakt.tv episode collection, Episode Count = '{2}', Show Title = '{3}', Show Year = '{4}', Show TVDb ID = '{5}', Show IMDb ID = '{6}'",
            ++iSyncCounter, showCount, showEpisodeCount, show.Title, show.Year.HasValue ? show.Year.ToString() : "<empty>", show.Ids.Tvdb, show.Ids.Imdb ?? "<empty>");

          show.Seasons.ForEach(s => s.Episodes.ForEach(e =>
          {
            TraktLogger.Info("Adding episode to trakt.tv collection, Title = '{0} - {1}x{2}', Collected At = '{3}', Audio Channels = '{4}', Audio Codec = '{5}', Resolution = '{6}', Media Type = '{7}', Is 3D = '{8}'", show.Title, s.Number, e.Number, e.CollectedAt.ToLogString(), e.AudioChannels.ToLogString(), e.AudioCodec.ToLogString(), e.Resolution.ToLogString(), e.MediaType.ToLogString(), e.Is3D);
          }));

          // only sync one show at a time regardless of batch size in settings
          var pagedShows = new List<TraktSyncShowCollectedEx>();
          pagedShows.Add(show);

          var response = TraktAPI.AddShowsToCollectonEx(new TraktSyncShowsCollectedEx { Shows = pagedShows });
          TraktLogger.LogTraktResponse<TraktSyncResponse>(response);

          // only add to cache if it was a success
          if (response != null && response.Added != null && response.Added.Episodes == showEpisodeCount)
          {
            // update local cache
            TraktCache.AddEpisodesToCollection(show);
          }
          ///******************************************************

          //var episodes = contentDirectory.Search(mediaItemQuery, true);

          //var series = episodes.ToLookup(GetSeriesKey);
          //foreach (var serie in series)
          //{
          //  var imdbId = serie.Select(episode =>
          //  {
          //    string value;
          //    return MediaItemAspect.TryGetAttribute(episode.Aspects, SeriesAspect.ATTR_IMDB_ID, out value) ? value : null;
          //  }).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

          //  var tvdbId = serie.Select(episode =>
          //  {
          //    int value;
          //    return MediaItemAspect.TryGetAttribute(episode.Aspects, SeriesAspect.ATTR_TVDB_ID, out value) ? value : 0;
          //}).FirstOrDefault(value => value != 0);

          //  TraktEpisodeSync syncData = new TraktEpisodeSync
          //{
          //  UserName = Username,
          //  Password = Password,
          //  EpisodeList = new List<TraktEpisodeSync.Episode>(),
          //  Title = serie.Key,
          //  Year = serie.Min(e =>
          //  {
          //    int year;
          //    string seriesTitle;
          //    GetSeriesTitleAndYear(e, out seriesTitle, out year);
          //    return year;
          //  }).ToString()
          //};

        //  if (!string.IsNullOrWhiteSpace(imdbId))
        //    syncData.IMDBID = imdbId;

        //  if (tvdbId > 0)
        //    syncData.SeriesID = tvdbId.ToString();

        //  HashSet<TraktEpisodeSync.Episode> uniqueEpisodes = new HashSet<TraktEpisodeSync.Episode>();
        //  foreach (var episode in serie)
        //  {
        //    string seriesTitle;
        //    int year = 0;
        //    if (!GetSeriesTitle /*AndYear*/(episode, out seriesTitle /*, out year*/))
        //      continue;

        //    // First send all movies to Trakt that we have so they appear in library
        //    CollectionUtils.AddAll(uniqueEpisodes, ToSeries(episode));
        //  }
        //  syncData.EpisodeList = uniqueEpisodes.ToList();

        //  TraktSyncModes traktSyncMode = TraktSyncModes.library;
        ////  var response = TraktAPI.SyncEpisodeLibrary(syncData, traktSyncMode);
        //  ServiceRegistration.Get<ILogger>().Info("Trakt.tv: Series '{0}' '{1}': {2}{3}", syncData.Title, traktSyncMode, response.Message, response.Error);

        //  // Then send only the watched movies as "seen"
        //  uniqueEpisodes.Clear();
        //  foreach (var seenEpisode in episodes.Where(IsWatched))
        //    CollectionUtils.AddAll(uniqueEpisodes, ToSeries(seenEpisode));
        //  syncData.EpisodeList = uniqueEpisodes.ToList();

        //  traktSyncMode = TraktSyncModes.seen;
        //  response = TraktAPI.SyncEpisodeLibrary(syncData, traktSyncMode);
          //ServiceRegistration.Get<ILogger>().Info("Trakt.tv: Series '{0}' '{1}': {2}{3}", syncData.Title, traktSyncMode, response.Message, response.Error);
          return true;
        }
      }
      catch (Exception ex)
      {
        ServiceRegistration.Get<ILogger>().Error("Trakt.tv: Exception while synchronizing media library.", ex);
      }
      return false;
    }

    /// <summary>
    /// Returns a list of shows for collection sync as show objects with season / episode hierarchy
    /// </summary>
    private TraktSyncShowsCollectedEx GetCollectedShowsForSyncEx(IList<MediaItem> localCollectedEpisodes, List<TraktCache.EpisodeCollected> traktEpisodesCollected)
    {
      TraktLogger.Info("Finding local episodes to add to trakt.tv collection");

      // prepare new sync object
      var syncCollectedEpisodes = new TraktSyncShowsCollectedEx();
      syncCollectedEpisodes.Shows = new List<TraktSyncShowCollectedEx>();

      var episodes = localCollectedEpisodes;

      // create a unique key to lookup and search for faster
      var onlineEpisodes = traktEpisodesCollected.ToLookup(tce => CreateLookupKey(tce), tce => tce);

      foreach (var episode in episodes)
      {
        string tvdbKey = CreateLookupKey(episode);

        var traktEpisode = onlineEpisodes[tvdbKey].FirstOrDefault();

        // check if not collected on trakt and add it to sync list
        if (traktEpisode == null)
        {
          // check if we already have the show added to our sync object
          var syncShow = syncCollectedEpisodes.Shows.FirstOrDefault(sce => sce.Ids != null && sce.Ids.Tvdb == GetSeriesTvdbId(episode));
          if (syncShow == null)
          {
            // get show data from episode
           // var show = Helper.getCorrespondingSeries(episode[DBOnlineEpisode.cSeriesID]);
            var show = GetSeriesTvdbId(episode);
            if (show == 0) continue;

            // create new show
            syncShow = new TraktSyncShowCollectedEx
            {
              Ids = new TraktShowId
              {
                Tvdb = GetSeriesTvdbId(episode),
                Imdb = GetSeriesImdbId(episode)
              },
              Title = GetSeriesTitle(episode),
            //  Year = show.Year.ToNullableInt32()
            };

            // add a new season collection to show object
            syncShow.Seasons = new List<TraktSyncShowCollectedEx.Season>();

            // add show to the collection
            syncCollectedEpisodes.Shows.Add(syncShow);
          }

          // check if season exists in show sync object
          var syncSeason = syncShow.Seasons.FirstOrDefault(ss => ss.Number == GetSeasonIndex(episode));
          if (syncSeason == null)
          {
            // create new season
            syncSeason = new TraktSyncShowCollectedEx.Season
            {
              Number = GetSeasonIndex(episode)
            };

            // add a new episode collection to season object
            syncSeason.Episodes = new List<TraktSyncShowCollectedEx.Season.Episode>();

            // add season to the show
            syncShow.Seasons.Add(syncSeason);
          }

          // add episode to season
          syncSeason.Episodes.Add(new TraktSyncShowCollectedEx.Season.Episode
          {
            Number = GetEpisodeIndex(episode),
            //CollectedAt = episode[DBEpisode.cFileDateAdded].ToString().ToISO8601(0, true),
            //MediaType = GetEpisodeMediaType(episode),
            //Resolution = GetEpisodeResolution(episode),
            //AudioCodec = GetEpisodeAudioCodec(episode),
            //AudioChannels = GetEpisodeAudioChannels(episode),
            Is3D = false
          });
        }
      }

      return syncCollectedEpisodes;
    }

    //foreach (var serie in series)
    //{

    //  string tvdbKey = CreateLookupKey(ToEpisode());

    //  var imdbId = serie.Select(episode =>
    //  {
    //    string value;
    //    return MediaItemAspect.TryGetAttribute(episode.Aspects, SeriesAspect.ATTR_IMDB_ID, out value) ? value : null;
    //  }).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    //var tvdbId = serie.Select(episode =>
    //{
    //  int value;
    //  return MediaItemAspect.TryGetAttribute(episode.Aspects, SeriesAspect.ATTR_TVDB_ID, out value) ? value : 0;
    //}).FirstOrDefault(value => value != 0);

    //  var syncShow = new TraktSyncShowCollectedEx
    //  {
    //    Ids = new TraktShowId
    //    {
    //      Tvdb = tvdbId,
    //      Imdb = imdbId
    //    },
    //    Title = serie.Key,
    //    Year = serie.Min(e =>
    //    {
    //      int year;
    //      string seriesTitle;
    //      GetSeriesTitleAndYear(e, out seriesTitle, out year);
    //      return year;
    //    })
    //  };
    //  // add a new season collection to show object
    //  syncShow.Seasons = new List<TraktSyncShowCollectedEx.Season>();

    //  // add show to the collection
    //  syncCollectedEpisodes.Shows.Add(syncShow);
    //}


    /// <summary>
    /// Returns a list of shows for watched history sync as show objects with season / episode hierarchy
    /// </summary>
    private TraktSyncShowsWatchedEx GetWatchedShowsForSyncEx(IList<MediaItem> localCollectedEpisodes, List<TraktCache.EpisodeWatched> traktEpisodesWatched)
    {
      TraktLogger.Info("Finding local episodes to add to trakt.tv watched history");

      // prepare new sync object
      var syncWatchedEpisodes = new TraktSyncShowsWatchedEx();
      syncWatchedEpisodes.Shows = new List<TraktSyncShowWatchedEx>();

      //foreach (var episode in episodes)
      //{
      //  string tvdbKey = CreateLookupKey(episode);

      //  var traktEpisode = onlineEpisodes[tvdbKey].FirstOrDefault();

      //  // check if not watched on trakt and add it to sync list
      //  if (traktEpisode == null)
      //  {
      //    // check if we already have the show added to our sync object
      //    var syncShow = syncWatchedEpisodes.Shows.FirstOrDefault(swe => swe.Ids != null && swe.Ids.Tvdb == episode[DBOnlineEpisode.cSeriesID]);
      //    if (syncShow == null)
      //    {
      //      // get show data from episode
      //      var show = Helper.getCorrespondingSeries(episode[DBOnlineEpisode.cSeriesID]);
      //      if (show == null) continue;

      //      // create new show
      //      syncShow = new TraktSyncShowWatchedEx
      //      {
      //        Ids = new TraktShowId
      //        {
      //          Tvdb = show[DBSeries.cID],
      //          Imdb = BasicHandler.GetProperImdbId(show[DBOnlineSeries.cIMDBID])
      //        },
      //        Title = show[DBOnlineSeries.cOriginalName],
      //        Year = show.Year.ToNullableInt32()
      //      };

      //      // add a new season collection to show object
      //      syncShow.Seasons = new List<TraktSyncShowWatchedEx.Season>();

      //      // add show to the collection
      //      syncWatchedEpisodes.Shows.Add(syncShow);
      //    }

      //    // check if season exists in show sync object
      //    var syncSeason = syncShow.Seasons.FirstOrDefault(ss => ss.Number == episode[DBOnlineEpisode.cSeasonIndex]);
      //    if (syncSeason == null)
      //    {
      //      // create new season
      //      syncSeason = new TraktSyncShowWatchedEx.Season
      //      {
      //        Number = episode[DBOnlineEpisode.cSeasonIndex]
      //      };

      //      // add a new episode collection to season object
      //      syncSeason.Episodes = new List<TraktSyncShowWatchedEx.Season.Episode>();

      //      // add season to the show
      //      syncShow.Seasons.Add(syncSeason);
      //    }

      //    // add episode to season
      //    syncSeason.Episodes.Add(new TraktSyncShowWatchedEx.Season.Episode
      //    {
      //      Number = episode[DBOnlineEpisode.cEpisodeIndex],
      //      WatchedAt = GetLastPlayedDate(episode)
      //    });
      //  }
      //}

      return syncWatchedEpisodes;
    }

    private void BuildBannerUrls()
    {
      if (string.IsNullOrEmpty(Username))
      {
        SeriesBanner = MoviesBanner = string.Empty;
        return;
      }
      string noCache = "?nocache=" + DateTime.Now.Ticks;
      SeriesBanner = string.Format(SERIES_BANNER_URL, Username) + noCache;
      MoviesBanner = string.Format(MOVIES_BANNER_URL, Username) + noCache;
    }

    private static bool IsWatched(MediaItem mediaItem)
    {
      int playCount;
      return (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, MediaAspect.ATTR_PLAYCOUNT, 0, out playCount) && playCount > 0);
    }

    private static int SafeCount(IList list)
    {
      return list != null ? list.Count : 0;
    }

  
    private TraktMovie ToMovie(MediaItem mediaItem)
    {
      string title;
      string imb;
      int tmdb = 0;
      int high;
      int width;
      string audioenc;
      DateTime col;
      DateTime importred;
      DateTime dtValue;

      int channels;

      TraktMovie movie = new TraktMovie();

      TraktSyncMovieCollected mo = new TraktSyncMovieCollected();




      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, MovieAspect.ATTR_MOVIE_NAME, out title) && !string.IsNullOrWhiteSpace(title))
        movie.Title = title;

      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, MovieAspect.ATTR_IMDB_ID, out imb) && !string.IsNullOrWhiteSpace(imb))
        
      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, MovieAspect.ATTR_TMDB_ID, out tmdb) && tmdb > 0)

        if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, VideoAspect.ATTR_HEIGHT, out high))
          mo.Resolution = high.ToString();

      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, VideoAspect.ATTR_WIDTH, out width))

       if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, VideoAspect.ATTR_AUDIOSTREAMCOUNT, out channels))

      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, MediaAspect.ATTR_RECORDINGTIME, out col))
        
      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, ImporterAspect.ATTR_DATEADDED, out importred))

      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, MediaAspect.ATTR_RECORDINGTIME, out dtValue))
        movie.Year = dtValue.Year;

      movie.Ids = new TraktMovieId
      {
        Imdb = imb,
        Tmdb = tmdb,
      };

      return movie;
    }

    private TraktCache.Episode ToEpisode(MediaItem mediaItem)
    {
      int seriesIndex;
      int episodeIndex;

      TraktCache.Episode episode = new TraktCache.Episode();

      if (!MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_SEASON, out seriesIndex))
        return episode;

      if (!MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_EPISODE, out episodeIndex))
        return episode;

      episode.Season = seriesIndex;
      episode.Number = episodeIndex;
      return episode;
    }

    //private List<TraktEpisodeSync.Episode> ToSeries(MediaItem mediaItem)
    //{
    //  int seriesIndex;
    //  List<int> episodeList;

    //  List<TraktEpisodeSync.Episode> episodes = new List<TraktEpisodeSync.Episode>();

    //  if (!MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_SEASON, out seriesIndex))
    //    return episodes;

    //  if (!MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_EPISODE, out episodeList))
    //    return episodes;

    //  foreach (var episode in episodeList)
    //    episodes.Add(new TraktEpisodeSync.Episode { SeasonIndex = seriesIndex.ToString(), EpisodeIndex = episode.ToString() });

    //  return episodes;
    //}

    private string GetSeriesKey(MediaItem mediaItem)
    {
      string series;
      //int year;
      if (!GetSeriesTitle(mediaItem, out series))
        return string.Empty;

      return string.Format("{0}", series);
      //return string.Format("{0} ({1})", series, year);
    }

    private static bool GetSeriesTitleAndYear(MediaItem mediaItem, out string series, out int year)
    {
      DateTime dtFirstAired;
      year = 0;

      if (!MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_SERIESNAME, out series) || string.IsNullOrWhiteSpace(series))
        return false;

      if (!MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_FIRSTAIRED, out dtFirstAired))
        return false;

      year = dtFirstAired.Year;
      return true;
    }

    private string GetImdbId(MediaItem mediaItem)
    {
      string imdb;
      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, MovieAspect.ATTR_IMDB_ID, out imdb) && !string.IsNullOrWhiteSpace(imdb))
        return imdb;
      return "";
    }

    private int GetTmdbId(MediaItem mediaItem)
    {
      int tmdb;
      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, MovieAspect.ATTR_TMDB_ID, out tmdb) && tmdb > 0)
        return tmdb;
      return 0;
    }

    private string GetDateAddedToDb(MediaItem mediaItem)
    {
      DateTime addedToDb;
      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, ImporterAspect.ATTR_DATEADDED, out addedToDb))
        return addedToDb.ToUniversalTime().ToISO8601();
      return "";
    }

    /// <summary>
    /// Gets the trakt compatible string for the movies Audio
    /// </summary>
    private string GetMovieAudioCodec(MediaItem mediaItem)
    {
      string audioCodec;

      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, VideoAspect.ATTR_AUDIOENCODING, out audioCodec) && !string.IsNullOrWhiteSpace(audioCodec))
      {
        switch (audioCodec.ToLowerInvariant())
        {
          case "truehd":
            return TraktAudio.dolby_truehd.ToString();
          case "dts":
            return TraktAudio.dts.ToString();
          case "dtshd":
            return TraktAudio.dts_ma.ToString();
          case "ac3":
            return TraktAudio.dolby_digital.ToString();
          case "aac":
            return TraktAudio.aac.ToString();
          case "mp2":
            return TraktAudio.mp3.ToString();
          case "pcm":
            return TraktAudio.lpcm.ToString();
          case "ogg":
            return TraktAudio.ogg.ToString();
          case "wma":
            return TraktAudio.wma.ToString();
          case "flac":
            return TraktAudio.flac.ToString();
          default:
            return null;
        }
      }
      return null;
    }

    /// <summary>
    /// Gets the trakt compatible string for the movies Media Type
    /// </summary>
    private string GetMovieMediaType(MediaItem mediaItem)
    {
      bool isDvd;

      MediaItemAspect.TryGetAttribute(mediaItem.Aspects, VideoAspect.ATTR_ISDVD, out isDvd);

      if (isDvd)
        return TraktMediaType.dvd.ToString();

      return TraktMediaType.digital.ToString();
    }

    /// <summary>
    /// Gets the trakt compatible string for the movies Media Type
    /// </summary>
    private string GetMovieChannelsNumber(MediaItem mediaItem)
    {
      int chan;

      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, VideoAspect.ATTR_CHANNELS, out chan) && chan > 0)
      {
        return chan.ToString(); ;
      }

        

      return "";
    }

    /// <summary>
    /// Gets the trakt compatible string for the movies Resolution
    /// </summary>
    private string GetMovieResolution(MediaItem mediaItem)
    {
      int width;

      if (MediaItemAspect.TryGetAttribute(mediaItem.Aspects, VideoAspect.ATTR_WIDTH, out width) && width > 0)

        switch (width)
        {
          case 1920:
            return TraktResolution.hd_1080p.ToString();
          case 1280:
            return TraktResolution.hd_720p.ToString();
          case 720:
            return TraktResolution.sd_576p.ToString();
          case 640:
            return TraktResolution.sd_480p.ToString();
          case 2160:
            return TraktResolution.uhd_4k.ToString();
          default:
            return TraktResolution.hd_720p.ToString();
        }

      return null;
    }


    private static bool GetSeriesTitle(MediaItem mediaItem, out string series)
    {
      return MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_SERIESNAME, out series) && !string.IsNullOrWhiteSpace(series);
    }

    private string GetSeriesTitle(MediaItem mediaItem)
    {
      string value;
      return MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_SERIESNAME, out value) ? value : null;
    }

    private int GetSeriesTvdbId(MediaItem mediaItem)
    {
      int value;
      return MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_TVDB_ID, out value) ? value : 0;
    }

    private int GetSeasonIndex(MediaItem mediaItem)
    {
      int value;
      return MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_SEASON, out value) ? value : 0;
    }

    private int GetEpisodeIndex(MediaItem mediaItem)
    {
      int value;
      return MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_EPISODE, out value) ? value : 0;
    }

    private string GetSeriesImdbId(MediaItem mediaItem)
    {
      string value;
      return MediaItemAspect.TryGetAttribute(mediaItem.Aspects, SeriesAspect.ATTR_IMDB_ID, out value) ? value : null;
    }

    private string CreateLookupKey(MediaItem episode)
    {
      string tvDbId;
      string seasonIndex;
      string episodeIndex;

      if (!MediaItemAspect.TryGetAttribute(episode.Aspects, SeriesAspect.ATTR_TVDB_ID, out tvDbId))
        return tvDbId;

      if (!MediaItemAspect.TryGetAttribute(episode.Aspects, SeriesAspect.ATTR_SEASON, out seasonIndex))
        return seasonIndex;

      if (!MediaItemAspect.TryGetAttribute(episode.Aspects, SeriesAspect.ATTR_EPISODE, out episodeIndex))
        return episodeIndex;

      return string.Format("{0}_{1}_{2}", tvDbId, seasonIndex, episodeIndex);
    }

    private string CreateLookupKey(TraktCache.Episode episode)
    {
      string show = null;

      if (episode.ShowTvdbId != null)
      {
        show = episode.ShowTvdbId.Value.ToString();
      }
      else if (episode.ShowImdbId != null)
      {
        show = episode.ShowImdbId;
      }
      else
      {
        if (episode.ShowTitle == null)
          return episode.GetHashCode().ToString();

        show = episode.ShowTitle + "_" + episode.ShowYear ?? string.Empty;
      }

      return string.Format("{0}_{1}_{2}", show, episode.Season, episode.Number);
    }

    #endregion

    #region IWorkflowModel implementation

    public Guid ModelId
    {
      get { return TRAKT_SETUP_MODEL_ID; }
    }

    public bool CanEnterState(NavigationContext oldContext, NavigationContext newContext)
    {
      return true;
    }
    /// <summary>
    /// Version of Plugin
    /// </summary>
    public static string Version
    {
      get
      {
        return Assembly.GetCallingAssembly().GetName().Version.ToString();
      }
    }

    /// <summary>
    /// UserAgent used for Web Requests
    /// </summary>
    public static string UserAgent
    {
      get
      {
        return string.Format("TraktForMediaPortal2/{0}", Version);
      }
    }

    public void EnterModelContext(NavigationContext oldContext, NavigationContext newContext)
    {
      // Load settings
      TraktSettings settings = ServiceRegistration.Get<ISettingsManager>().Load<TraktSettings>();
      IsEnabled = settings.EnableTrakt;
      Username = settings.Username;
      Password = settings.Password;
     
      // initialise API settings
      TraktAPI.ApplicationId = ApplicationId;
      TraktAPI.UserAgent = UserAgent;
      TraktAPI.UseSSL = settings.UseSSL;

      TraktLastSyncActivities la = new TraktLastSyncActivities();
      TraktLastSyncActivities.MovieActivities mov = new TraktLastSyncActivities.MovieActivities();
      TraktLastSyncActivities.EpisodeActivities ep = new TraktLastSyncActivities.EpisodeActivities();
      TraktLastSyncActivities.SeasonActivities ses = new TraktLastSyncActivities.SeasonActivities();
      TraktLastSyncActivities.ShowActivities swo = new TraktLastSyncActivities.ShowActivities();
      TraktLastSyncActivities.ListActivities lalis = new TraktLastSyncActivities.ListActivities();
      TraktLastSyncActivities.CommentActivities com = new TraktLastSyncActivities.CommentActivities();
      //  TestStatus = string.Empty;
      // BuildBannerUrls();
    }

    public void ExitModelContext(NavigationContext oldContext, NavigationContext newContext)
    {
      // Nothing to do here
    }

    public void ChangeModelContext(NavigationContext oldContext, NavigationContext newContext, bool push)
    {
      // Nothing to do here
    }

    public void Deactivate(NavigationContext oldContext, NavigationContext newContext)
    {
      // Nothing to do here
    }

    public void Reactivate(NavigationContext oldContext, NavigationContext newContext)
    {
      // Nothing to do here
    }

    public void UpdateMenuActions(NavigationContext context, IDictionary<Guid, WorkflowAction> actions)
    {
      // Nothing to do here
    }

    public ScreenUpdateMode UpdateScreen(NavigationContext context, ref string screen)
    {
      return ScreenUpdateMode.AutoWorkflowManager;
    }

    #endregion
  }
}
