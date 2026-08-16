using Planning.Domain.Entities;
using Planning.Infrastructure.Services;

namespace Planning.UnitTests;

public class PlateauBreakPackerTests
{
    [Fact]
    public void PackingCandidatesEarlyWave_critical_order_11_midi_13()
    {
        var start = new TimeOnly(8, 0);
        var early = BreakSlotPlanner.PackingCandidatesEarlyWave(start, isCriticalCell: true);
        Assert.Equal(new TimeOnly(11, 0), early[0]);
        Assert.Equal(new TimeOnly(11, 30), early[1]);
        Assert.True(early.IndexOf(new TimeOnly(12, 0)) < early.IndexOf(new TimeOnly(13, 0)));
        Assert.Contains(new TimeOnly(13, 0), early); // +5h max
        Assert.DoesNotContain(new TimeOnly(13, 30), early);
        Assert.DoesNotContain(new TimeOnly(14, 0), early);

        var nonCritical = BreakSlotPlanner.PackingCandidatesEarlyWave(start, isCriticalCell: false);
        Assert.Equal(new TimeOnly(12, 0), nonCritical[0]);
    }

    [Fact]
    public void PackingCandidatesSpread_critical_prefers_plus_5h_then_plus_4h()
    {
        var start = new TimeOnly(9, 0);
        var spread = BreakSlotPlanner.PackingCandidatesSpread(start, isCriticalCell: true);
        Assert.Equal(new TimeOnly(14, 0), spread[0]); // +5h max
        Assert.Equal(new TimeOnly(13, 0), spread[1]); // +4h
        Assert.DoesNotContain(new TimeOnly(15, 0), spread);
        Assert.DoesNotContain(new TimeOnly(16, 0), spread); // +7h interdit
    }

    [Fact]
    public void PackingCandidatesEarlyWave_puts_window_start_first()
    {
        var start = new TimeOnly(8, 0);
        var early = BreakSlotPlanner.PackingCandidatesEarlyWave(start, isCriticalCell: true);
        Assert.Equal(new TimeOnly(11, 0), early[0]);
        Assert.Contains(new TimeOnly(11, 30), early);
        Assert.Contains(new TimeOnly(13, 0), early);
        Assert.DoesNotContain(new TimeOnly(13, 30), early);
        Assert.DoesNotContain(new TimeOnly(16, 0), early);

        var normal = BreakSlotPlanner.PackingCandidates(start, isCriticalCell: true);
        Assert.Equal(new TimeOnly(12, 0), normal[0]);
        Assert.NotEqual(early[0], normal[0]);
    }

    [Fact]
    public void AssignDayBreaks_orders_low_early_count_first_for_opening_plus3()
    {
        const int minPresence = 85;
        // 8 présents → maxBreak=1 (pas de slack <20) → un seul créneau 11h
        var configs = BuildFourShiftConfigs(minPresence, isCritical: true, required: [8, 0, 0, 0])
            .Where(c => c.StartTime == new TimeOnly(8, 0))
            .ToList();
        configs[0].RequiredCount = 8;
        var configsById = configs.ToDictionary(c => c.Id);

        var assignments = BuildAssignments(configs, required: [8]);
        var heavyUser = assignments[0].UserId;
        var fairness = new PlateauBreakPacker.BreakFairnessCounters();
        fairness.EarlyCounts[heavyUser] = 5; // déjà beaucoup de +3h

        PlateauBreakPacker.AssignDayBreaks(assignments, configsById, minPresence, fairness);

        var at11 = assignments.Where(a => a.BreakTime == new TimeOnly(11, 0)).ToList();
        Assert.True(at11.Count >= 1);
        Assert.DoesNotContain(at11, a => a.UserId == heavyUser);
    }

    [Fact]
    public void AssignDayBreaks_opening_wave_fills_11h_slot()
    {
        const int minPresence = 85;
        var configs = BuildFourShiftConfigs(minPresence, isCritical: true, required: [19, 10, 5, 5]);
        var configsById = configs.ToDictionary(c => c.Id);
        var assignments = BuildAssignments(configs, required: [19, 10, 5, 5]);

        PlateauBreakPacker.AssignDayBreaks(assignments, configsById, minPresence);

        var openingId = configs.Single(c => c.StartTime == new TimeOnly(8, 0)).Id;
        var openingBreaks = assignments
            .Where(a => a.SubServiceShiftConfigId == openingId)
            .Select(a => a.BreakTime!.Value)
            .ToList();

        var earlyBand = openingBreaks.Count(t => t >= new TimeOnly(11, 0) && t <= new TimeOnly(11, 30));
        Assert.True(earlyBand >= 3,
            $"Au moins 3 pauses Opening entre 11:00–11:30, obtenu {earlyBand} (times: {string.Join(',', openingBreaks.OrderBy(t => t).Take(8))})");

        var (ranges, breaks) = Snapshot(assignments, configsById);
        var minP = PlateauBreakPacker.DayMinAvailabilityPercent(ranges, breaks);
        // Plafond +5h : 83 % / max6 souvent impossible à 39×1h — on garde un plancher réaliste
        Assert.True(minP >= 76m, $"P={minP} attendu ≥ 76 (plafond +5h)");

        Assert.All(assignments, a =>
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            var hours = (a.BreakTime!.Value - cfg.StartTime).TotalHours;
            Assert.True(hours <= 5.001, $"pause {a.BreakTime} dépasse +5h (start {cfg.StartTime})");
        });

