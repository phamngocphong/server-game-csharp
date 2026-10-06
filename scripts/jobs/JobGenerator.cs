using System.Collections.Generic;
using System.Linq;
using Godot;

namespace ShipperSimulator;

/// <summary>
/// Builds <see cref="JobData"/> from <see cref="JobTemplate"/>s and the map's
/// <see cref="DeliveryLocation"/>s. Pickups are chosen near the player's position.
///
/// How a board is filled (<see cref="GenerateJobs"/>):
/// - A pool of <see cref="CandidateFactor"/> times as many normal jobs is generated and ranked
///   by <see cref="Goodness"/> (pay per km of pickup + trip). Each slot then takes a job from
///   that ranking, skewed by the driver's rating: at 5 stars mostly from the top (close,
///   well paid), at 3 stars evenly, at 1 star mostly from the bottom.
/// - Each slot can instead roll a rare <see cref="JobData.JobTier.Premium"/> job (close, short,
///   double pay; likelier with a high rating) or a rare <see cref="JobData.JobTier.Special"/>
///   job (very long trip, half pay, untimed, extra 5-star reviews; likelier with a low rating,
///   so it is a way to win the rating back).
/// </summary>
public sealed class JobGenerator
{
    /// <summary>Normal candidates generated per board slot to rank and pick from.</summary>
    public const int CandidateFactor = 3;
    public const float PremiumRewardMultiplier = 2f;
    public const float SpecialRewardMultiplier = 0.5f;
    /// <summary>Special jobs go at least this far (px; 2000 px = 1 km), or 1.5x the template's longest trip.</summary>
    public const float SpecialMinDistance = 8000f;
    /// <summary>Extra 5-star reviews a special job adds on delivery.</summary>
    public const int SpecialRatingBonus = 2;

    /// <summary>Chance per board slot of a premium job: 2% at 1 star up to 6% at 5 stars.</summary>
    public static float PremiumChance(float rating) => Mathf.Lerp(0.02f, 0.06f, Mathf.Clamp((rating - 1f) / 4f, 0f, 1f));

    /// <summary>Chance per board slot of a special job: 12% at 1 star down to 6% at 5 stars.</summary>
    public static float SpecialChance(float rating) => Mathf.Lerp(0.12f, 0.06f, Mathf.Clamp((rating - 1f) / 4f, 0f, 1f));

    /// <summary>How good a job is: pay per km the player has to ride (to the pickup, then the trip).</summary>
    public static float Goodness(JobData job) =>
        job.Reward / ((job.DistanceToPickup + job.Distance) / GameManager.PixelsPerKm + 0.5f);

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

        var rating = GameManager.Instance.Reputation.Rating;
        var pickups = FindPickupCandidates(origin, count * CandidateFactor);
        Shuffle(pickups);

        // Normal candidates, best first.
        var pool = new List<JobData>();
        for (var i = 0; i < count * CandidateFactor; i++)
        {
            var template = PickTemplate();
            var pickup = pickups[i % pickups.Count];
            var delivery = FindDelivery(pickup, template.MinDistance, template.MaxDistance);
            if (delivery != null)
                pool.Add(BuildJob(template, pickup, delivery, origin));
        }
        pool.Sort((a, b) => Goodness(b).CompareTo(Goodness(a)));

        // Rank position = random^skew: skew 2 at 5 stars (favors the top), 1 at 3 stars, 0.5 at 1 star.
        var skew = Mathf.Pow(2f, (rating - 3f) / 2f);
        var premiumChance = PremiumChance(rating);
        var specialChance = SpecialChance(rating);
        for (var slot = 0; slot < count; slot++)
        {
            var roll = _rng.Randf();
            JobData? job = null;
            if (roll < premiumChance)
                job = BuildPremium(origin, pickups);
            else if (roll < premiumChance + specialChance)
                job = BuildSpecial(origin, pickups);
            if (job == null && pool.Count > 0)
            {
                var index = Mathf.Min((int)(Mathf.Pow(_rng.Randf(), skew) * pool.Count), pool.Count - 1);
                job = pool[index];
                pool.RemoveAt(index);
            }
            if (job != null)
                jobs.Add(job);
        }

        // Rare jobs first, then the closest pickups.
        return jobs
            .OrderBy(j => j.Tier == JobData.JobTier.Normal)
            .ThenBy(j => j.DistanceToPickup)
            .ToList();
    }

    /// <summary>Re-times the pickup from the player's current position (they may have moved since the board was generated).</summary>
    public void UpdatePickupTimeLimit(JobData job, Vector2 origin)
    {
        var template = _templates.FirstOrDefault(t => t.TemplateId == job.TemplateId);
        if (template == null || !template.IsTimed || !job.IsTimed)
            return; // untimed templates, and special jobs (which drop the limits)
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

    /// <summary>Close pickup (one of the 3 nearest), short trip, double pay.</summary>
    private JobData? BuildPremium(Vector2 origin, List<DeliveryLocation> pickups)
    {
        var template = PickTemplate();
        var nearest = pickups.OrderBy(p => RouteDistance(origin, p.Position)).Take(3).ToList();
        if (nearest.Count == 0)
            return null;
        var pickup = nearest[_rng.RandiRange(0, nearest.Count - 1)];
        var maxTrip = Mathf.Lerp(template.MinDistance, template.MaxDistance, 0.35f);
        var delivery = FindDelivery(pickup, template.MinDistance, maxTrip);
        if (delivery == null)
            return null;
        var job = BuildJob(template, pickup, delivery, origin);
        job.Tier = JobData.JobTier.Premium;
        job.Reward = Mathf.RoundToInt(job.Reward * PremiumRewardMultiplier);
        return job;
    }

    /// <summary>Very long trip, half pay, no time limits; the customer adds extra 5-star reviews.</summary>
    private JobData? BuildSpecial(Vector2 origin, List<DeliveryLocation> pickups)
    {
        var template = PickTemplate();
        if (pickups.Count == 0)
            return null;
        var pickup = pickups[_rng.RandiRange(0, pickups.Count - 1)];
        var minTrip = Mathf.Max(SpecialMinDistance, template.MaxDistance * 1.5f);
        var delivery = FindDelivery(pickup, minTrip, minTrip * 2f); // falls back to the farthest address
        if (delivery == null)
            return null;
        var job = BuildJob(template, pickup, delivery, origin);
        job.Tier = JobData.JobTier.Special;
        job.Reward = Mathf.Max(1, Mathf.RoundToInt(job.Reward * SpecialRewardMultiplier));
        job.RatingBonus = SpecialRatingBonus;
        job.PickupTimeLimit = 0f;
        job.DeliveryTimeLimit = 0f;
        job.TipTimeLimit = 0f;
        job.TipShare = 0f;
        job.LatePenalty = 0f;
        return job;
    }

    /// <summary>
    /// A random address <paramref name="minDistance"/>-<paramref name="maxDistance"/> away from the pickup;
    /// when none is in range, the one closest to the middle of the range.
    /// </summary>
    private DeliveryLocation? FindDelivery(DeliveryLocation pickup, float minDistance, float maxDistance)
    {
        var inRange = new List<DeliveryLocation>();
        DeliveryLocation? fallback = null;
        var bestScore = float.MaxValue;
        var ideal = (minDistance + maxDistance) * 0.5f;

        foreach (var loc in _map.Locations)
        {
            if (loc == pickup)
                continue;
            var d = RouteDistance(pickup.Position, loc.Position);
            if (d >= minDistance && d <= maxDistance)
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
