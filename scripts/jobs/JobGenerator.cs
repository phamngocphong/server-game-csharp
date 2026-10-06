using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Builds <see cref="JobData"/> from <see cref="JobTemplate"/>s and the map's
/// <see cref="DeliveryLocation"/>s. Pickups are chosen near the player's position.
/// </summary>
public sealed class JobGenerator
{
    /// <summary>Initial search radius for pickups around the player (grows if too few found).</summary>
    public float PickupSearchRadius { get; set; } = 1400f;
    /// <summary>Pickups closer than this are skipped (player is basically standing on them).</summary>
    public float MinPickupDistance { get; set; } = 120f;

    private readonly CityMap _map;
    private readonly IReadOnlyList<JobTemplate> _templates;
    private readonly RandomNumberGenerator _rng = new();
    private int _nextId = 1;

    public JobGenerator(CityMap map, IEnumerable<JobTemplate> templates)
    {
        _map = map;
        _templates = templates.ToList();
        _rng.Randomize();
    }

    /// <summary>Manhattan distance: a fair estimate of driving distance on a grid city.</summary>
    public static float RouteDistance(Vector2 a, Vector2 b) => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

    public List<JobData> GenerateJobs(Vector2 origin, int count)
    {
        var jobs = new List<JobData>();
        if (_templates.Count == 0 || _map.Locations.Count == 0)
            return jobs;

        var pickups = FindPickupCandidates(origin, count);
        Shuffle(pickups);
        for (var i = 0; i < count; i++)
        {
            var template = PickTemplate();
            var pickup = pickups[i % pickups.Count];
            var delivery = FindDelivery(pickup, template);
            if (delivery == null)
                continue;
            jobs.Add(BuildJob(template, pickup, delivery, origin));
        }

        jobs.Sort((a, b) => a.DistanceToPickup.CompareTo(b.DistanceToPickup));
        return jobs;
    }

    /// <summary>Re-times the pickup from the player's current position (they may have moved since the board was generated).</summary>
    public void UpdatePickupTimeLimit(JobData job, Vector2 origin)
    {
        var template = _templates.FirstOrDefault(t => t.TemplateId == job.TemplateId);
        if (template == null || !template.IsTimed)
            return;
        job.DistanceToPickup = RouteDistance(origin, job.PickupPosition);
        job.PickupTimeLimit = TimeLimit(template.PickupBaseTime, template.PickupTimePerKm, job.DistanceToPickup);
    }

    /// <summary>Base + km * per-km seconds, rounded up to a multiple of 5 s.</summary>
    public static float TimeLimit(float baseTime, float timePerKm, float distance)
    {
        var seconds = baseTime + distance / GameManager.PixelsPerKm * timePerKm;
        return Mathf.Ceil(seconds / 5f) * 5f;
    }

    /// <summary>
    /// One line for the Job Board about job types the driver's rating hides or makes rarer;
    /// empty when the rating restricts nothing.
    /// </summary>
    public string DescribeRatingLimits(float rating)
    {
        var locked = _templates.Where(t => t.RatingFactor(rating) <= 0f)
            .Select(t => $"{t.PackageName} ({GameManager.FormatRating(t.MinRating)}+)").ToList();
        var rarer = _templates.Where(t => t.RatingFactor(rating) is > 0f and < 1f)
            .Select(t => t.PackageName).ToList();
        var parts = new List<string>();
        if (locked.Count > 0)
            parts.Add("Locked by rating: " + string.Join(", ", locked));
        if (rarer.Count > 0)
            parts.Add("Rarer: " + string.Join(", ", rarer));
        return string.Join("\n", parts);
    }

    public int CalculateReward(JobTemplate template, float distance, DistrictData? district)
    {
        var km = distance / GameManager.PixelsPerKm;
        var raw = template.BaseReward + km * template.RewardPerKm;
        if (district != null)
            raw *= district.RewardMultiplier;
        raw *= GameManager.Instance.GetRewardMultiplier(template);
        raw *= 1f + _rng.RandfRange(-template.RewardVariance, template.RewardVariance);
        return Mathf.Max(1, Mathf.RoundToInt(raw));
    }

