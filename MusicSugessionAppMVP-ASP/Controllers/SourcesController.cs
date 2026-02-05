using Microsoft.AspNetCore.Mvc;
using MusicSugessionAppMVP_ASP.Models;
using MusicSugessionAppMVP_ASP.Services;
using MusicSugessionAppMVP_ASP.Persistance;
using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

namespace MusicSugessionAppMVP_ASP.Controllers
{
    public class SourcesController : Controller
    {
        private readonly Persistence _db;
        private readonly IEmailService _emailService;
        private readonly AppleMusicService _appleMusic;

        // NOTE: Session Terminology
        // - "User Login Session" = ASP.NET HttpContext.Session (managed by ASP.NET, tied to browser cookie)
        // - "Crate Session" = CrateSessionState object stored in _crates dictionary
        // Currently: One user login session = One crate session (keyed by HttpContext.Session.Id)
        // To support multiple crate sessions per user login: generate unique crate session IDs (e.g., GUID)
        // and store the active crate session ID in HttpContext.Session
        static ConcurrentDictionary<string, CrateSessionState> _crates = new();
        private const int PrimaryRefillThreshold = 10;
        private const int PrimaryRefillBatchSize = 5;

        public SourcesController(Persistence db, IEmailService emailService, AppleMusicService appleMusic)
        {
            _db = db;
            _emailService = emailService;
            _appleMusic = appleMusic;
        }

        [HttpGet]
        public IActionResult CanSkipExportOverlay()
        {
            var sessionKey = HttpContext.Session.Id;

            // 1. Logged-in user with email
            var userEmail = HttpContext.Session.GetString("Email");
            if (!string.IsNullOrWhiteSpace(userEmail))
                return Json(new { canSkip = true });

            // 2. Email already registered in current crate session
            if (_crates.TryGetValue(sessionKey, out var crate))
            {
                if (!string.IsNullOrWhiteSpace(crate.RegisteredEmail))
                    return Json(new { canSkip = true });
            }

            // Otherwise, we do NOT know where to export
            return Json(new { canSkip = false });
        }

        [HttpGet]
        public IActionResult HasActiveCrateSession()
        {
            var sessionKey = HttpContext.Session.Id;
            
            if (_crates.TryGetValue(sessionKey, out var crate))
            {
                var isActive = crate.LifecycleState == CrateLifecycleState.Active;
                return Json(new { hasActiveSession = isActive });
            }

            return Json(new { hasActiveSession = false });
        }

        [HttpGet]
        public IActionResult IsCrateReady()
        {
            var sessionKey = HttpContext.Session.Id;
            
            if (!_crates.TryGetValue(sessionKey, out var crate))
                return Json(new { isReady = false });

            lock (crate)
            {
                // Crate is ready if it's warm and has tracks available
                var isReady = crate.LifecycleState == CrateLifecycleState.Active &&
                              crate.IsWarm &&
                              crate.PrimaryQueue.Count > 0;
                return Json(new { isReady });
            }
        }



        public IActionResult Index()
        {
            //if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            //{
            //    return RedirectToAction(
            //        "Login",
            //        "Home",
            //        new { returnUrl = HttpContext.Request.Path }
            //    );
            //}

            return View();
        }




        //[HttpGet]
        //public IActionResult Review()
        //{
        //    //if (HttpContext.Session.GetString("IsAuthenticated") != "true")
        //    //    return RedirectToAction("Login", "Home");

        //    return View();
        //}