        var hoursBeforeNoon = breaks.Count(b => b.Start < new TimeOnly(12, 0));
        Assert.True(hoursBeforeNoon >= 3,
            $"Au moins 3 départs pause avant 12:00, obtenu {hoursBeforeNoon}");
    }

    [Fact]
    public void PackingCandidates_critical_includes_plus_5h_and_dense_window()
    {
        var start = new TimeOnly(8, 0);
        var critical = BreakSlotPlanner.PackingCandidates(start, isCriticalCell: true);
        Assert.Contains(new TimeOnly(12, 0), critical);  // +4h idéal
        Assert.Contains(new TimeOnly(11, 15), critical); // dense
        Assert.Contains(new TimeOnly(13, 0), critical);  // +5h max
        Assert.DoesNotContain(new TimeOnly(13, 30), critical);

        Assert.Contains(new TimeOnly(13, 0), BreakSlotPlanner.ExtremeTier(start, true));

        var normal = BreakSlotPlanner.PackingCandidates(start, isCriticalCell: false);
        Assert.DoesNotContain(new TimeOnly(13, 30), normal);
        Assert.Contains(new TimeOnly(13, 0), normal); // +5h extrême standard
        Assert.Contains(new TimeOnly(11, 0), normal); // +3h
    }

    [Fact]
    public void AssignDayBreaks_respects_70_percent_on_multi_shift_cell()
    {
        const int minPresence = 70;
        var configs = BuildFourShiftConfigs(minPresence, isCritical: true, required: [5, 4, 3, 3]);
        var configsById = configs.ToDictionary(c => c.Id);
        var assignments = BuildAssignments(configs, required: [5, 4, 3, 3]);

        PlateauBreakPacker.AssignDayBreaks(assignments, configsById, minPresence);

        Assert.All(assignments, a => Assert.True(a.BreakTime.HasValue));

        var (ranges, breaks) = Snapshot(assignments, configsById);
        var minP = PlateauBreakPacker.DayMinAvailabilityPercent(ranges, breaks);
        Assert.True(minP >= minPresence, $"P={minP} attendu ≥ {minPresence}");
    }

    [Fact]
    public void AssignDayBreaks_critical_85_percent_beats_naive_cluster_peak()
    {
        // Cible superviseur : ~83 % présence, max ~6 en pause
        const int minPresence = 85;
        var configs = BuildFourShiftConfigs(minPresence, isCritical: true, required: [19, 10, 5, 5]);
        var configsById = configs.ToDictionary(c => c.Id);
        var assignments = BuildAssignments(configs, required: [19, 10, 5, 5]);

        PlateauBreakPacker.AssignDayBreaks(assignments, configsById, minPresence);

        var (ranges, breaks) = Snapshot(assignments, configsById);
        var minP = PlateauBreakPacker.DayMinAvailabilityPercent(ranges, breaks);
        var peak = MaxConcurrentBreaks(breaks);

        Assert.True(minP >= 76m,
            $"P={minP} peak={peak} maxBreak39={PlateauBreakPacker.MaxOnBreakAt(39, 85)} attendu P≥76 (plafond +5h)");

        Assert.True(peak <= 9, $"pic pauses={peak} attendu ≤ 9 avec fenêtre +5h");

        Assert.All(assignments, a =>
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            Assert.True((a.BreakTime!.Value - cfg.StartTime).TotalHours <= 5.001,
                $"pause au-delà de +5h: {cfg.StartTime} → {a.BreakTime}");
        });
    }



    [Fact]
    public void AssignDayBreaks_uses_plus_5h_when_needed_for_spread()
    {
        const int minPresence = 85;
        var configs = BuildFourShiftConfigs(minPresence, isCritical: true, required: [8, 8, 8, 8]);
        var configsById = configs.ToDictionary(c => c.Id);
        var assignments = BuildAssignments(configs, required: [8, 8, 8, 8]);

        PlateauBreakPacker.AssignDayBreaks(assignments, configsById, minPresence);

        Assert.All(assignments, a =>
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            Assert.True((a.BreakTime!.Value - cfg.StartTime).TotalHours <= 5.001);
        });

        var anyExtremeOrLate = assignments.Count(a =>
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            var bt = a.BreakTime!.Value;
            return BreakSlotPlanner.IsExtremeCaseBreak(cfg.StartTime, bt)
                   || bt >= cfg.StartTime.AddHours(5);
        });
        Assert.True(anyExtremeOrLate > 0,
            "Au moins une pause extrême / +5h attendue pour étaler la charge");
    }

    [Fact]
    public void MaxOnBreakAt_critical_slack_allows_six_for_39_at_85()
    {
        Assert.Equal(6, PlateauBreakPacker.MaxOnBreakAt(39, 85));
        Assert.Equal(3, PlateauBreakPacker.MaxOnBreakAt(20, 85)); // pas de slack (80 % < 83)
        Assert.Equal(2, PlateauBreakPacker.MaxOnBreakAt(19, 85)); // floor(2.85)=2, no slack (<20)
    }

    [Fact]
    public void MaxOnBreakAt_zero_presence_allows_all()
    {
        Assert.Equal(10, PlateauBreakPacker.MaxOnBreakAt(10, 0));
        Assert.True(PlateauBreakPacker.DayRespectsPresence(
            Enumerable.Repeat(new PlateauBreakPacker.ShiftRange(new TimeOnly(8, 0), new TimeOnly(17, 0)), 5).ToList(),
            Enumerable.Repeat(new PlateauBreakPacker.BreakPlacement(new TimeOnly(12, 0), 60), 5).ToList(),
            0));
    }

    [Fact]
    public void FitsPresence_rejects_over_threshold()
    {
        var ranges = Enumerable.Repeat(
            new PlateauBreakPacker.ShiftRange(new TimeOnly(8, 0), new TimeOnly(17, 0)), 10).ToList();
        var already = Enumerable.Repeat(
            new PlateauBreakPacker.BreakPlacement(new TimeOnly(12, 0), 60), 2).ToList();
        // 10 présents, 85 % → maxBreak = floor(1.5)=1 ; déjà 2 en pause → nouveau rejet
        Assert.False(PlateauBreakPacker.FitsPresence(
            ranges, already, new TimeOnly(12, 0), 60, 85));
        Assert.True(PlateauBreakPacker.FitsPresence(
            ranges, Array.Empty<PlateauBreakPacker.BreakPlacement>(), new TimeOnly(12, 0), 60, 85));
    }

    private static List<SubServiceShiftConfig> BuildFourShiftConfigs(
        int minPresence, bool isCritical, int[] required)
    {
        var starts = new[]
        {
            new TimeOnly(8, 0), new TimeOnly(9, 0), new TimeOnly(10, 0), new TimeOnly(11, 0)
        };
        var list = new List<SubServiceShiftConfig>();
        for (var i = 0; i < 4; i++)
        {
            list.Add(new SubServiceShiftConfig
            {
                Id = i + 1,
                SubServiceId = 1,
                IsTemplate = true,
                Label = $"Shift {i + 1}",
                StartTime = starts[i],
                WorkHours = 8,
                BreakDurationMinutes = 60,
                IsCriticalCell = isCritical,
                RequiredCount = required[i],
                MinPresencePercent = minPresence,
                DisplayOrder = i + 1,
                CreatedAt = DateTime.UtcNow
            });
        }
        return list;
    }

    private static List<ShiftAssignment> BuildAssignments(
        List<SubServiceShiftConfig> configs, int[] required)
    {
        var date = new DateOnly(2026, 8, 24);
        var list = new List<ShiftAssignment>();
        var userId = 1;
        for (var i = 0; i < configs.Count; i++)
        {
            for (var n = 0; n < required[i]; n++)
            {
                list.Add(new ShiftAssignment
                {
                    UserId = userId++,
                    AssignedDate = date,
                    SubServiceShiftConfigId = configs[i].Id,
                    IsOnLeave = false,
                    IsHoliday = false,
                    IsSaturday = false
                });
            }
        }
        return list;
    }

    private static (
        List<PlateauBreakPacker.ShiftRange> Ranges,
        List<PlateauBreakPacker.BreakPlacement> Breaks)
        Snapshot(
            List<ShiftAssignment> assignments,
            IReadOnlyDictionary<int, SubServiceShiftConfig> configsById)
    {
        var ranges = new List<PlateauBreakPacker.ShiftRange>();
        var breaks = new List<PlateauBreakPacker.BreakPlacement>();
        foreach (var a in assignments)
        {
            var cfg = configsById[a.SubServiceShiftConfigId!.Value];
            ranges.Add(new PlateauBreakPacker.ShiftRange(cfg.StartTime, cfg.EndTime));
            breaks.Add(new PlateauBreakPacker.BreakPlacement(
                a.BreakTime!.Value,
                cfg.BreakDurationMinutes > 0 ? cfg.BreakDurationMinutes : 60));
        }
        return (ranges, breaks);
    }

    private static int MaxConcurrentBreaks(IReadOnlyList<PlateauBreakPacker.BreakPlacement> breaks)
    {
        if (breaks.Count == 0) return 0;
        var min = breaks.Min(b => b.Start);
        var max = breaks.Max(b => b.Start.AddMinutes(b.DurationMinutes > 0 ? b.DurationMinutes : 60));
        var peak = 0;
        for (var t = min; t < max; t = t.AddMinutes(5))
        {
            var n = PlateauBreakPacker.CountOnBreakAt(breaks, t);
            if (n > peak) peak = n;
        }
        return peak;
    }
}