    private List<DeliveryLocation> FindPickupCandidates(Vector2 origin, int count)
    {
        var radius = PickupSearchRadius;
        var result = new List<DeliveryLocation>();
        while (radius < 50000f)
        {
            result = _map.GetLocationsInRange(origin, MinPickupDistance, radius);
            if (result.Count >= count * 2)
                break;
            radius *= 1.5f;
        }
        if (result.Count == 0)
            result = _map.Locations.ToList();
        return result;
    }

    private DeliveryLocation? FindDelivery(DeliveryLocation pickup, JobTemplate template)
    {
        var inRange = new List<DeliveryLocation>();
        DeliveryLocation? fallback = null;
        var bestScore = float.MaxValue;
        var ideal = (template.MinDistance + template.MaxDistance) * 0.5f;

        foreach (var loc in _map.Locations)
        {
            if (loc == pickup)
                continue;
            var d = RouteDistance(pickup.Position, loc.Position);
            if (d >= template.MinDistance && d <= template.MaxDistance)
                inRange.Add(loc);
            var score = Mathf.Abs(d - ideal);
            if (score < bestScore)
            {
                bestScore = score;
                fallback = loc;
            }
        }
        return inRange.Count > 0 ? inRange[_rng.RandiRange(0, inRange.Count - 1)] : fallback;
    }

    /// <summary>Weighted pick; the driver's rating scales each weight (see <see cref="JobTemplate.RatingFactor"/>).</summary>
    private JobTemplate PickTemplate()
    {
        var rating = GameManager.Instance.Reputation.Rating;
        var period = GameManager.Instance.Period;
        float WeightOf(JobTemplate t) =>
            Mathf.Max(t.Weight, 0f) * t.RatingFactor(rating) * (period?.TemplateWeight(t.TemplateId) ?? 1f);

        var total = _templates.Sum(WeightOf);
        if (total <= 0f)
            return _templates[_rng.RandiRange(0, _templates.Count - 1)]; // every type is locked: ignore the rating
        var roll = _rng.Randf() * total;
        foreach (var t in _templates)
        {
            roll -= WeightOf(t);
            if (roll <= 0f && WeightOf(t) > 0f)
                return t;
        }
        return _templates.Last(t => WeightOf(t) > 0f);
    }

    private JobData BuildJob(JobTemplate template, DeliveryLocation pickup, DeliveryLocation delivery, Vector2 origin)
    {
        var distance = RouteDistance(pickup.Position, delivery.Position);
        var distanceToPickup = RouteDistance(origin, pickup.Position);
        return new JobData
        {
            Id = $"JOB-{_nextId++:0000}",
            TemplateId = template.TemplateId,
            PackageName = template.PackageName,
            PackageColor = template.Color,
            Cargo = template.Cargo,
            CustomerName = template.CustomerNames.Length > 0
                ? template.CustomerNames[_rng.RandiRange(0, template.CustomerNames.Length - 1)]
                : "",

            PickupName = pickup.DisplayName,
            PickupDistrict = pickup.DistrictName,
            PickupPosition = pickup.Position,
            DeliveryName = delivery.DisplayName,
            DeliveryDistrict = delivery.DistrictName,
            DeliveryPosition = delivery.Position,

            Distance = distance,
            DistanceToPickup = distanceToPickup,
            Reward = CalculateReward(template, distance, delivery.District),

            PickupTimeLimit = template.IsTimed
                ? TimeLimit(template.PickupBaseTime, template.PickupTimePerKm, distanceToPickup) : 0f,
            DeliveryTimeLimit = template.IsTimed
                ? TimeLimit(template.DeliveryBaseTime, template.DeliveryTimePerKm, distance) : 0f,
            LatePenalty = template.IsTimed ? template.LatePenalty : 0f,
            TipShare = template.IsTimed ? template.TipShare : 0f,
            TipTimeLimit = template.IsTimed
                ? Mathf.Floor(TimeLimit(template.DeliveryBaseTime, template.DeliveryTimePerKm, distance) * template.TipTimeShare)
                : 0f,
        };
    }

    private void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = _rng.RandiRange(0, i);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
