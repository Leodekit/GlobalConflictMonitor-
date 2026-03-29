using GlobalConflictMonitor.Application.Services;
using GlobalConflictMonitor.Domain.Entities;
using GlobalConflictMonitor.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static GlobalConflictMonitor.Domain.Entities.Event;

namespace GlobalConflictMonitor.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EventsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly OfwRiskService _ofwRiskService;
        private readonly SituationReportService _situationReportService;

        public EventsController(AppDbContext db, OfwRiskService ofwRiskService, SituationReportService situationReportService)
        {
            _db = db;
            _ofwRiskService = ofwRiskService;
            _situationReportService = situationReportService;
        }

        // 1️⃣ Recent Events
        [HttpGet("recent")]
        public async Task<IActionResult> GetRecent()
        {
            var events = await _db.Events
                .OrderByDescending(e => e.EventDate)
                .Take(20)
                .ToListAsync();

            return Ok(events);
        }

        // 2️⃣ Top Severity
        [HttpGet("top-severity")]
        public async Task<IActionResult> GetTopSeverity()
        {
            var events = await _db.Events
                .OrderByDescending(e => e.SeverityScore)
                .ThenByDescending(e => e.EventDate)
                .Take(20)
                .ToListAsync();

            return Ok(events);
        }

        // 3️⃣ By Category
        [HttpGet("by-category")]
        public async Task<IActionResult> GetByCategory([FromQuery] EventCategory category)
        {
            var events = await _db.Events
                .Where(e => e.Category == category)
                .OrderByDescending(e => e.EventDate)
                .Take(50)
                .ToListAsync();

            return Ok(events);
        }

        [HttpGet("dashboard-summary")]
        public async Task<IActionResult> GetDashboardSummary()
        {
            var now = DateTime.UtcNow;
            var last24h = now.AddHours(-24);

            var totalEvents = await _db.Events.CountAsync();

            var recentEvents = await _db.Events
                .Where(e => e.EventDate >= last24h)
                .CountAsync();

            var byCategory = await _db.Events
                .GroupBy(e => e.Category)
                .Select(g => new
                {
                    Category = g.Key.ToString(),
                    Count = g.Count()
                })
                .ToListAsync();

            var topSeverity = await _db.Events
                .OrderByDescending(e => e.SeverityScore)
                .ThenByDescending(e => e.EventDate)
                .Take(5)
                .Select(e => new
                {
                    e.Title,
                    e.SeverityScore,
                    e.SourceName,
                    e.EventDate,
                    e.SourceUrl,
                    e.Summary
                })
                .ToListAsync();

            return Ok(new
            {
                TotalEvents = totalEvents,
                EventsLast24Hours = recentEvents,
                EventsByCategory = byCategory,
                TopSeverity = topSeverity
            });
        }

        [HttpGet("heatmap")]
        public async Task<IActionResult> GetHeatmap()
        {
            var data = await _db.Events
                .Where(e => e.Latitude != null && e.Longitude != null)
                .Select(e => new
                {
                    e.Latitude,
                    e.Longitude,
                    e.SeverityScore
                })
                .ToListAsync();

            return Ok(data);
        }

        [HttpGet("daily-count")]
        public async Task<IActionResult> GetDailyCount()
        {
            var result = await _db.Events
                .GroupBy(e => e.EventDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("with-location")]
        public async Task<IActionResult> GetWithLocation()
        {
            var result = await _db.Events
                .Where(e => e.Latitude != null && e.Longitude != null)
                .Select(e => new
                {
                    latitude = e.Latitude,
                    longitude = e.Longitude,
                    severity = e.SeverityScore,
                    title = e.Title
                })
                .ToListAsync();

            return Ok(result);
        }

        [HttpGet("conflict-summary")]
        public async Task<IActionResult> GetConflictSummary()
        {
            var last7Days = DateTime.UtcNow.AddDays(-7);

            var recentEvents = _db.Events
                .Where(e => e.EventDate >= last7Days);

            var total = await recentEvents.CountAsync();

            var iranCount = await recentEvents
                .Where(e => e.CountriesJson.Contains("Iran"))
                .CountAsync();

            var israelCount = await recentEvents
              .Where(e => e.CountriesJson.Contains("Israel"))
                .CountAsync();

            var usaCount = await recentEvents
               .Where(e => e.CountriesJson.Contains("USA"))
                .CountAsync();

            var filipinoImpact = await recentEvents
                .Where(e => e.AffectsFilipinos)
                .CountAsync();

            var highSeverity = await recentEvents
                .Where(e => e.SeverityScore >= 8)
                .OrderByDescending(e => e.SeverityScore)
                .ThenByDescending(e => e.EventDate)
                .Take(5)
                .Select(e => new
                {
                    e.Title,
                    e.Country,
                    e.SeverityScore,
                    e.SourceName,
                    e.EventDate
                })
                .ToListAsync();

            return Ok(new
            {
                TotalLast7Days = total,
                IranEvents = iranCount,
                IsraelEvents = israelCount,
                USAEvents = usaCount,
                FilipinoRelated = filipinoImpact,
                HighSeverityEvents = highSeverity
            });
        }

        [HttpGet("threat-level")]
        public async Task<IActionResult> GetThreatLevels([FromServices] ThreatLevelService threatService)
        {
            var result = await threatService.CalculateThreatLevelsAsync();
            return Ok(result);
        }

        [HttpGet("country/{country}")]
        public async Task<IActionResult> GetCountryEvents(string country)
        {
            var last7Days = DateTime.UtcNow.AddDays(-7);

            var events = await _db.Events
                .Where(e => e.EventDate >= last7Days &&
                            e.CountriesJson.Contains(country))
                .OrderByDescending(e => e.EventDate)
                .Take(10)
                .Select(e => new
                {
                    e.Title,
                    e.SourceName,
                    e.EventDate,
                    e.SeverityScore,
                    e.SourceUrl
                })
                .ToListAsync();

            var count = await _db.Events
                .CountAsync(e => e.CountriesJson.Contains(country));

            var avgSeverity = await _db.Events
                .Where(e => e.CountriesJson.Contains(country))
                .AverageAsync(e => (double?)e.SeverityScore) ?? 0;

            return Ok(new
            {
                country,
                events = count,
                averageSeverity = avgSeverity,
                latestEvents = events
            });
        }

        [HttpGet("filipino-safety")]
        public async Task<IActionResult> GetFilipinoSafetyEvents()
        {
            var last7Days = DateTime.UtcNow.AddDays(-7);

            var events = await _db.Events
                .Where(e => e.Category == Event.EventCategory.FilipinoSafety &&
                            e.EventDate >= last7Days)
                .OrderByDescending(e => e.EventDate)
                .Take(10)
                .Select(e => new
                {
                    e.Title,
                    e.Country,
                    e.SourceName,
                    e.EventDate,
                    e.SeverityScore
                })
                .ToListAsync();

            return Ok(events);
        }

        //[HttpGet("ofw-risk")]
        //public async Task<IActionResult> GetOfwRisk()
        //{
        //    var result = await _ofwRiskService.CalculateRiskAsync();
        //    return Ok(result);
        //}

        [HttpGet("situation-report")]
        public async Task<IActionResult> GetReport()
        {
            var report = await _situationReportService.GenerateReport();
            return Ok(new { report });
        }

        [HttpGet("locations")]
        public async Task<IActionResult> GetEventLocations()
        {
            var fromDate = DateTime.UtcNow.AddDays(-7);
            var events = await _db.Events
            .Where(e => e.EventDate >= fromDate)
            .ToListAsync();

            var result = events
                .Where(e => e.Latitude != null && e.Longitude != null)
                .Select(e => new
                {
                    e.Title,
                    e.SourceName,
                    e.SourceUrl,
                    e.Latitude,
                    e.Longitude,
                    e.SeverityScore
                });

            return Ok(result);
        }

        [HttpGet("hotspots")]
        public async Task<IActionResult> GetHotspots()
        {
            var fromDate = DateTime.UtcNow.AddDays(-6);
            var events = await _db.Events
           .Where(e => e.EventDate >= fromDate)
           .ToListAsync();

            var hotspots = events
                .Where(e => e.Latitude != null && e.Longitude != null)
                .Take(50)
                .Select(e => new
                {
                    title = e.Title,
                    sourceName = e.SourceName,
                    sourceUrl = e.SourceUrl,
                    latitude = e.Latitude,
                    longitude = e.Longitude,
                    severity = e.SeverityScore
                });

            return Ok(hotspots);
        }
    }
}