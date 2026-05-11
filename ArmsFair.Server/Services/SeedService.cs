using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using ArmsFair.Shared.Models.Messages;
using System.Text.Json.Serialization;

namespace ArmsFair.Server.Services;

public class SeedService(
    IHttpClientFactory   httpFactory,
    IConfiguration       config,
    ILogger<SeedService> logger)
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    // Simple in-memory cache: key → (value, expiry)
    private readonly Dictionary<string, (string Value, DateTime Expiry)> _cache = new();

    /// <summary>
    /// Returns the seeded country list for the given game mode.
    /// Realistic mode fetches ACLED + GPI (cached 24h). All other modes are instant.
    /// Custom mode applies per-country overrides on top of the Realistic base.
    /// </summary>
    public async Task<List<CountryState>> GetCountriesAsync(
        GameMode mode,
        List<CustomCountryConfig>? customOverrides = null,
        CancellationToken ct = default)
    {
        return mode switch
        {
            GameMode.EqualWorld => SeedEqualWorld(),
            GameMode.BlankSlate => SeedBlankSlate(),
            GameMode.HotWorld   => SeedHotWorld(),
            GameMode.Custom     => await SeedCustomAsync(customOverrides, ct),
            _                   => await SeedRealisticAsync(ct)
        };
    }

    // ── Mode 1: Realistic ─────────────────────────────────────────────────────

    private async Task<List<CountryState>> SeedRealisticAsync(CancellationToken ct)
    {
        try
        {
            if (_cache.TryGetValue("realistic", out var entry) && DateTime.UtcNow < entry.Expiry)
            {
                logger.LogInformation("SeedService: returning cached Realistic country list");
                return System.Text.Json.JsonSerializer.Deserialize<List<CountryState>>(entry.Value) ?? new();
            }

            logger.LogInformation("SeedService: fetching fresh Realistic seed data from ACLED + GPI");
            var countries = await BuildRealisticListAsync(ct);
            _cache["realistic"] = (System.Text.Json.JsonSerializer.Serialize(countries), DateTime.UtcNow.Add(CacheTtl));
            return countries;
        }
        catch (Exception ex)
        {
            logger.LogWarning("SeedService: Realistic seed failed ({Msg}), falling back to EqualWorld seed", ex.Message);
            return SeedEqualWorld();
        }
    }

    private async Task<List<CountryState>> BuildRealisticListAsync(CancellationToken ct)
    {
        var acledTask = FetchAcledAsync(ct);
        var gpiTask   = FetchGpiAsync(ct);
        await Task.WhenAll(acledTask, gpiTask);

        var acledIsos = await acledTask;
        var gpiScores = await gpiTask;

        return BaseCountryList().Select(c =>
        {
            bool isConflict = acledIsos.Contains(c.Iso);
            gpiScores.TryGetValue(c.Iso, out float gpi);

            var stage = DetermineRealisticStage(isConflict, gpi);
            int tension = isConflict        ? Random.Shared.Next(60, 90)
                        : gpi > 0 && gpi < 1.5f ? Random.Shared.Next(10, 30)
                        : Random.Shared.Next(25, 55);

            return c with { Stage = stage, Tension = tension, InitialStage2Plus = (int)stage >= 2 };
        }).ToList();
    }

    private static CountryStage DetermineRealisticStage(bool isConflict, float gpi)
    {
        if (isConflict)
            return gpi >= 3.0f ? CountryStage.HumanitarianCrisis : CountryStage.HotWar;
        if (gpi == 0) return CountryStage.Simmering;
        return gpi switch
        {
            < 1.5f => CountryStage.Dormant,
            < 2.5f => CountryStage.Simmering,
            _      => CountryStage.Active
        };
    }

    // ── Mode 2: Equal World ───────────────────────────────────────────────────

    private List<CountryState> SeedEqualWorld()
    {
        logger.LogInformation("SeedService: seeding Equal World");
        return BaseCountryList().Select(c => c with
        {
            Stage             = CountryStage.Simmering,
            Tension           = 25,
            DemandType        = "covert",   // covert-only demand at start
            InitialStage2Plus = false
        }).ToList();
    }

    // ── Mode 3: Blank Slate ───────────────────────────────────────────────────

    private List<CountryState> SeedBlankSlate()
    {
        logger.LogInformation("SeedService: seeding Blank Slate");
        return BaseCountryList().Select(c => c with
        {
            Stage             = CountryStage.Dormant,
            Tension           = 5,
            DemandType        = "none",
            InitialStage2Plus = false
        }).ToList();
    }

    // ── Mode 4: Hot World ─────────────────────────────────────────────────────

    private List<CountryState> SeedHotWorld()
    {
        logger.LogInformation("SeedService: seeding Hot World");
        return BaseCountryList().Select(c =>
        {
            var (stage, tension) = HotWorldRegion(c.Iso);
            return c with { Stage = stage, Tension = tension, InitialStage2Plus = (int)stage >= 2 };
        }).ToList();
    }

    // Conflict-prone: Stage 3, Tension 70
    private static readonly HashSet<string> _hotRegion = new()
    {
        "AFG","IRQ","SYR","YEM","SDN","SSD","SOM","MLI","BFA","NER","CAF","COD",
        "MMR","PAK","LBY","ETH","NGA","MOZ","CMR","TCD","HTI","UKR","PSE"
    };
    // Politically volatile: Stage 2, Tension 50
    private static readonly HashSet<string> _warmRegion = new()
    {
        "IRN","LBN","VEN","COL","MEX","GTM","HND","SLV","NIC","ZWE","BDI","RWA",
        "UGA","AGO","ZMB","MWI","MDG","GIN","GNB","SLE","LBR","CIV","TGO","BEN",
        "GEO","ARM","AZE","KAZ","KGZ","TJK","TKM","UZB","BLR","SRB","BIH","ALB",
        "MKD","MNE","XKX","BGD","NPL","LKA","IDN","PHL","TLS"
    };

    private static (CountryStage stage, int tension) HotWorldRegion(string iso)
    {
        if (_hotRegion.Contains(iso))  return (CountryStage.HotWar, 70);
        if (_warmRegion.Contains(iso)) return (CountryStage.Active,  50);
        return (CountryStage.Active, 35);   // stable regions — still Stage 2 in Hot World
    }

    // ── Mode 5: Custom ────────────────────────────────────────────────────────

    private async Task<List<CountryState>> SeedCustomAsync(
        List<CustomCountryConfig>? overrides, CancellationToken ct)
    {
        logger.LogInformation("SeedService: seeding Custom scenario");
        // Start from Realistic base (cached if available), then apply host overrides
        var base_ = await SeedRealisticAsync(ct);
        if (overrides is null || overrides.Count == 0) return base_;

        var overrideMap = overrides.ToDictionary(o => o.Iso);
        return base_.Select(c =>
        {
            if (!overrideMap.TryGetValue(c.Iso, out var ov)) return c;
            return c with
            {
                Stage             = ov.Stage,
                Tension           = ov.Tension,
                InitialStage2Plus = (int)ov.Stage >= 2
            };
        }).ToList();
    }

    // ── ACLED ────────────────────────────────────────────────────────────────

    private async Task<string?> GetAcledTokenAsync(CancellationToken ct)
    {
        if (_cache.TryGetValue("acled_token", out var entry) && DateTime.UtcNow < entry.Expiry)
            return entry.Value;

        var email    = config["Acled:Email"];
        var password = config["Acled:Password"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return null;

        try
        {
            var client = httpFactory.CreateClient("acled");
            var form   = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"]   = email,
                ["password"]   = password,
                ["grant_type"] = "password",
                ["client_id"]  = "acled",
                ["scope"]      = "authenticated"
            });

            using var resp = await client.PostAsync("https://acleddata.com/oauth/token", form, ct);
            resp.EnsureSuccessStatusCode();

            var body  = await resp.Content.ReadFromJsonAsync<AcledTokenResponse>(cancellationToken: ct);
            var token = body?.AccessToken;
            if (token is null) return null;

            _cache["acled_token"] = (token, DateTime.UtcNow.AddHours(23));
            return token;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SeedService: ACLED token fetch failed");
            return null;
        }
    }

    private async Task<HashSet<string>> FetchAcledAsync(CancellationToken ct)
    {
        var token = await GetAcledTokenAsync(ct);
        if (token is null)
        {
            logger.LogWarning("SeedService: ACLED credentials not configured or token fetch failed — using empty conflict list");
            return new HashSet<string>();
        }

        try
        {
            var client  = httpFactory.CreateClient("acled");
            var fromDate = DateTime.UtcNow.AddDays(-90).ToString("yyyy-MM-dd");
            var toDate   = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var url      = $"https://acleddata.com/api/acled/read?_format=json" +
                           $"&event_date={fromDate}|{toDate}&event_date_where=BETWEEN" +
                           "&fields=iso3&limit=5000";

            using var req  = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            using var resp = await client.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<AcledResponse>(cancellationToken: ct);
            return body?.Data?.Select(x => x.Iso3).Where(x => !string.IsNullOrEmpty(x)).ToHashSet()!
                   ?? new HashSet<string>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SeedService: ACLED data fetch failed — using empty conflict list");
            return new HashSet<string>();
        }
    }

    // ── GPI ──────────────────────────────────────────────────────────────────

    private async Task<Dictionary<string, float>> FetchGpiAsync(CancellationToken ct)
    {
        var url = config["Gpi:JsonUrl"];
        if (string.IsNullOrEmpty(url))
        {
            logger.LogWarning("SeedService: GPI URL not configured — using empty scores");
            return new Dictionary<string, float>();
        }

        try
        {
            var client = httpFactory.CreateClient("gpi");
            using var resp = await client.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<List<GpiEntry>>(cancellationToken: ct);
            return body?.ToDictionary(x => x.Iso3, x => x.Score)
                   ?? new Dictionary<string, float>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SeedService: GPI fetch failed — using empty scores");
            return new Dictionary<string, float>();
        }
    }

    // ── Static base list ─────────────────────────────────────────────────────
    // A minimal representative set; the full 195-country list would live here.
    // Countries without ACLED/GPI data default to Simmering.

    private static List<CountryState> BaseCountryList() =>
    [
        Make("AFG", "Afghanistan"), Make("DZA", "Algeria"),  Make("AGO", "Angola"),
        Make("ARG", "Argentina"),   Make("ARM", "Armenia"),   Make("AUS", "Australia"),
        Make("AUT", "Austria"),     Make("AZE", "Azerbaijan"),Make("BHS", "Bahamas"),
        Make("BHR", "Bahrain"),     Make("BGD", "Bangladesh"),Make("BLR", "Belarus"),
        Make("BEL", "Belgium"),     Make("BLZ", "Belize"),    Make("BEN", "Benin"),
        Make("BTN", "Bhutan"),      Make("BOL", "Bolivia"),   Make("BIH", "Bosnia and Herzegovina"),
        Make("BWA", "Botswana"),    Make("BRA", "Brazil"),    Make("BRN", "Brunei"),
        Make("BGR", "Bulgaria"),    Make("BFA", "Burkina Faso"),Make("BDI", "Burundi"),
        Make("CPV", "Cabo Verde"),  Make("KHM", "Cambodia"),  Make("CMR", "Cameroon"),
        Make("CAN", "Canada"),      Make("CAF", "Central African Republic"),Make("TCD", "Chad"),
        Make("CHL", "Chile"),       Make("CHN", "China"),     Make("COL", "Colombia"),
        Make("COM", "Comoros"),     Make("COD", "Congo, DRC"),Make("COG", "Congo, Rep."),
        Make("CRI", "Costa Rica"),  Make("CIV", "Côte d'Ivoire"),Make("HRV", "Croatia"),
        Make("CUB", "Cuba"),        Make("CYP", "Cyprus"),    Make("CZE", "Czechia"),
        Make("DNK", "Denmark"),     Make("DJI", "Djibouti"),  Make("DOM", "Dominican Republic"),
        Make("ECU", "Ecuador"),     Make("EGY", "Egypt"),     Make("SLV", "El Salvador"),
        Make("GNQ", "Equatorial Guinea"),Make("ERI", "Eritrea"),Make("EST", "Estonia"),
        Make("SWZ", "Eswatini"),    Make("ETH", "Ethiopia"),  Make("FJI", "Fiji"),
        Make("FIN", "Finland"),     Make("FRA", "France"),    Make("GAB", "Gabon"),
        Make("GMB", "Gambia"),      Make("GEO", "Georgia"),   Make("DEU", "Germany"),
        Make("GHA", "Ghana"),       Make("GRC", "Greece"),    Make("GTM", "Guatemala"),
        Make("GIN", "Guinea"),      Make("GNB", "Guinea-Bissau"),Make("GUY", "Guyana"),
        Make("HTI", "Haiti"),       Make("HND", "Honduras"),  Make("HUN", "Hungary"),
        Make("ISL", "Iceland"),     Make("IND", "India"),     Make("IDN", "Indonesia"),
        Make("IRN", "Iran"),        Make("IRQ", "Iraq"),      Make("IRL", "Ireland"),
        Make("ISR", "Israel"),      Make("ITA", "Italy"),     Make("JAM", "Jamaica"),
        Make("JPN", "Japan"),       Make("JOR", "Jordan"),    Make("KAZ", "Kazakhstan"),
        Make("KEN", "Kenya"),       Make("PRK", "Korea, North"),Make("KOR", "Korea, South"),
        Make("XKX", "Kosovo"),      Make("KWT", "Kuwait"),    Make("KGZ", "Kyrgyzstan"),
        Make("LAO", "Laos"),        Make("LVA", "Latvia"),    Make("LBN", "Lebanon"),
        Make("LSO", "Lesotho"),     Make("LBR", "Liberia"),   Make("LBY", "Libya"),
        Make("LTU", "Lithuania"),   Make("LUX", "Luxembourg"),Make("MDG", "Madagascar"),
        Make("MWI", "Malawi"),      Make("MYS", "Malaysia"),  Make("MDV", "Maldives"),
        Make("MLI", "Mali"),        Make("MLT", "Malta"),     Make("MRT", "Mauritania"),
        Make("MUS", "Mauritius"),   Make("MEX", "Mexico"),    Make("MDA", "Moldova"),
        Make("MNG", "Mongolia"),    Make("MNE", "Montenegro"),Make("MAR", "Morocco"),
        Make("MOZ", "Mozambique"),  Make("MMR", "Myanmar"),   Make("NAM", "Namibia"),
        Make("NPL", "Nepal"),       Make("NLD", "Netherlands"),Make("NZL", "New Zealand"),
        Make("NIC", "Nicaragua"),   Make("NER", "Niger"),     Make("NGA", "Nigeria"),
        Make("MKD", "North Macedonia"),Make("NOR", "Norway"),Make("OMN", "Oman"),
        Make("PAK", "Pakistan"),    Make("PAN", "Panama"),    Make("PNG", "Papua New Guinea"),
        Make("PRY", "Paraguay"),    Make("PER", "Peru"),      Make("PHL", "Philippines"),
        Make("POL", "Poland"),      Make("PRT", "Portugal"),  Make("QAT", "Qatar"),
        Make("ROU", "Romania"),     Make("RUS", "Russia"),    Make("RWA", "Rwanda"),
        Make("SAU", "Saudi Arabia"),Make("SEN", "Senegal"),   Make("SRB", "Serbia"),
        Make("SLE", "Sierra Leone"),Make("SGP", "Singapore"),  Make("SVK", "Slovakia"),
        Make("SVN", "Slovenia"),    Make("SOM", "Somalia"),   Make("ZAF", "South Africa"),
        Make("SSD", "South Sudan"), Make("ESP", "Spain"),     Make("LKA", "Sri Lanka"),
        Make("SDN", "Sudan"),       Make("SUR", "Suriname"),  Make("SWE", "Sweden"),
        Make("CHE", "Switzerland"),  Make("SYR", "Syria"),    Make("TWN", "Taiwan"),
        Make("TJK", "Tajikistan"),   Make("TZA", "Tanzania"), Make("THA", "Thailand"),
        Make("TLS", "Timor-Leste"),  Make("TGO", "Togo"),     Make("TTO", "Trinidad and Tobago"),
        Make("TUN", "Tunisia"),      Make("TUR", "Turkey"),   Make("TKM", "Turkmenistan"),
        Make("UGA", "Uganda"),       Make("UKR", "Ukraine"),  Make("ARE", "United Arab Emirates"),
        Make("GBR", "United Kingdom"),Make("USA", "United States"),Make("URY", "Uruguay"),
        Make("UZB", "Uzbekistan"),   Make("VEN", "Venezuela"),Make("VNM", "Vietnam"),
        Make("YEM", "Yemen"),        Make("ZMB", "Zambia"),   Make("ZWE", "Zimbabwe"),
    ];

    private static CountryState Make(string iso, string name) => new()
    {
        Iso               = iso,
        Name              = name,
        Stage             = CountryStage.Simmering,
        Tension           = 30,
        DemandType        = "open",
        InitialStage2Plus = false
    };

    // ── JSON response shapes ─────────────────────────────────────────────────

    private record AcledTokenResponse(
        [property: JsonPropertyName("access_token")]  string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);
    private record AcledResponse([property: JsonPropertyName("data")] List<AcledRow>? Data);
    private record AcledRow([property: JsonPropertyName("iso3")] string Iso3);
    private record GpiEntry(
        [property: JsonPropertyName("iso3")]  string Iso3,
        [property: JsonPropertyName("score")] float  Score);
}
