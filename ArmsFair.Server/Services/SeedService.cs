using ArmsFair.Shared.Enums;
using ArmsFair.Shared.Models;
using StackExchange.Redis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArmsFair.Server.Services;

public class SeedService(
    IHttpClientFactory  httpFactory,
    IConnectionMultiplexer redis,
    IConfiguration      config,
    ILogger<SeedService> logger)
{
    private const string CacheKey  = "seed:countries";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    public async Task<List<CountryState>> GetCountriesAsync(CancellationToken ct = default)
    {
        var db  = redis.GetDatabase();
        var raw = await db.StringGetAsync(CacheKey);
        if (raw.HasValue)
        {
            logger.LogInformation("SeedService: returning cached country list");
            return JsonSerializer.Deserialize<List<CountryState>>(raw!) ?? new();
        }

        logger.LogInformation("SeedService: fetching fresh seed data");
        var countries = await BuildCountryListAsync(ct);
        var json      = JsonSerializer.Serialize(countries);
        await db.StringSetAsync(CacheKey, json, CacheTtl);
        return countries;
    }

    private async Task<List<CountryState>> BuildCountryListAsync(CancellationToken ct)
    {
        var acledTask = FetchAcledAsync(ct);
        var gpiTask   = FetchGpiAsync(ct);
        await Task.WhenAll(acledTask, gpiTask);

        var acledIsos  = await acledTask;  // iso3 codes of active conflict countries
        var gpiScores  = await gpiTask;    // iso3 → GPI score (lower = more peaceful)

        // Base country list — all ~195 UN-recognised states
        var all = BaseCountryList();

        return all.Select(c =>
        {
            bool isConflict = acledIsos.Contains(c.Iso);
            gpiScores.TryGetValue(c.Iso, out float gpi); // 0 if not found

            var stage = DetermineStage(isConflict, gpi);

            // Tension: conflict countries start high, peaceful start low
            int tension = isConflict ? Random.Shared.Next(60, 90)
                        : gpi > 0 && gpi < 1.5f ? Random.Shared.Next(10, 30)
                        : Random.Shared.Next(25, 55);

            return c with
            {
                Stage             = stage,
                Tension           = tension,
                InitialStage2Plus = (int)stage >= 2
            };
        }).ToList();
    }

    private static CountryStage DetermineStage(bool isConflict, float gpi)
    {
        if (isConflict)
            // High GPI score in a conflict zone → Humanitarian Crisis, else Hot War
            return gpi >= 3.0f ? CountryStage.HumanitarianCrisis : CountryStage.HotWar;

        if (gpi == 0) return CountryStage.Simmering;     // no GPI data → assume low-level instability

        return gpi switch
        {
            < 1.5f => CountryStage.Dormant,
            < 2.5f => CountryStage.Simmering,
            _      => CountryStage.Active
        };
    }

    // ── ACLED ────────────────────────────────────────────────────────────────

    private async Task<HashSet<string>> FetchAcledAsync(CancellationToken ct)
    {
        var key    = config["Acled:ApiKey"];
        var email  = config["Acled:Email"];
        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(email))
        {
            logger.LogWarning("SeedService: ACLED credentials not configured — using empty conflict list");
            return new HashSet<string>();
        }

        try
        {
            var client = httpFactory.CreateClient("acled");
            // Fetch last 90 days of conflict events, grouped by ISO country code
            var url = $"https://api.acleddata.com/acled/read?key={key}&email={email}" +
                      $"&event_date={DateTime.UtcNow.AddDays(-90):yyyy-MM-dd}|{DateTime.UtcNow:yyyy-MM-dd}" +
                      "&event_date_where=BETWEEN&limit=0&fields=iso3&count=true";

            using var resp = await client.GetAsync(url, ct);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<AcledResponse>(cancellationToken: ct);
            return body?.Data?.Select(x => x.Iso3).Where(x => x is not null).ToHashSet()!
                   ?? new HashSet<string>();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SeedService: ACLED fetch failed — using empty conflict list");
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

    private record AcledResponse([property: JsonPropertyName("data")] List<AcledRow>? Data);
    private record AcledRow([property: JsonPropertyName("iso3")] string Iso3);
    private record GpiEntry(
        [property: JsonPropertyName("iso3")]  string Iso3,
        [property: JsonPropertyName("score")] float  Score);
}
