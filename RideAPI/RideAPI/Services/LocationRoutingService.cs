using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RideAPI.Services;

public static class LocationRoutingService
{
    private static readonly HashSet<string> NorthLocations = new(StringComparer.OrdinalIgnoreCase)
    {
        "ha noi",
        "hanoi",
        "hn",
        "hai phong",
        "haiphong",
        "quang ninh",
        "bac ninh",
        "bac giang",
        "hai duong",
        "hung yen",
        "vinh phuc",
        "thai nguyen",
        "nam dinh",
        "ninh binh",
        "lang son",
        "phu tho",
        "thanh hoa",
        "hoa binh"
    };

    private static readonly HashSet<string> SouthLocations = new(StringComparer.OrdinalIgnoreCase)
    {
        "ho chi minh",
        "hcm",
        "hcmc",
        "tphcm",
        "tp hcm",
        "saigon",
        "sai gon",
        "tp ho chi minh",
        "binh duong",
        "dong nai",
        "long an",
        "ba ria vung tau",
        "tay ninh",
        "can tho",
        "an giang",
        "kien giang",
        "soc trang",
        "vinh long",
        "dong thap",
        "tien giang"
    };

    public static string ResolveRegion(double? latitude, string? province = null)
    {
        var byProvince = ResolveRegionFromProvince(province);
        if (byProvince is not null)
            return byProvince;

        if (latitude.HasValue)
            return ResolveRegionFromLatitude(latitude.Value);

        return "SOUTH";
    }

    public static string? ResolveRegionFromProvince(string? province)
    {
        var normalized = Normalize(province);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (NorthLocations.Contains(normalized))
            return "NORTH";

        if (SouthLocations.Contains(normalized))
            return "SOUTH";

        return null;
    }

    public static string ResolveRegionFromLatitude(double latitude)
        => latitude >= 16 ? "NORTH" : "SOUTH";

    public static int ResolveRegionId(double? latitude, string? province = null)
        => ResolveRegion(latitude, province) == "NORTH" ? 1 : 2;

    private static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var trimmed = input.Trim().ToLowerInvariant().Replace("_", " ").Replace("-", " ");
        var formD = trimmed.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(formD.Length);

        foreach (var c in formD)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category != UnicodeCategory.NonSpacingMark)
                builder.Append(c);
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }
}