        [HttpGet]
        public IActionResult Review(string? postLogin = null)
        {
            //if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            //    return RedirectToAction("Login", "Home");

            ViewData["PostLogin"] = postLogin;

            var sessionKey = HttpContext.Session.Id;

            // Safety check: if no active crate exists, redirect to Index
            if (!_crates.TryGetValue(sessionKey, out var crate) || 
                crate.LifecycleState != CrateLifecycleState.Active)
            {
                return RedirectToAction("Index");
            }

            // ✅ If the user logged in mid-flow, attach the existing DB session to them.
            // This fixes the case where the crate was created anonymously (UserId=null)
            // and later the UI claims "saved successfully" after login.
            var isAuthenticated = HttpContext.Session.GetString("IsAuthenticated") == "true";
            if (isAuthenticated)
            {
                var userEmail = HttpContext.Session.GetString("Email");
                if (!string.IsNullOrWhiteSpace(userEmail) &&
                    crate.SessionId != Guid.Empty)
                {
                    var user = _db.Users.FirstOrDefault(u => u.Email == userEmail);
                    if (user != null)
                    {
                        var dbSession = _db.Sessions.FirstOrDefault(s => s.Id == crate.SessionId);
                        if (dbSession != null && dbSession.UserId != user.Id)
                        {
                            dbSession.UserId = user.Id;
                            _db.SaveChanges();
                        }
                    }
                }
            }

            // Resolve selected artists (crate source) from current crate session
            if (crate.SeedArtists != null)
            {
                var artistNames = crate.SeedArtists
                    .OrderBy(sa => sa.Position)
                    .Select(sa => sa.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .ToList();

                if (artistNames.Count > 0)
                {
                    ViewData["CrateSource"] = string.Join(", ", artistNames);
                }
            }

            return View();
        }

        [HttpGet]
        public IActionResult Loading()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Tutorial()
        {
            return View();
        }

        [HttpGet]
        public IActionResult TermsAndConditions()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> SkipTrack([FromBody] TrackInfo track)
        {
            var sessionKey = HttpContext.Session.Id;

            if (!_crates.TryGetValue(sessionKey, out var crate))
                return Ok();

            lock (crate)
            {
                // Prevent duplicates
                if (!crate.SkippedTracks.Any(t =>
                    string.Equals(t.Id, track.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    crate.SkippedTracks.Add(track);
                }

                crate.TotalSwipeCount++;
                crate.DislikeCount++;
            }

            // Persist swipe event + track stats
            if (crate.SessionId != Guid.Empty)
            {
                var (artist, trackEntity) = await GetOrCreateArtistAndTrackAsync(track);

                var swipe = new SwipeEvent
                {
                    Id = Guid.NewGuid(),
                    SessionId = crate.SessionId,
                    Session = null!,
                    TrackId = trackEntity.Id,
                    Track = null!,
                    Direction = SwipeDirection.Dislike,
                    SwipedAtUtc = DateTime.UtcNow
                };

                _db.SwipeEvents.Add(swipe);

                await UpsertTrackStatsAsync(trackEntity.Id, like: false);
                await UpsertSessionStatsAsync(crate);
                await _db.SaveChangesAsync();
            }

            return Ok();
        }

        private string? ResolveEmail(CrateSessionState crate)
        {
            if (!string.IsNullOrWhiteSpace(crate.RegisteredEmail))
                return crate.RegisteredEmail;

            var email = HttpContext.Session.GetString("Email");
            return string.IsNullOrWhiteSpace(email) ? null : email;
        }



        [HttpPost]
        public async Task<IActionResult> EndSession()
        {
            var sessionKey = HttpContext.Session.Id;

            if (!_crates.TryGetValue(sessionKey, out var crate))
                return BadRequest();

            crate.LifecycleState = CrateLifecycleState.Ended;
            crate.EndedAtUtc = DateTime.UtcNow;

            // Persist session end + stats snapshot
            if (crate.SessionId != Guid.Empty)
            {
                var session = await _db.Sessions
                    .FirstOrDefaultAsync(s => s.Id == crate.SessionId);

                if (session != null)
                {
                    session.EndedAtUtc = crate.EndedAtUtc;
                }

                await UpsertSessionStatsAsync(crate, snapshot: true);
                await _db.SaveChangesAsync();
            }

            // ✅ If user is logged in (or email otherwise known), auto-select email export.
            // This prevents re-prompting at end-session when we already know where to send.
            if (crate.SelectedExportMedium == ExportMedium.None)
            {
                var resolvedEmail = ResolveEmail(crate);
                if (!string.IsNullOrWhiteSpace(resolvedEmail))
                {
                    crate.SelectedExportMedium = ExportMedium.Email;
                    crate.RegisteredEmail ??= resolvedEmail;
                }
                else
                {
                    // 🔒 Guard: no export medium selected and no email known
                    return Json(new { status = "no-medium" });
                }
            }

            // 🔒 Guard: email export requires email
            if (crate.SelectedExportMedium == ExportMedium.Email)
            {
                // If user is logged in, use their session email even if not explicitly registered this run.
                var email = ResolveEmail(crate);
                if (string.IsNullOrWhiteSpace(email))
                    return Json(new { status = "missing-email" });

                // Keep crate state consistent for the rest of the flow (views + CanSkipExportOverlay)
                crate.RegisteredEmail ??= email;
                try
                {
                    if (!crate.EmailSent)
                    {
                        await _emailService.SendPlaylistAsync(
                            email,
                            crate.LikedTracks);

                        lock (crate)
                        {
                            crate.EmailSent = true;
                            crate.LifecycleState = CrateLifecycleState.Exported;
                        }

                        // Persist export record
                        if (crate.SessionId != Guid.Empty)
                        {
                            var export = new SessionExport
                            {
                                Id = Guid.NewGuid(),
                                SessionId = crate.SessionId,
                                Session = null!,
                                Medium = ExportMedium.Email,
                                ExportedAtUtc = DateTime.UtcNow
                            };

                            _db.SessionExports.Add(export);
                            await _db.SaveChangesAsync();
                        }
                    }
                }
                catch (Exception ex)
                {
                }

                return Json(new { status = "email-sent" });
            }

            // Future: Spotify / Apple Music
            return Json(new { status = "unsupported" });
        }



        [HttpPost]
        public async Task<IActionResult> CreateCrate(CreateCrateInputModel input)
        {
            // Check if tutorial should be shown (user not logged in AND first time in session)
            var userEmail = HttpContext.Session.GetString("Email");
            var isFirstTimeInSession = HttpContext.Session.GetString("SessionInitialized") == null;
            var shouldShowTutorial = string.IsNullOrWhiteSpace(userEmail) && isFirstTimeInSession;

            HttpContext.Session.SetString("SessionInitialized", "true");
            var sessionKey = HttpContext.Session.Id;


            if (_crates.TryGetValue(sessionKey, out var existing))
            {
                lock (existing)
                {
                    existing.LifecycleState = CrateLifecycleState.ToBeDiscarded;
                }
            }



            //if (HttpContext.Session.GetString("IsAuthenticated") != "true")
            //    return RedirectToAction("Login", "Home");

            var resolvedArtists = new List<ArtistInfo>();

            foreach (var name in new[] { input.Artist1, input.Artist2, input.Artist3 })
            {
                var artist = await ResolveArtistAsync(_appleMusic, name);
                if (artist == null)
                {
                    ModelState.AddModelError("", $"Artist not found: {name}");
                    return View("Index");
                }

                resolvedArtists.Add(artist);
            }



            var crate = new CrateSessionState
            {
                SelectedGenres = ExtractGenres(resolvedArtists),
                StartedAtUtc = DateTime.UtcNow,
                DeviceType = DetectDeviceType(HttpContext),
                LifecycleState = CrateLifecycleState.Active,
                
            };

            // ✅ Logged-in users: default export destination is their account email.
            // This prevents the export prompt from reappearing at end-session.
            if (!string.IsNullOrWhiteSpace(userEmail))
            {
                crate.SelectedExportMedium = ExportMedium.Email;
                crate.RegisteredEmail = userEmail.Trim();
                crate.EmailRegisteredAtUtc = DateTimeOffset.UtcNow;
            }


//get the logged in user email
            var user = _db.Users.FirstOrDefault(u => u.Email == userEmail);
            // Create persistent Session row
            var dbSession = new Session
            {
                Id = Guid.NewGuid(),
                StartedAtUtc = crate.StartedAtUtc,
                EndedAtUtc = null,
                UserId = user?.Id,
                User = null!,
                DeviceType = crate.DeviceType
            };

            crate.SessionId = dbSession.Id;

            // Seed input artists (snapshot)
            int position = 1;
            foreach (var artist in resolvedArtists)
            {
                var dbArtist = await GetOrCreateArtistAsync(artist);

                crate.SeedArtists.Add(new SeedArtistSnapshot
                {
                    ArtistId = dbArtist.Id,
                    Name = dbArtist.Name,
                    Position = position
                });

                var inputArtist = new SessionInputArtist
                {
                    Id = Guid.NewGuid(),
                    SessionId = dbSession.Id,
                    Session = null!,
                    ArtistId = dbArtist.Id,
                    Artist = null!,
                    Position = position
                };

                _db.SessionInputArtists.Add(inputArtist);
                position++;
            }

            _db.Sessions.Add(dbSession);
            await _db.SaveChangesAsync();

            _crates[sessionKey] = crate;

            // 🔥 Fire-and-forget background fill (IMPORTANT)
            _ = Task.Run(() =>
                FillCrateAsync(sessionKey, resolvedArtists)
            );

            // Redirect to tutorial or loading screen
            if (shouldShowTutorial)
            {
                return RedirectToAction("Tutorial");
            }
            else
            {
                return RedirectToAction("Loading");
            }
        }







        [HttpPost]
        public IActionResult RegisterEmail([FromBody] RegisterEmailInputModel input)
        {
            if (string.IsNullOrWhiteSpace(input.Email))
                return BadRequest("Email is required.");

            var sessionId = HttpContext.Session.Id;

            if (!_crates.TryGetValue(sessionId, out var crate))
                return BadRequest("Crate session not found.");

            lock (crate)
            {
                // Prevent re-registration
                if (!string.IsNullOrWhiteSpace(crate.RegisteredEmail))
                    return Ok();

                crate.RegisteredEmail = input.Email.Trim();
                crate.EmailRegisteredAtUtc = DateTimeOffset.UtcNow;
                crate.SelectedExportMedium = ExportMedium.Email;

            }

            return Ok();
        }

        /// <summary>
        /// Sends the email playlist without ending the session.
        /// Used when user registers email during end-session flow to avoid calling EndSession twice.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SendEmail()
        {
            var sessionId = HttpContext.Session.Id;

            if (!_crates.TryGetValue(sessionId, out var crate))
                return BadRequest("Crate session not found.");

            // Guard: email export requires email
            if (crate.SelectedExportMedium != ExportMedium.Email)
                return BadRequest("Email export not selected.");

            if (string.IsNullOrWhiteSpace(crate.RegisteredEmail))
                return BadRequest("Email not registered.");

            try
            {
                if (!crate.EmailSent)
                {
                    var email = ResolveEmail(crate);
                    if (email == null)
                        return BadRequest("Email not available.");

                    await _emailService.SendPlaylistAsync(
                        email,
                        crate.LikedTracks);

                    lock (crate)
                    {
                        crate.EmailSent = true;
                        crate.LifecycleState = CrateLifecycleState.Exported;
                    }

                    // Persist export record
                    if (crate.SessionId != Guid.Empty)
                    {
                        var export = new SessionExport
                        {
                            Id = Guid.NewGuid(),
                            SessionId = crate.SessionId,
                            Session = null!,
                            Medium = ExportMedium.Email,
                            ExportedAtUtc = DateTime.UtcNow
                        };

                        _db.SessionExports.Add(export);
                        await _db.SaveChangesAsync();
                    }
                }

                return Json(new { status = "email-sent" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", message = ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> NextTrack()
        {
            var sessionKey = HttpContext.Session.Id;

            if (!_crates.TryGetValue(sessionKey, out var crate))
                return NoContent();

            TrackInfo? track;

            lock (crate)
            {
                if (!crate.IsWarm || crate.PrimaryQueue.Count == 0)
                    return NoContent();

                track = crate.PrimaryQueue.Dequeue();
                // 🔥 REFILL LOGIC (MISSING BEFORE)
                if (crate.PrimaryQueue.Count <= PrimaryRefillThreshold &&
                    crate.BackupQueue.Count > 0)
                {
                    string? lastArtist =
                        crate.PrimaryQueue.LastOrDefault()?.ArtistName;

                    var refill = TakeSpacedTracksFromBackup(
                        crate.BackupQueue,
                        PrimaryRefillBatchSize,
                        lastArtist);

                    foreach (var t in refill)
                        crate.PrimaryQueue.Enqueue(t);
                }
            }

            // Record exposure once per track per session
            if (crate.SessionId != Guid.Empty && track != null)
            {
                var (artist, trackEntity) = await GetOrCreateArtistAndTrackAsync(track);

                lock (crate)
                {
                    if (!crate.ExposedTrackIds.Add(trackEntity.Id))
                        return Json(track);
                }

                var exposure = new TrackExposure
                {
                    Id = Guid.NewGuid(),
                    SessionId = crate.SessionId,
                    Session = null!,
                    TrackId = trackEntity.Id,
                    Track = null!,
                    ExposedAtUtc = DateTime.UtcNow
                };

                _db.TrackExposures.Add(exposure);
                await _db.SaveChangesAsync();
            }

            return Json(track);
        }


        [HttpPost]
        public async Task<IActionResult> LikeTrack([FromBody] TrackInfo track)
        {
            var sessionKey = HttpContext.Session.Id;

            if (!_crates.TryGetValue(sessionKey, out var crate))
                return Ok();

            lock (crate)
            {
                if (!crate.LikedTracks.Any(t =>
                    string.Equals(t.Id, track.Id, StringComparison.OrdinalIgnoreCase)))
                {
                    crate.LikedTracks.Add(track);
                }

                crate.TotalSwipeCount++;
                crate.LikeCount++;
            }

            // Persist swipe event + track stats
            if (crate.SessionId != Guid.Empty)
            {
                var (artist, trackEntity) = await GetOrCreateArtistAndTrackAsync(track);

                var swipe = new SwipeEvent
                {
                    Id = Guid.NewGuid(),
                    SessionId = crate.SessionId,
                    Session = null!,
                    TrackId = trackEntity.Id,
                    Track = null!,
                    Direction = SwipeDirection.Like,
                    SwipedAtUtc = DateTime.UtcNow
                };

                _db.SwipeEvents.Add(swipe);

                await UpsertTrackStatsAsync(trackEntity.Id, like: true);
                await UpsertSessionStatsAsync(crate);
                await _db.SaveChangesAsync();
            }

            // 🔥 Expand discovery graph asynchronously
            _ = Task.Run(() =>
                ExpandFromLikedTrackAsync(sessionKey, track)
            );

            return Ok();
        }


        private async Task FillCrateAsync(
    string sessionId,
    List<ArtistInfo> seedArtists)
        {
            var crate = _crates[sessionId];
            var aggregator = new SimilarArtistAggregationService(_appleMusic);

            foreach (var seed in seedArtists)
            {
                var related =
                    await aggregator.GetSimilarArtistsWithAtLeastOneTrackAsync(
                        seed.Name, 30);

                foreach (var candidate in related)
                {
                    if (!crate.SeenArtists.Add(candidate.Name))
                        continue;

                    if (!await ArtistValdation(crate.SelectedGenres, candidate, seedArtists))
                        continue;

                    var shuffled = candidate.Tracks
                        .OrderBy(_ => Random.Shared.Next())
                        .ToList();

                    if (shuffled.Count == 0)
                        continue;

                    lock (crate)
                    {
                        crate.PrimaryQueue.Enqueue(shuffled[0]);
                        foreach (var t in shuffled.Skip(1))
                            crate.BackupQueue.Add(t);

                        // equivalent of "open review window"
                        if (!crate.IsWarm && crate.PrimaryQueue.Count >= 5)
                            crate.IsWarm = true;
                    }

                    await Task.Delay(50);
                }
            }
        }

        private async Task<bool> ArtistValdation(HashSet<string> selectedGenres,
            ArtistInfo similarArtist, List<ArtistInfo> selectedArtists)
        {
            if (selectedArtists.Any(a =>
                string.Equals(a.Name, similarArtist.Name, StringComparison.OrdinalIgnoreCase)))
                return false;

            bool hasGenres = await PopulateGenresFromAppleMusicAsync(similarArtist);
            if (!hasGenres)
                return false;

            if (!similarArtist.Metadata.TryGetValue("genres", out var genreText))
                return false;

            var candidateGenres = genreText
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(g => g.Trim());

            if (!candidateGenres.Any(g => selectedGenres.Contains(g)))
                return false;

            return true;
        }


        private async Task ExpandFromLikedTrackAsync(
    string sessionId,
    TrackInfo likedTrack)
        {
            if (!_crates.TryGetValue(sessionId, out var crate))
                return;
            var aggregator = new SimilarArtistAggregationService(_appleMusic);

            try
            {
                var relatedArtists =
                    await aggregator.GetSimilarArtistsWithAtLeastOneTrackAsync(
                        likedTrack.ArtistName, 10);

                foreach (var candidate in relatedArtists)
                {
                    // 🔒 Deduplicate artists globally
                    if (!crate.SeenArtists.Add(candidate.Name))
                        continue;

                    if (!await ArtistValdation(
                            crate.SelectedGenres,
                            candidate,
                            new List<ArtistInfo>())) // seed artists already filtered earlier
                        continue;

                    var shuffled = candidate.Tracks
                        .OrderBy(_ => Random.Shared.Next())
                        .ToList();

                    if (shuffled.Count == 0)
                        continue;

                    lock (crate)
                    {
                        // Primary → immediate review
                        crate.PrimaryQueue.Enqueue(shuffled[0]);

                        // Backup → refill later
                        foreach (var t in shuffled.Skip(1))
                            crate.BackupQueue.Add(t);

                        if (!crate.IsWarm && crate.PrimaryQueue.Count >= 2)
                            crate.IsWarm = true;
                    }

                    await Task.Delay(50);
                }
            }
            catch
            {
                // intentionally swallowed to preserve pipeline continuity
            }
        }


        private static List<TrackInfo> TakeSpacedTracksFromBackup(
    List<TrackInfo> backup,
    int takeCount,
    string? lastArtistInPrimary = null)
        {
            var artistBuckets = backup
                .GroupBy(t => t.ArtistName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => new Queue<TrackInfo>(g));

            var result = new List<TrackInfo>();
            string? lastArtist = lastArtistInPrimary;

            while (result.Count < takeCount && artistBuckets.Count > 0)
            {
                var orderedArtists = artistBuckets
                    .OrderByDescending(kv => kv.Value.Count)
                    .Select(kv => kv.Key)
                    .ToList();

                var nextArtist =
                    orderedArtists.FirstOrDefault(a =>
                        !string.Equals(a, lastArtist, StringComparison.OrdinalIgnoreCase))
                    ?? orderedArtists.First();

                var track = artistBuckets[nextArtist].Dequeue();
                result.Add(track);
                lastArtist = nextArtist;

                if (artistBuckets[nextArtist].Count == 0)
                    artistBuckets.Remove(nextArtist);
            }

            foreach (var track in result)
                backup.Remove(track);

            return result;
        }



        private async Task<ArtistInfo?> ResolveArtistAsync(AppleMusicService appleMusicService, string inputName)
        {
            if (string.IsNullOrWhiteSpace(inputName))
                return null;

            var results = await appleMusicService.SearchArtistsAsync(inputName).ConfigureAwait(false);
            if (results.Count == 0)
                return null;

            var exact =
                results.FirstOrDefault(a =>
                    string.Equals(a.Name, inputName, StringComparison.OrdinalIgnoreCase));

            var selected = exact ?? results
                .OrderByDescending(a => SimilarityScore(inputName, a.Name))
                .First();

            if (!TryGetAppleMusicId(selected, out var appleId))
                return selected;

            // Fetch full artist object (genreNames, url, etc.)
            var full = await appleMusicService.GetArtistByIdAsync(appleId).ConfigureAwait(false);
            return full ?? selected;
        }

        private async Task<bool> PopulateGenresFromAppleMusicAsync(ArtistInfo artist)
        {
            if (artist.Metadata.TryGetValue("genres", out var existing) && !string.IsNullOrWhiteSpace(existing))
                return true;

            if (!TryGetAppleMusicId(artist, out var appleId))
                return false;

            try
            {
                var full = await _appleMusic.GetArtistByIdAsync(appleId).ConfigureAwait(false);
                if (full == null)
                    return false;

                // Copy a few enriched fields
                artist.ImageUrl ??= full.ImageUrl;
                if (full.Metadata.TryGetValue("genres", out var genres) && !string.IsNullOrWhiteSpace(genres))
                    artist.Metadata["genres"] = genres;

                return artist.Metadata.TryGetValue("genres", out var g) && !string.IsNullOrWhiteSpace(g);
            }
            catch
            {
                // preserve original pipeline behavior (soft-fail)
                return false;
            }
        }

        private static bool TryGetAppleMusicId(ArtistInfo artist, out string appleMusicId)
        {
            if (artist.Metadata.TryGetValue("apple_music_id", out var id) && !string.IsNullOrWhiteSpace(id))
            {
                appleMusicId = id!;
                return true;
            }

            appleMusicId = string.Empty;
            return false;
        }

        private static int SimilarityScore(string a, string b)
        {
            a = a.ToLowerInvariant();
            b = b.ToLowerInvariant();

            if (a == b) return 100;
            if (b.Contains(a) || a.Contains(b)) return 80;

            return 0;
        }

        private HashSet<string> ExtractGenres(List<ArtistInfo> resolvedArtists)
        {
            var genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var artist in resolvedArtists)
            {
                if (artist.Metadata.TryGetValue("genres", out var g) && g != null)
                {
                    foreach (var genre in g.Split(',', StringSplitOptions.RemoveEmptyEntries))
                        genres.Add(genre.Trim());
                }
            }

            return genres;
        }

        // -----------------------------
        // Persistence helpers
        // -----------------------------

        private async Task<Artist> GetOrCreateArtistAsync(ArtistInfo artistInfo)
        {
            var existing = await _db.Artists
                .FirstOrDefaultAsync(a => a.Name == artistInfo.Name);

            if (existing != null)
                return existing;

            var artist = new Artist
            {
                Id = Guid.NewGuid(),
                Name = artistInfo.Name,
                SpotifyId = artistInfo.SpotifyId,
                DeezerId = artistInfo.DeezerId,
                MusicBrainzId = artistInfo.Metadata.TryGetValue("musicbrainz_id", out var mb)
                    ? mb
                    : null
            };

            _db.Artists.Add(artist);
           // await _db.SaveChangesAsync();

            return artist;
        }

        private async Task<(Artist artist, Track track)> GetOrCreateArtistAndTrackAsync(TrackInfo trackInfo)
        {
            var artist = await _db.Artists
                .FirstOrDefaultAsync(a => a.Name == trackInfo.ArtistName);

            if (artist == null)
            {
                artist = new Artist
                {
                    Id = Guid.NewGuid(),
                    Name = trackInfo.ArtistName
                };
                _db.Artists.Add(artist);
                await _db.SaveChangesAsync();
            }

            var track = await _db.Tracks
                .FirstOrDefaultAsync(t => t.ArtistId == artist.Id && t.Name == trackInfo.Name);

            if (track == null)
            {
                track = new Track
                {
                    Id = Guid.NewGuid(),
                    Name = trackInfo.Name,
                    ArtistId = artist.Id,
                    Artist = artist
                };

                _db.Tracks.Add(track);
                await _db.SaveChangesAsync();
            }

            return (artist, track);
        }

        private async Task UpsertSessionStatsAsync(CrateSessionState crate, bool snapshot = false)
        {
            if (crate.SessionId == Guid.Empty)
                return;

            var stats = await _db.SessionStats
                .FirstOrDefaultAsync(s => s.SessionId == crate.SessionId);

            if (stats == null)
            {
                stats = new SessionStats
                {
                    SessionId = crate.SessionId,
                    TotalSwipes = crate.TotalSwipeCount,
                    Likes = crate.LikeCount,
                    Dislikes = crate.DislikeCount
                };

                _db.SessionStats.Add(stats);
            }
            else
            {
                stats.TotalSwipes = crate.TotalSwipeCount;
                stats.Likes = crate.LikeCount;
                stats.Dislikes = crate.DislikeCount;
            }

            if (snapshot)
            {
                var snap = new SessionStatsSnapshot
                {
                    SessionId = crate.SessionId,
                    TotalSwipes = crate.TotalSwipeCount,
                    Likes = crate.LikeCount,
                    Dislikes = crate.DislikeCount,
                    CapturedAtUtc = DateTime.UtcNow
                };

                _db.SessionStatsSnapshots.Add(snap);
            }
        }

        private async Task UpsertTrackStatsAsync(Guid trackId, bool like)
        {
            var stats = await _db.TrackStats
                .FirstOrDefaultAsync(s => s.TrackId == trackId);

            if (stats == null)
            {
                stats = new TrackStats
                {
                    TrackId = trackId,
                    TotalLikes = like ? 1 : 0,
                    TotalDislikes = like ? 0 : 1
                };

                _db.TrackStats.Add(stats);
            }
            else
            {
                if (like)
                    stats.TotalLikes++;
                else
                    stats.TotalDislikes++;
            }
        }

        /// <summary>
        /// Detects device type from the User-Agent header.
        /// Returns Mobile for mobile devices, Desktop otherwise.
        /// </summary>
        private static Persistance.DeviceType DetectDeviceType(HttpContext httpContext)
        {
            var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
            
            if (string.IsNullOrWhiteSpace(userAgent))
                return Persistance.DeviceType.Desktop;

            // Convert to lowercase for case-insensitive matching
            var ua = userAgent.ToLowerInvariant();

            // Common mobile device indicators
            var mobileIndicators = new[]
            {
                "mobile", "android", "iphone", "ipod", "ipad", "blackberry",
                "windows phone", "opera mini", "iemobile", "webos", "palm"
            };

            // Check if User-Agent contains any mobile indicators
            foreach (var indicator in mobileIndicators)
            {
                if (ua.Contains(indicator))
                    return Persistance.DeviceType.Mobile;
            }

            // Default to Desktop if no mobile indicators found
            return Persistance.DeviceType.Desktop;
        }
    }
}